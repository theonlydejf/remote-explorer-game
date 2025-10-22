# Getting Started

## Prerequisites
- **Operating system**: Windows, macOS, or Linux with console access.
- **.NET SDK**: version 7.0 or newer for building `lesson-exec` and C# clients.
- **Python**: 3.9+ with `pip` for the Python client library.
- **Git & build tools**: required if you plan to modify the sources.
- **Console sizing**: the visualizer expects enough columns/rows to render the map (see `lesson-exec` startup check).

## Install the Client Libraries
- **C# / .NET (NuGet)**
  ```bash
  dotnet add package RemoteExplorerGame
  ```
  Target your project. Use a local project reference when contributing to the repo.
- **Python (PyPI)**
  ```bash
  pip install remote-explorer-game
  ```
  Prefer virtual environments for isolation. Switch to `pip install -e .` when developing against the local sources.

## Launch the Bundled Server (`lesson-exec`)
1. Download the latest release archive for your platform from the [GitHub releases page](https://github.com/theonlydejf/remote-explorer-game/releases/latest) (`lesson-exec-windows-x64.zip`, `lesson-exec-linux-x64.zip`, etc.).
2. Extract the archive to a convenient location.
3. Run the executable:
   - Windows: `lesson-exec.exe`
   - macOS/Linux: `./lesson-exec`
4. Confirm the console visualizer appears, the Test World listens on port 8080, and additional challenge worlds (8081+) start headless.

> [!TIP]
> You can modify the `lesson-exec` and build from source (`dotnet run --project lesson-exec`) if needed.

## Walkthrough: First Agent
- **C# snippet**
  ```csharp
  using ExplorerGame.Net;
  using ExplorerGame.Core;

  var factory = new RemoteGameSessionFactory("http://127.0.0.1:8080/", "Student");
  var session = factory.Create(new SessionIdentifier("[]", ConsoleColor.Magenta));

  while (session.IsAgentAlive)
      session.Move(new Vector(1, 0));
  ```
- **Python snippet**
  ```python
  from remote_explorer_game import RemoteGameSessionFactory, VisualSessionIdentifier, Color

  factory = RemoteGameSessionFactory("http://127.0.0.1:8080/", "Student")
  session = factory.create(VisualSessionIdentifier("[]", Color.Magenta))

  while session.is_agent_alive:
      session.move((1, 0))
  ```
- Run the scripts, observe the agent marker in the Test World, and confirm logs appear in the console.

## Next Steps
- Explore [`examples`](https://github.com/theonlydejf/remote-explorer-game/blob/main/examples) to expand on the quickstart loop.
- Work through the feedback, multi-agent, and async examples in **Tutorials & Examples**.
- Read **Server Setup** to learn about `lesson-exec` configuration and custom host creation.
- Attempt challenge worlds (8081+) once comfortable with agent orchestration and failure handling.
