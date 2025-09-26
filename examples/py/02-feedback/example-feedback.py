"""
 Example: Feedback and error handling

 - Connects to a single agent and checks if it is alive.
 - Demonstrates how each move returns feedback about success 
   and survival.
 - Covers different cases: invalid moves, collisions, 
   or death.
 ADVANCED SECTION:
 - Shows how to handle server messages when something goes 
   wrong (like inactivity timeouts).

 - The goal is to teach how to read movement results and 
   respond to errors in the game.The goal is to teach how to read movement results and respond to errors in the game.
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

    # The simplest way to check if the agent is alive
    # Session automatically updates this value after each action
    if session.is_agent_alive:
        print("Agent is alive")
    else:
        print("Agent is not alive")

    # Each call to session.move(...) returns information about the result
    result = session.move((1, 0))  # Everything succeeds (example)

    # Invalid move => unsuccessful move but survives (uncomment to try)
    # result = session.move((6, -9))

    # Agent hits a top wall => successful move but does not survive (uncomment to try)
    # result = session.move((0, -1))

    print(f"alive={result.is_agent_alive}, moved={result.moved_successfully}")

    if result.is_agent_alive:
        print("Agent survived the move")
    else:
        print("Agent did not survive the move")

    if result.moved_successfully:
        print("Move was successful")
    else:
        # Can happen if trying to communicate with an agent that is already dead,
        # or performing an invalid move (e.g., (10, -68))
        print("Move was not successful")

    # Each movement discovers a tile from the map.
    # Tile is None if the tile is empty, the agent wandered out of the map,
    # or if the movement was not successful.
    discovered_tile = result.discovered_tile
    if discovered_tile is not None:
        print("Discovered tile:", str(discovered_tile))

    # Session keeps track of last discovered tile (like session.is_agent_alive)
    if session.discovered_tile is not None:
        print("Session's last discovered tile:", str(session.discovered_tile))

    ######### ADVANCED #########
    print("Waiting...")
    time.sleep(6.1)  # Wait until the server might kick the agent for inactivity
    kicked_move_result = session.move((1, 0))
    print(f"Alive: {kicked_move_result.is_agent_alive}, Move was successful: {kicked_move_result.moved_successfully}")

    # If you don't know why the agent suddenly died and you can't move it anymore,
    # the server can send back a message explaining why the move failed.
    if session.last_response_message is not None:
        print("Message:", session.last_response_message)
    else:
        print("No message received")


if __name__ == "__main__":
    main()
