# Tutorials & Examples

## Overview
- Each example pairs a concise script with a single learning objective.
- Run commands from the repository root so path-relative imports resolve.
- Ensure `lesson-exec` is active before launching any client script.

## Quick Reference
| Example | Concept | Python | C# |
| --- | --- | --- | --- |
| Simple Movement | Connecting, basic moves, loops | `examples/py/01-simple/example-simple.py` | `examples/csharp/01-simple/ExampleSimple.cs` |
| Feedback & Errors | Inspecting `MovementResult`, idle timeout handling | `examples/py/02-feedback/example-feedback.py` | `examples/csharp/02-feedback/ExampleFeedback.cs` |
| Multiple Agents | Coordinating swarms, auto-respawn | `examples/py/03-multiple-agents/example-multiple-agents.py` | `examples/csharp/03-multiple-agents/ExampleMultipleAgents.cs` |
| Async Movement | Non-blocking requests, polling handles | `examples/py/04-async/example-async.py` | `examples/csharp/04-async/ExampleAsync.cs` |
| Async Multi-Agent | Parallel async orchestration | `examples/py/05-async-multiple-agents/example-async-multiple-agents.py` | `examples/csharp/05-async-multiple-agents/ExampleAsyncMultipleAgents.cs` |
| Advanced Solution | Shared knowledge, heuristics | – | `examples/csharp/06-example-solution/ExampleSolution.cs` |
| Custom Server | Embedding `ConnectionHandler` | – | `examples/csharp/07-custom-server/ExampleCustomServer.cs` |

## Walkthrough Highlights
### Simple Movement
- Shows connection setup, VSID selection, and directional moves (`move_n_times` helper in Python, `MoveNTimes` in C#) culminating in a square walk loop.

### Feedback & Error Handling
- Examines `MovementResult` properties, demonstrates invalid move responses, and surfaces idle timeout messages; extend by piping diagnostics into logs or dashboards.

### Multiple Agents
- Spawns several agents with unique VSIDs, applies weighted random motion, and respawns on death; adapt to share state or coordinate exploration strategies.

### Async Movement
- Invokes `move_async`/`MoveAsync` to keep applications responsive; polling handles can evolve into event loops or `async Task Main` workflows.

### Async Multi-Agent
- Maintains parallel async handles, restarting them on completion while handling agent deaths; a foundation for actor-style orchestration.

### Advanced Solution (C#)
- Demonstrates collaborative exploration with shared map knowledge and heuristics suitable for competitions or grading rubrics.

### Custom Server (C#)
- Embeds `ConnectionHandler` inside a standalone host, illustrating how to extend or replace `lesson-exec` with bespoke infrastructure.

## Troubleshooting Tips
- **Module not found** – run `pip install remote-explorer-game` (or `pip install -e .` when developing locally) from the project root and launch Python from the same directory.
- **Server not reachable** – verify the server URL/port, ensure `lesson-exec` is running, and watch the console logger for startup errors.
- **Identifier conflicts** – choose a unique two-character VSID per agent, especially when sharing the Test World with classmates.
- **Headless runs** – when you use `--no-visualizer`, enable file logging to track agent lifecycle events without the console UI.

## Adapting the Examples
- Always choose unique VSIDs when running alongside classmates to avoid console collisions.
- Parameterize server URLs and usernames via environment variables for easier switching.
- Wrap loops with try/except or try/catch to ensure graceful shutdown on `KeyboardInterrupt`.
- Combine strategies (e.g., async + multi-agent) to tackle challenge worlds more effectively.

## Related Reading
- **Getting Started** – installation, first agent walkthrough.
- **Server Setup** – tuning `lesson-exec`, adding maps, and building custom hosts.
- **API Reference** – deep dive into `RemoteGameSession`, `MovementResult`, and data types.
