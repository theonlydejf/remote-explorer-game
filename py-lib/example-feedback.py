import time
from remote_explorer_game import RemoteGameSessionFactory, SessionIdentifier, Color

def main():
    # Connect to the agent
    factory = RemoteGameSessionFactory("http://127.0.0.1:8080/", username="Py-Example")
    session = factory.create(SessionIdentifier("[]", Color.Magenta))

    # Check if the agent is alive
    if session.is_agent_alive:
        print("Agent is alive")
    else:
        print("Agent is dead")

    # Each call to session.move(...) returns a MovementResult
    # Successful move to the right
    result = session.move((1, 0))

    # Agent hits a wall:
    # => moved_successfully=True, is_agent_alive=False
    # result = session.move((-1, 0))

    # Invalid move:
    # => moved_successfully=False, is_agent_alive=True
    # result = session.move((6, -9))

    if result.is_agent_alive:
        print("Agent survived the move")
    else:
        print("Agent did not survive the move")

    if result.moved_successfully:
        print("Move executed successfully")
    else:
        # This can happen if the agent is already dead, or the move is invalid
        print("Move failed")

    #/////////// ADVANCED ///////////

    print("Waiting...")
    time.sleep(6) # wait until the server kills the agent for inactivity
    killed_result = session.move((1, 0))
    print(f"Alive: {killed_result.is_agent_alive}, Move successful: {killed_result.moved_successfully}")

    # If you're not sure why the agent suddenly died and can't move anymore,
    # the server may include an explanatory message.

    if session.last_response_message:
        print("Server message:", session.last_response_message)

if __name__ == "__main__":
    main()
