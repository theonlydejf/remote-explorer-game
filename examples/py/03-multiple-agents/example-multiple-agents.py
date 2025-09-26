"""
 Example: Controlling multiple agents

 - Shows how to manage several agents at the same time.
 - Agents move in random directions, with a higher chance 
   of going right or up.
 - Illustrates automatic recovery: when an agent dies, 
   a new one is created instantly.
 - The result looks like a swarm of agents wandering the map 
   without stopping.

 - The goal is to demonstrate how to coordinate multiple 
   sessions, use randomness, and keep agents running even 
   after failure.
"""

import time
import random
from remote_explorer_game import (
    RemoteGameSessionFactory,
    SessionIdentifier,
    VisualSessionIdentifier,
    Color,
    RemoteGameSession,
    MovementResult,
)

ADDRESS = "http://127.0.0.1:8080/"
USERNAME = "Example"
AGENT_CNT = 5

# Weighted movement options:
# (1, 0) and (0, 1) appear 3x each to be more likely.
MOVES = [
    (1, 0), (1, 0), (1, 0),   # right (higher chance)
    (0, 1), (0, 1), (0, 1),   # up (higher chance)
    (-1, 0),                  # left
    (0, -1),                  # down
]

def create_agent(factory: RemoteGameSessionFactory, idx: int) -> RemoteGameSession:
    """
    Create a single agent with a unique two-character identifier.
    Uses '[0', '[1', ... which are two chars long.
    """
    vsid = VisualSessionIdentifier(f"[{idx}", Color.Magenta)
    return factory.create(SessionIdentifier(vsid))

def main() -> None:
    factory = RemoteGameSessionFactory(ADDRESS, USERNAME)

    # Create agents and store their sessions
    sessions: list[RemoteGameSession] = [create_agent(factory, i) for i in range(AGENT_CNT)]

    try:
        while True:
            # For each agent, perform one random move
            for i, session in enumerate(sessions):
                move = random.choice(MOVES)
                result: MovementResult = session.move(move)

                # If the agent died during movement, create a new one immediately
                if not result.is_agent_alive:
                    sessions[i] = create_agent(factory, i)

    except KeyboardInterrupt:
        print("Stopped by user.")


if __name__ == "__main__":
    main()
