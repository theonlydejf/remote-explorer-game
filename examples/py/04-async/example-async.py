"""
 Example: Asynchronous movement and polling

 - Starts a move asynchronously and continues doing other work.
 - Polls the async handle until the move finishes (non-blocking pattern).
 - Reads the final movement result (success + survival) once ready.

 - The goal is to demonstrate how to start actions without waiting,
   keep the app responsive, and handle results when they arrive.
"""

import time
from remote_explorer_game import (
    RemoteGameSessionFactory,
    SessionIdentifier,
    VisualSessionIdentifier,
    Color,
)


def main() -> None:
    # Connect to the server and create one agent
    factory = RemoteGameSessionFactory("http://127.0.0.1:8080/", "Example")
    session = factory.create(VisualSessionIdentifier("[]", Color.Magenta))

    print("Movement started...")

    # Ask the agent to move, but do it asynchronously.
    # Our library returns an AsyncMovementResult handle immediately,
    # and completes it in the background.
    handle = session.move_async((1, 0))

    # We can keep doing something else while the move is still in progress
    print("Doing something else while the movement completes...")

    # Poll until finished (non-blocking sleep)
    while not handle.ready:
        print("Waiting for the movement to finish...")
        time.sleep(0.02)

    # Now that the move is finished, read the result
    result = handle.movement_result
    if result is None:
        print("No result available (request may have failed).")
        return

    is_alive = result.is_agent_alive        # Did the agent survive?
    move_successful = result.moved_successfully  # Did the move succeed?
    print(f"Alive: {is_alive}, Move was successful: {move_successful}")

if __name__ == "__main__":
    main()
