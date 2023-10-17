from enum import Enum
from collections import namedtuple

FootsiesMoveInfo = namedtuple("FootsiesMoveInfo", ["id", "duration"])

class FootsiesMove(Enum):
    STAND = FootsiesMoveInfo(0, 24)
    FORWARD = FootsiesMoveInfo(1, 24)
    BACKWARD = FootsiesMoveInfo(2, 24)
    DASH_FORWARD = FootsiesMoveInfo(10, 16)
    DASH_BACKWARD = FootsiesMoveInfo(11, 22)
    N_ATTACK = FootsiesMoveInfo(100, 22)
    B_ATTACK = FootsiesMoveInfo(105, 21)
    N_SPECIAL = FootsiesMoveInfo(110, 44)
    B_SPECIAL = FootsiesMoveInfo(115, 55)
    DAMAGE = FootsiesMoveInfo(200, 17)
    GUARD_M = FootsiesMoveInfo(301, 23)
    GUARD_STAND = FootsiesMoveInfo(305, 15)
    GUARD_CROUCH = FootsiesMoveInfo(306, 15)
    GUARD_BREAK = FootsiesMoveInfo(310, 36)
    GUARD_PROXIMITY = FootsiesMoveInfo(350, 1)
    DEAD = FootsiesMoveInfo(500, 500)
    WIN = FootsiesMoveInfo(510, 33)

# Helper structures to simplify move IDs (0, 1, 2, ...)
footsies_move_index_to_move = list(FootsiesMove)
footsies_move_id_to_index = {move.value.id: i for i, move in enumerate(footsies_move_index_to_move)}
