import socket
import json
import subprocess
import gymnasium as gym
from time import sleep

class FootsiesEnv(gym.Env):
    metadata = {"render_modes": "human", "render_fps": 60}

    def __init__(self, render_mode: str = None, game_path: str = "./Build/FOOTSIES", game_address: str = "localhost", game_port: int = 11000):
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

    def _instantiate_game(self):
        """
        Start the FOOTSIES process in the background, with the specified render mode.
        No-op if already instantiated.
        """
        if self._game_instance is None:
            # TODO: specify address and port in these arguments
            # TODO: allow connecting to an already created instance
            args = [self.game_path, "-logFile", "-", "--mute", "--training"]
            if self.render_mode is None:
                args.extend(["-batchmode", "-nographics"])
            self._game_instance = subprocess.Popen(args, stdout=subprocess.PIPE)

    def _connect_to_game(self, retry_delay: float = 0.5):
        """
        Connect to the FOOTSIES instance specified by the environment's address and port.
        If the connection is refused, wait `retry_delay` seconds before trying again.
        No-op if already connected.
        """
        while not self._connected:
            try:
                self.comm.connect((self.game_address, self.game_port))
                self._connected = True

            except ConnectionRefusedError:
                sleep(retry_delay) # avoid constantly pestering the game for a connection
                continue

    def _get_obs(self):
        pass

    def reset(self):
        self._instantiate_game()
        self._connect_to_game()