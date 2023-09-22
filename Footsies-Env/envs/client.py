import socket
import json

s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
s.connect(("localhost", 11000))


class EnvironmentState:
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

    def observation(self):
        return {k: v for k, v in self.__dict__.items() if k != "global_frame"}

    def info(self):
        return {"frame": self.global_frame}

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
- Frame: {self.global_frame}"""


def step(action: tuple[bool]):
    action_message = bytearray(action)
    print("Sending action message...", end=" ")
    s.send(action_message)
    print("sent!")
    print("Receiving next state...", end=" ")
    next_state_json = s.recv(4096).decode("utf-8")
    next_state = EnvironmentState(**json.loads(next_state_json))
    print(f"received! ({next_state})")

    return (
        next_state.observation(),
        next_state.p1_vital == 0 or next_state.p2_vital == 0,
        False,
        next_state.info(),
    )


def reset():
    print("Environment reset! Receiving initial state...", end=" ")
    state_json = s.recv(4096).decode("utf-8")
    state = EnvironmentState(**json.loads(state_json))
    print(f"received! ({state})")
    return state.observation(), state.info()

# TODO: move training agent input reading (through socket comms) to Update() instead of FixedUpdate()
# TODO: allow unlimited framerate
# TODO: mouse interaction in main menu (extremely low priority)
# TODO: remove intro and outro flair for efficient training
# TODO: decouple training from the debug pause mode (should NOT be allowed)
# TODO: close game when socket is closed

try:
    while True:
        terminated = False
        observation, info = reset()
        while not terminated:
            ipt = input("Action: ")
            next_observation, terminated, truncated, info = step(
                ((key in ipt) for key in ["a", "d", " "])
            )


except KeyboardInterrupt:
    s.close()
