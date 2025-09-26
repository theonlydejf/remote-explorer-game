"""
 Example: Simple movement of an agent

 - Connects to the server and creates a single agent.
 - Assigns the agent a visual identifier (icon + color).
 - Demonstrates basic moves: right, left, up, down, and a jump.
 - Then loops forever, making the agent walk in a square.
 
 - The goal is to show how to connect, control movement, 
   and run continuous actions.
"""

import time
from typing import Tuple

from remote_explorer_game import (
    RemoteGameSessionFactory, RemoteGameSession,
    SessionIdentifier, VisualSessionIdentifier, Color,
)


def move_n_times(n: int, movement: Tuple[int, int], session: RemoteGameSession) -> None:
    """Move the agent `n` times by the same vector, with a small pause between moves."""
    for _ in range(n):
        session.move(movement)


def main() -> None:
    # Prepare an object ("factory") that helps us create agents and connect to them
    address = "http://127.0.0.1:8080/"
    username = "Example"
    factory = RemoteGameSessionFactory(address, username)

    # Prepare a graphical identifier that will be shown on the server screen
    # ! Everyone must have their own, it cannot happen that multiple people have the same one !
    img = "[]"  # two characters max
    color = Color.Magenta
    identifier = VisualSessionIdentifier(img, color)

    # Create an agent and connect to it
    session = factory.create(identifier)

    # Demonstrate basic moves (dx, dy):
    session.move((1, 0))   # Move right
    time.sleep(0.5)
    session.move((-1, 0))  # Move left
    time.sleep(0.5)
    session.move((0, 1))   # Move up
    time.sleep(0.5)
    session.move((0, -1))  # Move down
    time.sleep(0.5)

    session.move((2, 0))   # Jump right (step of 2)
    time.sleep(0.5)

    # Walk in a square forever
    try:
        while True:
            move_n_times(4, (1, 0), session)   # right ×4
            move_n_times(4, (0, 1), session)   # up ×4
            move_n_times(4, (-1, 0), session)  # left ×4
            move_n_times(4, (0, -1), session)  # down ×4
    except KeyboardInterrupt:
        print("Stopped by user.")


if __name__ == "__main__":
    main()
