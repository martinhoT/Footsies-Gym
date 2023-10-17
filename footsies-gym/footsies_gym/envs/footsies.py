from collections import deque
import socket
import json
import subprocess
import gymnasium as gym
from os import path
from math import log
from time import sleep, monotonic
from gymnasium import spaces
from ..state import FootsiesState
from ..moves import FootsiesMove, footsies_move_id_to_index
from .exceptions import FootsiesGameClosedError

# TODO: move training agent input reading (through socket comms) to Update() instead of FixedUpdate()
# TODO: dynamically change the game's timeScale value depending on the estimated framerate
# TODO: self-play support

MAX_STATE_MESSAGE_BYTES = 4096


class FootsiesEnv(gym.Env):
    metadata = {"render_modes": "human", "render_fps": 60}

    def __init__(
        self,
        frame_delay: int = 20,
        render_mode: str = None,
        game_path: str = "./Build/FOOTSIES",
        game_address: str = "localhost",
        game_port: int = 11000,
        fast_forward: bool = True,
        synced: bool = True,
        by_example: bool = False,
        log_file: str = None,
        log_file_overwrite: bool = False,
    ):
        """
        FOOTSIES training environment

        Parameters
        ----------
        frame_delay: int
            with how many frames of delay should environment states be sent to the agent (meant for human reaction time emulation)
        render_mode: str
            how should the environment be rendered
        game_path: str
            path to the FOOTSIES executable. Preferably a fully qualified path
        game_address: str
            address of the FOOTSIES instance
        game_port: int
            port of the FOOTSIES instance
        fast_forward: bool
            whether to run the game at a much faster rate than normal
        synced: bool
            whether to wait for the agent's input before proceeding in the environment. It doesn't make much sense to let both `fast_forward` to be `True` and `synced` be `False`
        by_example: bool
            whether to simply observe another autonomous player play the game. Actions passed in `step()` are ignored
        log_file: str
            path of the log file to which the FOOTSIES instance logs will be written. If `None` logs will be written to the default Unity location
        log_file_overwrite: bool
            whether to overwrite the specified log file if it already exists
        """
        self.game_path = game_path
        self.game_address = game_address
        self.game_port = game_port
        self.fast_forward = fast_forward
        self.synced = synced
        self.by_example = by_example
        self.log_file = log_file
        self.log_file_overwrite = log_file_overwrite

        # Create a queue containing the last `frame_delay` frames so that we can send delayed frames to the agent
        self.delayed_frame_queue: deque[FootsiesState] = deque([], maxlen=frame_delay)

        assert render_mode is None or render_mode in self.metadata["render_modes"]
        self.render_mode = render_mode

        self.comm = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.comm.setblocking(True)
        self._game_instance = None
        self._connected = False

        # Don't consider the end-of-round moves
        relevant_moves = set(FootsiesMove) - {FootsiesMove.WIN, FootsiesMove.DEAD}
        maximum_move_duration = max(m.value.duration for m in relevant_moves)

        # The observation space is divided into 2 columns, the first for player 1 and the second for player 2
        self.observation_space = spaces.Dict(
            {
                "guard": spaces.MultiDiscrete([4, 4]),  # 0..3
                "move": spaces.MultiDiscrete(
                    [len(relevant_moves), len(relevant_moves)]
                ),
                "move_frame": spaces.Box(low=0.0, high=maximum_move_duration, shape=(2,)),
                "position": spaces.Box(low=-4.4, high=4.4, shape=(2,)),
            }
        )

        # 3 actions, which can be combined: left, right, attack
        self.action_space = spaces.MultiBinary(3)

        # -1 for losing, 1 for winning, 0 otherwise
        self.reward_range = (-1, 1)

        self._current_state = None

    def _instantiate_game(self):
        """
        Start the FOOTSIES process in the background, with the specified render mode.
        No-op if already instantiated
        """
        if self._game_instance is None:
            args = [
                self.game_path,
                "--mute",
                "--training",
                "--address",
                self.game_address,
                "--port",
                str(self.game_port),
            ]
            if self.render_mode is None:
                args.extend(["-batchmode", "-nographics"])
            if self.fast_forward:
                args.append("--fast-forward")
            if self.synced:
                args.append("--synced")
            if self.by_example:
                args.append("--by-example")
            if self.log_file is not None:
                if not self.log_file_overwrite and path.exists(self.log_file):
                    raise FileExistsError(
                        f"the log file '{self.log_file}' already exists and the environment was set to not overwrite it"
                    )
                args.extend(["-logFile", self.log_file])

            self._game_instance = subprocess.Popen(
                args, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL
            )

    def _connect_to_game(self, retry_delay: float = 0.5):
        """
        Connect to the FOOTSIES instance specified by the environment's address and port.
        If the connection is refused, wait `retry_delay` seconds before trying again.
        No-op if already connected
        """
        while not self._connected:
            try:
                self.comm.connect((self.game_address, self.game_port))
                self._connected = True

            except ConnectionRefusedError:
                sleep(
                    retry_delay
                )  # avoid constantly pestering the game for a connection
                continue

    def _receive_and_update_state(self) -> FootsiesState:
        """Receive the environment state from the FOOTSIES instance"""
        try:
            state_json = self.comm.recv(MAX_STATE_MESSAGE_BYTES).decode("utf-8")
        except OSError:
            raise FootsiesGameClosedError

        # The communication is assumed to work correctly, so if a message wasn't received then the game must have closed
        if len(state_json) == 0:
            raise FootsiesGameClosedError

        self._current_state = FootsiesState(**json.loads(state_json))

        return self._current_state

    def _send_action(self, action: "tuple[bool, bool, bool]"):
        """Send an action to the FOOTSIES instance"""
        action_message = bytearray(action)
        try:
            self.comm.sendall(action_message)
        except OSError:
            raise FootsiesGameClosedError

    def _extract_obs(self, state: FootsiesState) -> dict:
        """Extract the relevant observation data from the environment state"""
        # Simplify the number of frames since the start of the move for moves that last indefinitely
        p1_move_frame_simple = (
            0
            if state.p1_move
            in {FootsiesMove.STAND, FootsiesMove.FORWARD, FootsiesMove.BACKWARD}
            else state.p1_move_frame
        )
        p2_move_frame_simple = (
            0
            if state.p2_move
            in {FootsiesMove.STAND, FootsiesMove.FORWARD, FootsiesMove.BACKWARD}
            else state.p2_move_frame
        )

        return {
            "guard": [state.p1_guard, state.p2_guard],
            "move": [
                footsies_move_id_to_index[state.p1_move],
                footsies_move_id_to_index[state.p2_move],
            ],
            "move_frame": [p1_move_frame_simple, p2_move_frame_simple],
            "position": [state.p1_position, state.p2_position],
        }

    def _extract_info(self, state: FootsiesState) -> dict:
        """Get the current additional info from the environment state"""
        return {"frame": state.global_frame}

    def reset(self) -> "tuple[dict, dict]":
        self.delayed_frame_queue.clear()

        self._instantiate_game()
        self._connect_to_game()
        while len(self.delayed_frame_queue) < self.delayed_frame_queue.maxlen:
            self.delayed_frame_queue.append(self._receive_and_update_state())
            # Do nothing until we get the first state
            self._send_action(
                [False, False, False]
            )  # TODO: should we allow the agent to take actions on unknown states anyway?

        state = self.delayed_frame_queue.popleft()
        self.delayed_frame_queue.append(self._receive_and_update_state())

        return self._extract_obs(state), self._extract_info(state)

    # Step already assumes that the queue of delayed frames is full from reset()
    def step(
        self, action: "tuple[bool, bool, bool]"
    ) -> "tuple[dict, float, bool, bool, dict]":
        # Send action
        self._send_action(action)

        # Flag the current state as being outdated so that we can receive a new one
        state = self.delayed_frame_queue.popleft()
        self.delayed_frame_queue.append(self._receive_and_update_state())

        # Get next observation, info and reward
        obs = self._extract_obs(state)
        info = self._extract_info(state)

        terminated = state.p1_vital == 0 or state.p2_vital == 0
        if terminated:
            reward = 1 if state.p2_vital == 0 else -1
        else:
            reward = 0

        # Environment is never truncated
        return obs, reward, terminated, False, info

    def close(self):
        self.comm.close()  # game should close as well after socket is closed


