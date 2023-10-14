from enum import Enum

class FootsiesMove(Enum):
    STAND = 0
    FORWARD = 1
    BACKWARD = 2
    DASH_FORWARD = 10
    DASH_BACKWARD = 11
    N_ATTACK = 100
    B_ATTACK = 105
    N_SPECIAL = 110
    B_SPECIAL = 115
    DAMAGE = 200
    GUARD_M = 301
    GUARD_STAND = 305
    GUARD_CROUCH = 306
    GUARD_BREAK = 310
    GUARD_PROXIMITY = 350
    DEAD = 500
    WIN = 510

discretized_footsies_moves = {move.name: i for i, move in enumerate(FootsiesMove)}
