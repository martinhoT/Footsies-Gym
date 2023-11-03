class FootsiesState:
    """The full state of the FOOTSIES environment, obtained directly from the game"""

    p1_vital: int
    p2_vital: int
    p1_guard: int
    p2_guard: int
    p1_move: int
    p1_move_frame: int
    p2_move: int
    p2_move_frame: int
    p1_position: float
    p2_position: float
    global_frame: int
    p1_most_recent_action: tuple[bool, bool, bool]
    p2_most_recent_action: tuple[bool, bool, bool]

    # accept camel-case attributes
    def __init__(
        self,
        p1Vital,
        p2Vital,
        p1Guard,
        p2Guard,
        p1Move,
        p1MoveFrame,
        p2Move,
        p2MoveFrame,
        p1Position,
        p2Position,
        globalFrame,
        p1MostRecentAction,
        p2MostRecentAction,
    ):
        self.p1_vital = p1Vital
        self.p2_vital = p2Vital
        self.p1_guard = p1Guard
        self.p2_guard = p2Guard
        self.p1_move = p1Move
        self.p1_move_frame = p1MoveFrame
        self.p2_move = p2Move
        self.p2_move_frame = p2MoveFrame
        self.p1_position = p1Position
        self.p2_position = p2Position
        self.global_frame = globalFrame
        self.p1_most_recent_action = (
            (p1MostRecentAction & 1) != 0,
            (p1MostRecentAction & 2) != 0,
            (p1MostRecentAction & 4) != 0,
        )
        self.p2_most_recent_action = (
            (p2MostRecentAction & 1) != 0,
            (p2MostRecentAction & 2) != 0,
            (p2MostRecentAction & 4) != 0,
        )

    def __str__(self):
        """Detailed representation of the environment state"""
        return f"""[P1]:
- Vital: {self.p1_vital}
- Guard: {self.p1_guard}
- Move: {self.p1_move}
- Move frame: {self.p1_move_frame}
- Position: {self.p1_position}
[P2]:
- Vital: {self.p2_vital}
- Guard: {self.p2_guard}
- Move: {self.p2_move}
- Move frame: {self.p2_move_frame}
- Position: {self.p2_position}
[Info]:
- Frame: {self.global_frame}
- P1 most recent action: {self.p1_most_recent_action}
- P2 most recent action: {self.p2_most_recent_action}"""
