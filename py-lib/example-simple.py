import time
from remote_explorer_game import RemoteGameSessionFactory, RemoteGameSession, SessionIdentifier, Color


def move_n_times(n: int, movement: tuple[int, int], session: RemoteGameSession):
    """
    Move the agent n times in the specified direction with a delay between moves.
    """
    for i in range(n):
        result = session.move(movement)
        print(f"Move #{i+1}/{n} - delta={movement}: "
              f"moved_successfully={result.moved_successfully}, "
              f"agent_alive={result.is_agent_alive}")

def main():
    # Server configuration
    address = "http://127.0.0.1:8080"
    username = "Py-Example"
    factory = RemoteGameSessionFactory(address, username=username)

    # Create a unique session identifier (icon and color)
    icon = "[]"
    session_color = Color.Magenta
    identifier = SessionIdentifier(icon, session_color)

    # Connect to the remote game session
    session = factory.create(identifier)

    session.move((1, 0))  # Move right
    time.sleep(.5)
    session.move((-1, 0)) # Move left
    time.sleep(.5)
    session.move((0, 1))  # Move up
    time.sleep(.5)
    session.move((0, -1)) # Move down
    time.sleep(.5)

    session.move((2, 0))  # Jump right
    time.sleep(.5)

    # Walk in an infinite square pattern
    print("Starting infinite square loop...")
    while True:
        move_n_times(4, (1, 0), session)   # right
        move_n_times(4, (0, 1), session)   # up
        move_n_times(4, (-1, 0), session)  # left
        move_n_times(4, (0, -1), session)  # down


if __name__ == "__main__":
    main()
