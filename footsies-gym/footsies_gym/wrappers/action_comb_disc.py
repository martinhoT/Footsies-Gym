import gymnasium as gym
from gymnasium import spaces

class FootsiesActionCombinationsDiscretized(gym.ActionWrapper):
    def __init__(self, env):
        super().__init__()
        self.action_space = spaces.Discrete(2**3) 
    
    def action(self, act):
        return ((act & 4) != 0, (act & 2) != 0, (act & 1) != 0)
