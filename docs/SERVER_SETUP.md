# Server Setup

## ConnectionHandler at a Glance
- Hosts an HTTP listener that answers `/connect` and `/move`.
- Manages per-client session caps, idle timeouts, and serialized action queues.
- Emits events (`SessionConnected`, `AgentDied`) for logging, visualization, or telemetry.
- Wraps each agent in `LocalGameSession`, tracking map bounds and trap collisions.

## Running `lesson-exec`
- Preferred workflow:
  1. Grab the latest packaged binaries from the [GitHub releases page](https://github.com/theonlydejf/remote-explorer-game/releases).
  2. Extract the archive that matches your OS/architecture.
  3. Launch the bundled executable (`lesson-exec.exe` on Windows, `./lesson-exec` elsewhere).
  4. The Test World (port 8080) uses the console visualizer; challenge worlds (8081+) run headless.
- Build from source (`dotnet run --project lesson-exec`) only when editing the host or testing new configuration options.
- CLI options:
  - `--resources <path>` — override the default `resources/` root (test map + challenges).
  - `--port <num>` / `--challenge-port-start <num>` — change the Test World port and the base for auto-numbered challenge worlds.
  - `--no-visualizer` — run fully headless, skipping the console map and VSID enforcement.
  - `--max-sessions <num>` — cap concurrent sessions per client (default 20).
  - `--idle-timeout <seconds>` — kill inactive agents after the specified number of seconds (default 5).
  - `--log-file <path>` — append structured text logs to the given file (directories created automatically).
  - `--log-level <trace|info|warn|error>` — minimum severity for file logging (default `info`).
  - `--map-config <file>` — JSON file that replaces the built-in world discovery.

Example JSON for `--map-config`:

```json
[
  {
    "name": "Visual Test",
    "port": 9000,
    "map": "resources/test-map.png",
    "color": "Cyan",
    "visualize": true
  },
  {
    "name": "Hidden Challenge",
    "port": 9001,
    "map": "resources/challenges/challenge-1.png",
    "color": "Green",
    "visualize": false
  }
]
```

> Only one world can be visualized at a time. When multiple entries set `visualize: true`, the launcher exits with an error.

## Customizing Worlds
- **Map sources**: place PNGs under `resources/challenges` (naming `challenge-<index>.png`) or supply explicit file paths via configuration.
- **Visual identifiers**: enforce unique identifier/color pairs when the visualizer is active; disable enforcement via CLI for headless runs.
- **Session limits**: adjust `clientSessions` thresholds or idle windows through configuration for varied difficulty.
- **Colors & names**: expose `WorldInfo` metadata (name, console color, port) as part of the configurable map definition.

## Operating Headless Instances
- Deploy `lesson-exec` with `--no-visualizer` for remote shells/containers.
- Direct logs to files for later inspection; consider structured logging for ingestion.
- Front the HTTP listener with reverse proxies or TLS terminators if exposing to the internet.
- Monitor session counts, idle kicks, and trap collisions through `SessionConnected`/`AgentDied` hooks.

## Building Custom Hosts
- Minimal skeleton:
  ```csharp
  using ExplorerGame.Base;
  using ExplorerGame.Net;

  var map = GameFactory.MapFromImage("resources/custom.png");
  var handler = new ConnectionHandler(map, visualizer: null);
  await handler.StartHttpServer(9000, CancellationToken.None);
  ```
- Attach bespoke loggers or telemetry by subscribing to `SessionConnected` and `LocalGameSession.AgentDied`.
- Swap out map generation (`GameFactory.MapFromImage`, procedural generators, or manual arrays).
- Tune rules by extending `LocalGameSession` (different valid moves, scoring, or hazards).

## Troubleshooting & FAQ
- **Port already in use**: supply alternate ports via CLI or terminate conflicting services.
- **Identifier collisions**: ensure unique VSIDs per world; enable identifier auto-assignment if desired.
- **Agents die immediately**: check for traps near spawn, idle timeouts, or missing VSID on visualized worlds.
- **HTTP 404/500**: confirm the server path (`/connect`, `/move`) and review log output for exceptions.
