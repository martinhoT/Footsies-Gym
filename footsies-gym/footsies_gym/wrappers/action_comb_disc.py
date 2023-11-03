import gymnasium as gym
from gymnasium import spaces


class FootsiesActionCombinationsDiscretized(gym.ActionWrapper):
    """
    Discretizes the FOOTSIES actions, which are a tuple of three boolean values, into a single integer representing all possible combinations of those boolean values

    For an action represented by an integer, the respective tuple is equal to its first (rightmost) 3 bits
    """

    def __init__(self, env):
        super().__init__(env)
        self.action_space = spaces.Discrete(2**3)

    def action(self, act):
        return ((act & 4) != 0, (act & 2) != 0, (act & 1) != 0)
