# Remote Explorer Game
*An educational client–server exploration game for learning algorithms, libraries, and async programming.*

## What is it
Remote Explorer Game is an educational tool for learning programming, algorithms, and client–server interaction.

Students connect their own client applications (in C# or Python) to a remote game server and control agents exploring a hidden 2D map. Each agent starts at a fixed position and can move step by step until it encounters a trap and dies. Agents that remain idle for too long are automatically killed. Students can then spawn a new agent to continue exploration.

The goal is to uncover as much of the map as possible and ideally locate all traps. Multiple agents can be controlled at the same time, which introduces challenges of coordination and asynchronous programming.

## What you’ll practice
- Using published libraries (NuGet for .NET, PyPI for Python)
- Graph search algorithms (BFS, Dijkstra, A*)
- Asynchronous programming and coordinating multiple agents
- Building clients that interact with a remote server

## How it works
- Agents spawn at a fixed start location.
- Stepping on a trap kills the agent.
- Staying idle for too long kills the agent (idle kill).
- Each client has a limit on how many agents may be alive simultaneously.
- You need a running server (local or remote) to connect to.

## Worlds
- **Test World** — visualized map that shows where agents are (great for demos/debugging).
- **Challenge Worlds** — one or more headless worlds on separate ports for labs/assignments.
  - To create or configure challenge worlds, see **docs/SERVER_SETUP.md**.

## Quick start
1) **Install the client library**
   - .NET: `dotnet add package ExplorerGame`
   - Python: `pip install remote-explorer-game`

2) **Start a server**
   - Launch the server with **lesson-exec** (compile or download the executable).
   - For details, ports, and configuration, see **docs/SERVER_SETUP.md**.

3) **Connect an agent (tiny examples)**

**C#**
```csharp
using ExplorerGame.Net;
using ExplorerGame.Core;

var factory = new RemoteGameSessionFactory("http://127.0.0.1:8080/", "Your name");
var session = factory.Create(new SessionIdentifier("[]", ConsoleColor.Magenta));

while (session.IsAgentAlive)
    session.Move(new Vector(1, 0));
```

**Python**
```python
from remote_explorer_game import *
factory = RemoteGameSessionFactory("http://127.0.0.1:8080/", "Example")
session = factory.create(VisualSessionIdentifier("[]", Color.Magenta))

while session.is_agent_alive:
    session.move((1, 0))
```

4) **Learn by doing**
   - Explore the [`examples/`](https://github.com/theonlydejf/remote-explorer-game/tree/main/examples) directory for runnable scenarios.
   - Check the [API references](https://theonlydejf.github.io/remote-explorer-game/api/ExplorerGame.Net.html) for deeper details.

## For instructors / teachers
Use this as a hands-on teaching aid for programming, algorithms, and distributed systems.
- Easy setup: run one shared server; students only install a client library.
- Two languages: C#/.NET and Python.
- Concepts covered: BFS/Dijkstra/A*, async programming, client–server interaction, and working with real package ecosystems (NuGet/PyPI).
- Assessment ideas: map coverage %, number of deaths, time-to-coverage goals, code clarity and testing.
