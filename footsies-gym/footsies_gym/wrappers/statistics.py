import gymnasium as gym
from ..moves import footsies_move_index_to_move, FootsiesMove

class FootsiesStatistics(gym.Wrapper):
    """Collect statistics on the FOOTSIES environment. The environment that this wrapper receives should not have flattened observations"""
    def __init__(self, env):
        super().__init__(env)
        self._special_moves_per_episode = []
        self._special_moves_per_episode_counter = 0
        self._prev_p1_move = None # use to make sure special moves are only counted when they are performed, and not every time step they are active

    def _get_p1_move(self, obs) -> FootsiesMove:
        p1_move_index = obs["move"][0]
        return footsies_move_index_to_move[p1_move_index]

    def reset(self, *, seed: int = None, options: dict = None):
        obs, info = self.env.reset(seed=seed, options=options)
        self._prev_p1_move = self._get_p1_move(obs)
        
        return obs, info

    def step(self, action):
        next_obs, reward, terminated, truncated, info = self.env.step(action)

        p1_move = self._get_p1_move(next_obs)
        if self._prev_p1_move != p1_move and p1_move in {FootsiesMove.B_SPECIAL, FootsiesMove.N_SPECIAL}:
            self._special_moves_per_episode_counter += 1
        self._prev_p1_move = p1_move

        if terminated or truncated:
            self._special_moves_per_episode.append(self._special_moves_per_episode_counter)
            self._special_moves_per_episode_counter = 0
        
        return next_obs, reward, terminated, truncated, info

    @property
    def metric_special_moves_per_episode(self):
        return self._special_moves_per_episode