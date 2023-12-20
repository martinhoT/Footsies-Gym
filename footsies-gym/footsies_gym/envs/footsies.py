from collections import deque
import socket
import json
import subprocess
import struct
import gymnasium as gym
from os import path
from typing import Callable, Tuple
from time import sleep, monotonic
from gymnasium import spaces
from ..state import FootsiesState
from ..moves import FootsiesMove, footsies_move_id_to_index
from .exceptions import FootsiesGameClosedError

# TODO: move training agent input reading (through socket comms) to Update() instead of FixedUpdate()
# TODO: dynamically change the game's timeScale value depending on the estimated framerate
# TODO: outside environment truncations are very costly, they require performing a hard_reset(). It should be optimized (allow passing commands through actions?)


class FootsiesEnv(gym.Env):
    metadata = {"render_modes": "human", "render_fps": 60}
    
    STATE_MESSAGE_SIZE_BYTES = 4
    COMM_TIMEOUT = 3.0

    def __init__(
        self,
        frame_delay: int = 0,
        render_mode: str = None,
        game_path: str = "./Build/FOOTSIES",
        game_address: str = "localhost",
        game_port: int = 11000,
        fast_forward: bool = True,
        sync_mode: str = "synced_blocking",
        by_example: bool = False,
        opponent: Callable[[dict], Tuple[bool, bool, bool]] = None,
        opponent_port: int = 11001,
        vs_player: bool = False,
        dense_reward: bool = True,
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
        sync_mode: str
            one of "async", "synced_non_blocking" or "synced_blocking":
            - "async": process the game without making sure the agents have provided inputs. Doesn't make much sense to have `fast_forward` enabled as well
            - "synced_non_blocking": at every time step, the game will wait for all agents' inputs before proceeding. Communications are non-blocking, but may slow down game interaction speed to half
            - "synced_blocking": similar to above, but communications are blocking. If using human `render_mode`, the game may have frozen rendering

        by_example: bool
            whether to simply observe another autonomous player play the game. Actions passed in `step()` are ignored
        opponent: Callable[[dict], Tuple[bool, bool, bool]]
            if not `None`, it's the policy to be followed by the agent's opponent. It's recommended that the environment is `synced` if a policy is supplied, since both the agent and the opponent will be acting at the same time
        opponent_port: int
            if an opponent policy is supplied, then this is the game's port to which the opponent's actions are sent
        vs_player: bool
            whether to play against a human opponent (who will play as P2). It doesn't make much sense to let `fast_forward` be `True`. Not allowed if `opponent` is specified
        dense_reward: bool
            whether to use dense reward on the environment, rather than sparse reward. Sparse reward only rewards the agent on win or loss (1 and -1, respectively). Dense reward rewards the agent on inflicting/receiving guard damage (0.3 and -0.3, respectively), but on win/loss a compensation is given such that the sum is like the sparse reward (1 and -1, respectively)
        log_file: str
            path of the log file to which the FOOTSIES instance logs will be written. If `None` logs will be written to the default Unity location
        log_file_overwrite: bool
            whether to overwrite the specified log file if it already exists

        WARNING: if the environment has an unexpected error or closes incorrectly, it's possible the game process will still be running in the background. It should be closed manually in that case
        """
        if opponent is not None and vs_player:
            raise ValueError(
                "custom opponent and human opponent can't be specified together"
            )

        self.game_path = game_path
        self.game_address = game_address
        self.game_port = game_port
        self.fast_forward = fast_forward
        self.sync_mode = sync_mode
        self.by_example = by_example
        self.opponent = opponent
        self.opponent_port = opponent_port
        self.vs_player = vs_player
        self.dense_reward = dense_reward
        self.log_file = log_file
        self.log_file_overwrite = log_file_overwrite

        # Create a queue containing the last `frame_delay` frames so that we can send delayed frames to the agent
        # The actual capacity has one extra space to accomodate for the case that `frame_delay` is 0, so that
        # the only state to send (the most recent one) can be effectively sent through the queue
        self.delayed_frame_queue: deque[FootsiesState] = deque(
            [], maxlen=frame_delay + 1
        )

        assert render_mode is None or render_mode in self.metadata["render_modes"]
        self.render_mode = render_mode

        self.comm = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.comm.settimeout(self.COMM_TIMEOUT)
        self._connected = False
        self._game_instance = None

        self.opponent_comm = (
            socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            if self.opponent is not None
            else None
        )
        if self.opponent_comm is not None:
            self.opponent_comm.settimeout(self.COMM_TIMEOUT)
        self._opponent_connected = False

        # Don't consider the end-of-round moves
        relevant_moves = set(FootsiesMove) - {FootsiesMove.WIN, FootsiesMove.DEAD}
        maximum_move_duration = max(m.value.duration for m in relevant_moves)

        # The observation space is divided into 2 columns, the first for player 1 (the agent) and the second for player 2
        self.observation_space = spaces.Dict(
            {
                "guard": spaces.Box(low=0.0, high=3.0, shape=(2,)),  # 0..3
                "move": spaces.MultiDiscrete(
                    [len(relevant_moves), len(relevant_moves)]
                ),
                "move_frame": spaces.Box(
                    low=0.0, high=maximum_move_duration, shape=(2,)
                ),
                "position": spaces.Box(low=-4.4, high=4.4, shape=(2,)),
            }
        )

        # 3 actions, which can be combined: left, right, attack
        self.action_space = spaces.MultiBinary(3)

        # -1 for losing, 1 for winning, 0 otherwise
        self.reward_range = (-1, 1)

        # Save the most recent state internally
        # Useful to differentiate between the previous and current environment state
        self._current_state = None

        # The latest observation that the agent saw
        # Required in order to communicate to the opponent the same observation
        self._last_passed_observation = None

        # Keep track of the total reward during this episode
        # Only used when dense rewards are enabled
        self._cummulative_episode_reward = 0.0

        # Keep track of whether the current episode is finished
        # Necessary when calling reset() when it isn't finished, which will require a hard reset
        self.has_terminated = True

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
                "--p1-address",
                self.game_address,
                "--p1-port",
                str(self.game_port),
            ]
            if self.render_mode is None:
                args.extend(["-batchmode", "-nographics"])
            if self.fast_forward:
                args.append("--fast-forward")
            if self.sync_mode == "synced_non_blocking":
                args.append("--synced-non-blocking")
            elif self.sync_mode == "synced_blocking":
                args.append("--synced-blocking")
            if self.by_example:
                args.append("--p1-bot")
                args.append("--p1-spectator")
            if self.vs_player:
                args.append("--p2-player")
            elif self.opponent is None:
                args.append("--p2-bot")
            else:
                args.extend(
                    [
                        "--p2-address",
                        self.game_address,
                        "--p2-port",
                        str(self.opponent_port),
                        "--p2-no-state",
                    ]
                )
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
        No-op if already connected.

        If an opponent was supplied, then try establishing a connection for the opponent as well.
        """
        while not self._connected:
            try:
                self.comm.connect((self.game_address, self.game_port))
                self._connected = True

            except (ConnectionRefusedError, ConnectionAbortedError):
                sleep(
                    retry_delay
                )  # avoid constantly pestering the game for a connection
                continue

        if self.opponent is not None:
            while not self._opponent_connected:
                try:
                    self.opponent_comm.connect((self.game_address, self.opponent_port))
                    self._opponent_connected = True

                except (ConnectionRefusedError, ConnectionAbortedError):
                    sleep(
                        retry_delay
                    )  # avoid constantly pestering the game for a connection
                    continue

    def _game_recv(self, size: int) -> bytes:
        """Receive a message of the given size from the FOOTSIES instance. Raises `FootsiesGameClosedError` if a problem occurred"""
        try:
            res = self.comm.recv(size)
        except TimeoutError:
            raise FootsiesGameClosedError("game took too long to respond, will assume it's closed")

        # The communication is assumed to work correctly, so if a message wasn't received then the game must have closed
        if len(res) == 0:
            raise FootsiesGameClosedError("game has closed")

        return res

    def _receive_and_update_state(self) -> FootsiesState:
        """Receive the environment state from the FOOTSIES instance"""
        state_json_size_bytes = self._game_recv(self.STATE_MESSAGE_SIZE_BYTES)
        state_json_size = struct.unpack("!I", state_json_size_bytes)[0]

        state_json = self._game_recv(state_json_size).decode("utf-8")
        
        self._current_state = FootsiesState(**json.loads(state_json))

        return self._current_state

    def _send_action(
        self, action: "tuple[bool, bool, bool]", is_opponent: bool = False
    ):
        """Send an action to the FOOTSIES instance"""
        action_message = bytearray(action)
        try:
            if is_opponent:
                self.opponent_comm.sendall(action_message)
            else:
                self.comm.sendall(action_message)
        except OSError:
            raise FootsiesGameClosedError

    def _extract_obs(self, state: FootsiesState) -> dict:
        """Extract the relevant observation data from the environment state"""
        # Simplify the number of frames since the start of the move for moves that last indefinitely
        p1_move_frame_simple = (
            0
            if state.p1_move
            in {
                FootsiesMove.STAND.value.id,
                FootsiesMove.FORWARD.value.id,
                FootsiesMove.BACKWARD.value.id,
            }
            else state.p1_move_frame
        )
        p2_move_frame_simple = (
            0
            if state.p2_move
            in {
                FootsiesMove.STAND.value.id,
                FootsiesMove.FORWARD.value.id,
                FootsiesMove.BACKWARD.value.id,
            }
            else state.p2_move_frame
        )

        return {
            "guard": (state.p1_guard, state.p2_guard),
            "move": (
                footsies_move_id_to_index[state.p1_move],
                footsies_move_id_to_index[state.p2_move],
            ),
            "move_frame": (p1_move_frame_simple, p2_move_frame_simple),
            "position": (state.p1_position, state.p2_position),
        }

    def _extract_info(self, state: FootsiesState) -> dict:
        """Get the current additional info from the environment state"""
        return {
            "frame": state.global_frame,
            "p1_action": state.p1_most_recent_action,
            "p2_action": state.p2_most_recent_action,
            "p1_move": footsies_move_id_to_index[state.p1_move],
            "p2_move": footsies_move_id_to_index[state.p2_move],
        }

    def _get_sparse_reward(
        self, state: FootsiesState, next_state: FootsiesState, terminated: bool
    ) -> float:
        """Get the sparse reward from this environment step. Equal to 1 or -1 on win/loss, respectively"""
        return (1 if next_state.p2_vital == 0 else -1) if terminated else 0

    def _get_dense_reward(
        self, state: FootsiesState, next_state: FootsiesState, terminated: bool
    ) -> float:
        """Get the dense reward from this environment step. Sums up to 1 or -1 on win/loss, but is also given when inflicting/dealing guard damage (0.3 and -0.3, respectively)"""
        reward = 0.0
        if next_state.p1_guard < state.p1_guard:
            reward -= 0.3
        if next_state.p2_guard < state.p2_guard:
            reward += 0.3

        self._cummulative_episode_reward += reward

        if terminated:
            reward += (
                1 if next_state.p2_vital == 0 else -1
            ) - self._cummulative_episode_reward

        return reward

    def reset(self, *, seed: int = None, options: dict = None) -> "tuple[dict, dict]":
        if not self.has_terminated:
            self._hard_reset()
        
        self.delayed_frame_queue.clear()
        self._cummulative_episode_reward = 0.0

        self._instantiate_game()
        self._connect_to_game()
        first_state = self._receive_and_update_state()
        # We leave a space at the end of the queue since insertion of the most recent state happens before popping the oldest state.
        # This is done so that the case when `frame_delay` is 0 is correctly handled
        while len(self.delayed_frame_queue) < self.delayed_frame_queue.maxlen - 1:
            # Give the agent the same initial state but repeated (`frame_delay` - 1) times
            self.delayed_frame_queue.append(first_state)

        # The episode can't terminate right in the beginning
        # This will also allow reset() to be called right after reset()
        self.has_terminated = False

        obs = self._extract_obs(first_state)
        # Create a copy of this observation (make sure it's not edited because 'obs' was changed afterwards, which may happen with wrappers)
        self._last_passed_observation = obs.copy()
        return obs, self._extract_info(first_state)

    # Step already assumes that the queue of delayed frames is full from reset()
    def step(
        self, action: "tuple[bool, bool, bool]"
    ) -> "tuple[dict, float, bool, bool, dict]":
        # Send action
        if not self.by_example:
            self._send_action(action, is_opponent=False)

        if self.opponent is not None:
            opponent_action = self.opponent(self._last_passed_observation)
            self._send_action(opponent_action, is_opponent=True)

        # Save the state before the environment step for later
        previous_state = self._current_state

        # Store the most recent state first and then take the oldest one
        most_recent_state = self._receive_and_update_state()
        self.delayed_frame_queue.append(most_recent_state)
        state = self.delayed_frame_queue.popleft()

        # In the terminal state, the defeated opponent gets into a move (DEAD) that doesn't occur throughout the game, so in that case we default to STAND
        state.p1_move = (
            state.p1_move
            if state.p1_move
            not in {FootsiesMove.DEAD.value.id, FootsiesMove.WIN.value.id}
            else FootsiesMove.STAND.value.id
        )
        state.p2_move = (
            state.p2_move
            if state.p2_move
            not in {FootsiesMove.DEAD.value.id, FootsiesMove.WIN.value.id}
            else FootsiesMove.STAND.value.id
        )

        # Get next observation, info and reward
        obs = self._extract_obs(state)
        info = self._extract_info(state)

        terminated = most_recent_state.p1_vital == 0 or most_recent_state.p2_vital == 0
        reward = (
            self._get_dense_reward(previous_state, most_recent_state, terminated)
            if self.dense_reward
            else self._get_sparse_reward(previous_state, most_recent_state, terminated)
        )

        # Enable reset() without needing hard_reset() if episode terminated normally on this step
        self.has_terminated = terminated

        # Create a copy of this observation, as is done in reset()
        self._last_passed_observation = obs.copy()

        # Environment is never truncated
        return obs, reward, terminated, False, info

    def close(self):
        self.comm.close()  # game should close as well after socket is closed
        if self.opponent_comm is not None:
            self.opponent_comm.close()
        if self._game_instance is not None:
            self._game_instance.kill()  # just making sure the game is closed

    def _hard_reset(self):
        """Reset the entire environment, closing the socket connections and the game. The next `reset()` call will recreate these resources"""
        self.close()

        self.comm = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.comm.settimeout(self.COMM_TIMEOUT)

        self.opponent_comm = (
            socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            if self.opponent is not None
            else None
        )
        if self.opponent_comm is not None:
            self.opponent_comm.settimeout(self.COMM_TIMEOUT)

        self._connected = False
        self._opponent_connected = False
        self._game_instance = None

    def set_opponent(self, opponent: Callable[[dict], Tuple[bool, bool, bool]]) -> bool:
        """
        Set the agent's opponent to the specified custom policy, or `None` if the default environment opponent should be used.
        Returns whether the environment requires calling `reset(...)` after calling this method.

        WARNING: will cause a hard reset on the environment if changing between the environment's AI and the custom opponent, closing the socket connections and the game!
        There is no hard reset if merely changing custom opponent policies.
        """
        require_hard_reset = (opponent is not None and self.opponent is None) or (
            opponent is None and self.opponent is not None
        )

        # This internal variable needs to be updated before hard-resetting
        self.opponent = opponent

        # TODO: ideally this shouldn't be required, makes changing between the in-game and custom opponent costly
        if require_hard_reset:
            self._hard_reset()
        
        return require_hard_reset


if __name__ == "__main__":
    env = FootsiesEnv(
        game_path="Build/FOOTSIES.x86_64",
        render_mode="human",
        vs_player=True,
        fast_forward=True,
        log_file="out.log",
        log_file_overwrite=True,
        frame_delay=0,
    )

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
                # action_to_string = lambda t: " ".join(("O" if a else " ") for a in t)
                # print(f"P1: {action_to_string(info['p1_action']):} | P2: {action_to_string(info['p2_action'])}")
            episode_counter += 1

    except KeyboardInterrupt:
        print("Training manually interrupted by the keyboard")

    except FootsiesGameClosedError as err:
        print(
            f"Training interrupted due to the game connection being lost: {err}"
        )

    finally:
        env.close()
