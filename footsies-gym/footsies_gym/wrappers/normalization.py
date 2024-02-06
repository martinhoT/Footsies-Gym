import gymnasium as gym
from gymnasium import spaces
from ..moves import FootsiesMove, FOOTSIES_MOVE_INDEX_TO_MOVE


class FootsiesNormalized(gym.ObservationWrapper):
    """Normalizes all observation space variables. Wrapper should be applied to the base FOOTSIES environment before any other observation wrapper

    Move frame durations will be between `0` and `1`, inclusive. `0` indicates the start of the move, while `1` indicates the end of it
    """

    def __init__(self, env):
        super().__init__(env)
        relevant_moves = set(FootsiesMove) - {FootsiesMove.WIN, FootsiesMove.DEAD}

        self.observation_space = spaces.Dict(
            {
                "guard": spaces.Box(low=0.0, high=1.0, shape=(2,)),
                "move": spaces.MultiDiscrete(
                    [len(relevant_moves), len(relevant_moves)]
                ),
                "move_frame": spaces.Box(low=0.0, high=1.0, shape=(2,)),
                "position": spaces.Box(low=-1.0, high=1.0, shape=(2,)),
            }
        )

    def observation(self, obs: dict):
        obs = obs.copy()
        obs["guard"] = (obs["guard"][0] / 3.0, obs["guard"][1] / 3.0)
        obs["position"] = (obs["position"][0] / 4.4, obs["position"][1] / 4.4)
        obs["move_frame"] = (
            obs["move_frame"][0]
            / FOOTSIES_MOVE_INDEX_TO_MOVE[int(obs["move"][0])].value.duration,
            obs["move_frame"][1]
            / FOOTSIES_MOVE_INDEX_TO_MOVE[int(obs["move"][1])].value.duration
        )

        return obs

    @staticmethod
    def undo(obs):
        obs = obs.copy()
        obs["guard"] = (obs["guard"][0] * 3.0, obs["guard"][1] * 3.0)
        obs["position"] = (obs["position"][0] * 4.4, obs["position"][1] * 4.4)
        obs["move_frame"] = (
            obs["move_frame"][0]
            * FOOTSIES_MOVE_INDEX_TO_MOVE[int(obs["move"][0])].value.duration,
            obs["move_frame"][1]
            * FOOTSIES_MOVE_INDEX_TO_MOVE[int(obs["move"][1])].value.duration
        )

        return obs
