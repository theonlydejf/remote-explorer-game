# Introduction

## Who This Project Serves
- **Students** learning algorithms, async I/O, or client–server design through a playful exploration map.
- **Instructors** who need a ready-made lab platform with scripts, examples, and extensible server tooling.
- **Tinkerers** looking to prototype exploration strategies or extend the framework with custom maps and rules.

## How the System Fits Together
- `lesson-exec` boots one or more `ConnectionHandler` instances, each bound to a map and port.
- Client libraries (C# and Python) connect via `/connect`, receive a session ID (SID), and issue `/move` commands.
- Optional `ConsoleVisualizer` renders the map, agent identifiers (VSIDs), and logs session lifecycle events.
- Challenge worlds run headless; the shared Test World exposes the visualizer for quick feedback.

## Key Terminology
- **SID** – server-generated session identifier returned on `/connect`; route all moves through it.
- **VSID** – two-character visual identifier + color; mandatory on visualized worlds to avoid collisions.
- **Tile** – two-character map cell; traps or walls end the agent’s life, empty spaces are `null`.
- **Idle timeout** – five-second inactivity window enforced by `ConnectionHandler.CleanupLoop`.
- **Challenge world** – headless server instance with hidden traps meant for assignments or competitions.

## Bundled Worlds
- **Test World (port 8080)** – visible grid where identifiers, traps, and movement appear instantly.
- **Challenge Worlds (8081+)** – maps loaded from `resources/challenges/challenge-*.png`; no visualizer, ideal for grading.
- **Custom Maps** – plug in additional PNGs or authored arrays to create alternative scenarios.

## Featured Examples
- **Simple Movement** – Python: [`examples/py/01-simple/example-simple.py`](https://github.com/theonlydejf/remote-explorer-game/blob/main/examples/py/01-simple/example-simple.py), C#: [`examples/csharp/01-simple/ExampleSimple.cs`](https://github.com/theonlydejf/remote-explorer-game/blob/main/examples/csharp/01-simple/ExampleSimple.cs).
- **Feedback & Error Handling** – Python: [`examples/py/02-feedback/example-feedback.py`](https://github.com/theonlydejf/remote-explorer-game/blob/main/examples/py/02-feedback/example-feedback.py), C#: [`examples/csharp/02-feedback/ExampleFeedback.cs`](https://github.com/theonlydejf/remote-explorer-game/blob/main/examples/csharp/02-feedback/ExampleFeedback.cs).
- **Multiple Agents** – Python: [`examples/py/03-multiple-agents/example-multiple-agents.py`](https://github.com/theonlydejf/remote-explorer-game/blob/main/examples/py/03-multiple-agents/example-multiple-agents.py), C#: [`examples/csharp/03-multiple-agents/ExampleMultipleAgents.cs`](https://github.com/theonlydejf/remote-explorer-game/blob/main/examples/csharp/03-multiple-agents/ExampleMultipleAgents.cs).
- **Async Movement** – Python: [`examples/py/04-async/example-async.py`](https://github.com/theonlydejf/remote-explorer-game/blob/main/examples/py/04-async/example-async.py), C#: [`examples/csharp/04-async/ExampleAsync.cs`](https://github.com/theonlydejf/remote-explorer-game/blob/main/examples/csharp/04-async/ExampleAsync.cs).
- **Async Multi-Agent** – Python: [`examples/py/05-async-multiple-agents/example-async-multiple-agents.py`](https://github.com/theonlydejf/remote-explorer-game/blob/main/examples/py/05-async-multiple-agents/example-async-multiple-agents.py), C#: [`examples/csharp/05-async-multiple-agents/ExampleAsyncMultipleAgents.cs`](https://github.com/theonlydejf/remote-explorer-game/blob/main/examples/csharp/05-async-multiple-agents/ExampleAsyncMultipleAgents.cs).
- **Advanced Solution** – C#: [`examples/csharp/06-example-solution/ExampleSolution.cs`](https://github.com/theonlydejf/remote-explorer-game/blob/main/examples/csharp/06-example-solution/ExampleSolution.cs).
- **Custom Server Host** – C#: [`examples/csharp/07-custom-server/ExampleCustomServer.cs`](https://github.com/theonlydejf/remote-explorer-game/blob/main/examples/csharp/07-custom-server/ExampleCustomServer.cs).

## Where to Go Next
- Follow **Getting Started** for installation, the first agent walkthrough, and quick verification steps.
- Browse **Tutorials & Examples** for guided exercises that expand on the bundled sample scripts.
- Configure or extend the backend with **Server Setup**, including `lesson-exec` knobs and custom host guidance.
- Dive into the generated **API Reference** when you need method signatures or type details.
