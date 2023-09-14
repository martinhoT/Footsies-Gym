import socket

s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)

s.connect(("localhost", 11000))

data = s.recv(4096)

print("Initial game state:", data.decode('utf-8'))

try:
    while True:
        ipt = input("Action: ")

        action_message = bytes.fromhex(
            "".join(("FF" if key in ipt else "00") for key in ["a", "d", " "])
        )

        print("Sending action message...", end=" ")
        s.send(action_message)
        print("sent!")
        print("Receiving next state...", end=" ")
        next_state = s.recv(4096)
        print(f"received! ({next_state.decode('utf-8')})")

except KeyboardInterrupt:
    s.close()