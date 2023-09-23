import socket
import json
import subprocess
import gymnasium as gym
from time import sleep
from gymnasium import spaces
from state import FootsiesState
from moves import FootsiesMove

# TODO: move training agent input reading (through socket comms) to Update() instead of FixedUpdate()
# TODO: allow unlimited framerate
# TODO: decouple training from the debug pause mode (which should not be allowed)
# TODO: close game when socket is closed

MAX_STATE_MESSAGE_BYTES = 4096


class FootsiesEnv(gym.Env):
    metadata = {"render_modes": "human", "render_fps": 60}

    def __init__(
        self,
        render_mode: str = None,
        game_path: str = "./Build/FOOTSIES",
        game_address: str = "localhost",
        game_port: int = 11000,
    ):
        """
        FOOTSIES training environment

        Parameters
        ----------
        render_mode: str
            how should the environment be rendered
        game_path: str
            path to the FOOTSIES executable. Preferably to be a fully qualified path
        game_address: str
            address of the FOOTSIES instance
        game_port: int
            port of the FOOTSIES instance
        """
        self.game_path = game_path
        self.game_address = game_address
        self.game_port = game_port

        assert render_mode is None or render_mode in self.metadata["render_modes"]
        self.render_mode = render_mode

        self.comm = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self._game_instance = None
        self._connected = False

        # Don't consider the end-of-round moves
        relevant_moves = set(FootsiesMove) - {FootsiesMove.WIN, FootsiesMove.DEAD}

        # The observation space is divided into 2 columns, the first for player 1 and the second for player 2
        self.observation_space = spaces.Dict(
            {
                "guard": spaces.MultiDiscrete([4, 4]),  # 0..3
                "move": spaces.MultiDiscrete(
                    [len(relevant_moves), len(relevant_moves)]
                ),
                "move_frame": spaces.MultiDiscrete(
                    [55, 55]
                ),  # the maximum number of frames a move can have (excluding WIN and DEAD)
                "position": spaces.Box(low=-4.4, high=4.4, shape=(2,)),
            }
        )

        # 3 actions, which can be combined: left, right, attack
        self.action_space = spaces.MultiBinary(3)

        # -1 for losing, 1 for winning, 0 otherwise
        self.reward_range = (-1, 1)

        self._current_state = None
        self._outdated_state = True

    def _instantiate_game(self):
        """
        Start the FOOTSIES process in the background, with the specified render mode.
        No-op if already instantiated
        """
        if self._game_instance is None:
            args = [self.game_path, "-logFile", "-", "--mute", "--training" , "--address", self.game_address, "--port", self.game_port]
            if self.render_mode is None:
                args.extend(["-batchmode", "-nographics"])
            self._game_instance = subprocess.Popen(args, stdout=subprocess.PIPE)

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

    def _receive_state(self) -> FootsiesState:
        """
        Receive the environment state from the FOOTSIES instance.
        No-op if already received the state in the current step
        """
        if self._outdated_state:
            state_json = self.comm.recv(MAX_STATE_MESSAGE_BYTES).decode("utf-8")
            self._current_state = FootsiesState(**json.loads(state_json))
            self._outdated_state = False

        return self._current_state

    def _send_action(self):
        """Send an action to FOOTSIES instance"""

    def _get_obs(self) -> dict:
        """
        Get the current observation from the FOOTSIES instance.
        The observation is a filtered version of the full state, for use by the training agent
        """
        state = self._receive_state()
        return {
            "guard": [state.p1_guard, state.p2_guard],
            "move": [state.p1_move, state.p2_move],
            "move_frame": [state.p1_move_frame, state.p2_move_frame],
            "position": [state.p1_position, state.p2_position],
        }

    def _get_info(self) -> dict:
        """Get the current additional info from the FOOTSIES instance"""
        state = self._receive_state()
        return {"frame": state.global_frame}

    def reset(self) -> "tuple[dict, dict]":
        self._instantiate_game()
        self._connect_to_game()

        return self._get_obs(), self._get_info()

    def step(
        self, action: "tuple[bool, bool, bool]"
    ) -> "tuple[dict, float, bool, bool, dict]":
        # Send action
        action_message = bytearray(action)
        self.comm.send(action_message)

        # Flag the current state as being outdated so that we can receive a new one
        self._outdated_state = True
        state = self._receive_state()

        # Get next observation, info and reward
        obs = self._get_obs()
        info = self._get_info()

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
    env = FootsiesEnv(game_path="../../Build/FOOTSIES", render_mode="human")

    try:
        while True:
            terminated = False
            observation, info = env.reset()
            print("New episode!")
            while not terminated:
                action = env.action_space.sample()
                next_observation, reward, terminated, truncated, info = env.step(action)

    except KeyboardInterrupt:
        env.close()
