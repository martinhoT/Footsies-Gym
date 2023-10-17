import gymnasium as gym
from gymnasium import spaces
from ..moves import FootsiesMove, footsies_move_index_to_move

class FootsiesMoveFrameNormalized(gym.ObservationWrapper):
    def __init__(self, env):
        super().__init__(env)
        relevant_moves = set(FootsiesMove) - {FootsiesMove.WIN, FootsiesMove.DEAD}
        self.observation_space = spaces.Dict(
            {
                "guard": spaces.MultiDiscrete([4, 4]),  # 0..3
                "move": spaces.MultiDiscrete(
                    [len(relevant_moves), len(relevant_moves)]
                ),
                "move_frame": spaces.Box(low=0.0, high=1.0, shape=(2,)),
                "position": spaces.Box(low=-4.4, high=4.4, shape=(2,)),
            }
        )

    def observation(self, obs):
        obs["move_frame"] = obs["move_frame"] / footsies_move_index_to_move[obs["move"]].value.duration
        return obs
