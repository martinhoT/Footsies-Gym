import socket
import json
from dataclasses import dataclass

s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
s.connect(("localhost", 11000))

@dataclass
class EnvironmentState:
    p1_vital:       int
    p2_vital:       int
    p1_guard:       int
    p2_guard:       int
    p1_move:        int
    p1_move_frame:  int
    p2_move:        int
    p2_move_frame:  int
    p1_position:    int
    p2_position:    int

    # accept camel-case attributes
    def __init__(self, p1Vital, p2Vital, p1Guard, p2Guard, p1Move, p1MoveFrame, p2Move, p2MoveFrame, p1Position, p2Position):
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

def step(action: tuple[bool]):
    action_message = bytearray(action)
    print("Sending action message...", end=" ")
    s.send(action_message)
    print("sent!")
    print("Receiving next state...", end=" ")
    next_state_json = s.recv(4096).decode("utf-8")
    next_state = EnvironmentState(**json.loads(next_state_json))
    print(f"received! ({next_state})")

    return next_state, next_state.p1_vital == 0 or next_state.p2_vital == 0, False, {}

def reset():
    print("Environment reset! Receiving initial state...", end=" ")
    state_json = s.recv(4096).decode("utf-8")
    state = EnvironmentState(**json.loads(state_json))
    print(f"received! ({state})")
    return state, {}


try:
    while True:
        terminated = False
        state, info = reset()
        while not terminated:
            ipt = input("Action: ")
            next_state, terminated, truncated, info = step(((key in ipt) for key in ["a", "d", " "]))
            

except KeyboardInterrupt:
    s.close()