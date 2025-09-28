"""
 Example: Asynchronous movement with multiple agents

 - Starts several agents at once and makes them move asynchronously.
 - Each agent picks a random direction from a list (right and up are more likely).
 - Continuously checks if each agent has finished its move.
 - If an agent dies, it is instantly recreated so the group size stays the same.
 - The result is a swarm of agents moving around at the same time without waiting
   for one another.

 - The goal is to show how to coordinate many agents in parallel using async moves.
"""

import random
from remote_explorer_game import (
    RemoteGameSessionFactory,
    RemoteGameSession,
    SessionIdentifier,
    VisualSessionIdentifier,
    MovementResult,
    Color,
)

ADDRESS = "http://127.0.0.1:8080/"
USERNAME = "Example"
AGENT_CNT = 5

# Weighted moves: right and up appear 3x to be more likely
MOVES = [
    (1, 0), (1, 0), (1, 0),   # right
    (0, 1), (0, 1), (0, 1),   # up
    (-1, 0),                  # left
    (0, -1),                  # down
]


def create_agent(factory: RemoteGameSessionFactory, idx: int) -> RemoteGameSession:
    """
    Create an agent with a unique two-character identifier like "[0", "[1", ...
    """
    vsid = VisualSessionIdentifier(f"[{idx}", Color.Magenta)
    return factory.create(SessionIdentifier(vsid))


def main() -> None:
    factory = RemoteGameSessionFactory(ADDRESS, USERNAME)

    # Create initial agents and their first async moves
    sessions: list[RemoteGameSession] = [create_agent(factory, i) for i in range(AGENT_CNT)]
    handles = []
    for i in range(AGENT_CNT):
        move = random.choice(MOVES)
        handle = sessions[i].move_async(move)  # returns immediately; no event loop needed
        handles.append(handle)

    try:
        while True:
            # Check each agent's async handle
            for i in range(AGENT_CNT):
                handle = handles[i]
                if not handle.ready:
                    continue  # still moving

                result: MovementResult | None = handle.movement_result
                # If agent died, recreate it
                if result is None or not result.is_agent_alive:
                    sessions[i] = create_agent(factory, i)

                # Start a new async move for this agent
                next_move = random.choice(MOVES)
                handles[i] = sessions[i].move_async(next_move)

    except KeyboardInterrupt:
        print("Stopped by user.")


if __name__ == "__main__":
    main()