if __name__ == "__main__":
    env = FootsiesEnv(game_path="Build/FOOTSIES.x86_64", render_mode=None)

    # Keep track of how many frames/steps were processed each second so that we can adjust how fast the game runs
    frames = 0
    seconds = 0
    # Multiply the counters by the decay to avoid infinitely increasing counters and prioritize recent values.
    # Set to a value such that the 1000th counter value in the past will have a weight of 1%
    fps_counter_decay = 0.01 ** (1 / 1000)

    episode_counter = 0

    try:
        while True:
            terminated = False
            observation, info = env.reset()
            while not terminated:
                time_current = monotonic()  # for fps tracking
                action = env.action_space.sample()
                next_observation, reward, terminated, truncated, info = env.step(action)

                frames = (frames * fps_counter_decay) + 1
                seconds = (seconds * fps_counter_decay) + monotonic() - time_current
                print(
                    f"Episode {episode_counter:>3} | {0 if seconds == 0 else frames / seconds:>3.2f} fps",
                    end="\r",
                )
            episode_counter += 1

    except KeyboardInterrupt:
        print("Training manually interrupted by the keyboard")

    except FootsiesGameClosedError:
        print(
            "Training interrupted due to the game connection being lost (did the game close?)"
        )

    finally:
        env.close()
