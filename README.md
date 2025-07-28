# Remote Explorer Game

**Remote Explorer Game** is an educational programming sandbox where students write agents that explore a grid-based world by sending movement commands to a centralized HTTP server. It’s designed to teach programming concepts like algorithms, concurrency, networking, and error handling in a fun, competitive environment.

---

## 🎯 Who is this for?

- 🧑‍🎓 **Students** – can control agents via simple API calls
- 🧑‍🏫 **Teachers** – run server instances to manage worlds

---

## 🚀 Quick Start

### 🧑‍🏫 For Teachers – Launch the Server

1. Download a compiled `lesson-exec` zip from the GitHub [release](https://github.com/theonlydejf/remote-explorer-game/releases)
2. Unzip and run

This starts:
- a **test world** on port `8080`
- multiple **challenge worlds** on ports `8081+`
- a live console visualizer and logging system

Press `ESC` or `Q` to stop the server.

> [!NOTE]
> For the best visual experience, use a dark-themed terminal

## 🧑‍🎓 For Students – Use the Client Library

1. Download the provided `lib-vX.X.zip` from [releases](https://github.com/theonlydejf/remote-explorer-game/releases)
2. Unpack it and reference it in a new C# project
3. Create your `Program.cs`:

```csharp
var factory = new RemoteGameSessionFactory("http://127.0.0.1:8080/", "Ukazka");
var session = factory.Create(new SessionIdentifier("[]", ConsoleColor.Magenta));
session.Move(new Vector(1, 0)); // move right
```

> [!TIP]
> Don't forget to explore the examples!

---

## 💡 Code Examples

### 👣 Basic Movement

[`example-simple`](./example-simple/Program.cs) — moves an agent in a square using synchronous calls.

## 🧵 Feedback and Error Handling

[`example-feedback`](./example-feedback/Program.cs) — shows how to safely check movement results and handle potential errors such as null results, timeouts, or invalid server states. Ideal for debugging.

### 🤖 Multiple Agents

[`example-multiple-agents`](./example-multiple-agents/Program.cs) — spawns 5 agents moving independently.

### 🔁 Async Movement

[`example-async`](./example-async/Program.cs) — demonstrates `MoveAsync` and polling for result.

### 🌪 Async + Multiple Agents

[`example-async-multiple-agents`](./example-async-multiple-agents/Program.cs) — best performance with parallel movement logic.

### 🧭 Basic Pathfinding Solution

[`example-solution`](./example-solution/Program.cs) — demonstrates how to implement a simple pathfinding algorithm that explores the map intelligently using visited node tracking and heuristic movement.

---

## 🧩 How to Create a Challenge

### 1. 🗺️ Create Challenge Maps

- Each map is a **PNG image**, where:
  - **Bright pixels** represent **deadly (trap)** tiles
  - **Dark pixels** represent **walkable (safe)** tiles

The image is read pixel by pixel. Each pixel becomes one cell in the grid-based world.

### 2. 📁 Place Images in the Correct Directory

Create and organise the images like this:

```
resources/challenges/
├── challenge-1.png
├── challenge-2.png
├── ...
```

- File names must match the pattern: `challenge-<number>.png`
- Images will be sorted and loaded in numeric order

### 3. 🚀 Run the Server

Run the `lesson-exec` binary (or build and run it yourself). It will automatically:

- Launch the **test world server** on port `8080`
- Launch each **challenge server** starting from port `8081` upward

Example output:

```
Test server started on port 8080
Challenge 1 server started on port 8081
Challenge 2 server started on port 8082
```

> [!NOTE]
> Challenge worlds cannot be viewed in the default lesson-exec

### 4. 🎮 Connect to a Specific Challenge

Students must point their agent to the correct server port:

```csharp
var factory = new RemoteGameSessionFactory("http://127.0.0.1:8082/", "StudentName");
```

In this example, the agent connects to **challenge 2**, which runs on port `8082`.

---

## 🏗️ Build Instructions

You can build and package everything yourself using:

```bash
./build.sh --self-contained
```

It generates:

- `packages/lib-Y-M-D.zip` — for students
- `packages/lesson-exec-RID-Y-M-D.zip` — server executable

Default RIDs: `win-x64`, `linux-x64`, `osx-x64`, etc.

Use `./build.sh --help` for more info

---

## 🧱 Project Structure

| Folder/File                      | Description                                  |
|----------------------------------|----------------------------------------------|
| `explorer-game/`                 | Core engine (sessions, networking, maps)     |
| `lesson-exec/`                   | Console server for lessons                   |
| `example-simple/`                | Basic movement example                       |
| `example-feedback/`              | Debugging + error handling sample            |
| `example-multiple-agents/`       | Multiple synchronous agents                  |
| `example-async/`                 | Async client logic                           |
| `example-async-multiple-agents/` | Fully async, multi-agent demo                |
| `example-solution`               | Solution using A* pathfinding                |
| `resources/test-map.png`         | Sample map                                   |
| `build.sh`                       | Build + package script                       |

---

## 📦 Dependencies

- [.NET 6 SDK](https://dotnet.microsoft.com/)
- [Newtonsoft.Json](https://www.newtonsoft.com/json)
- [ImageSharp](https://github.com/SixLabors/ImageSharp)

---

## 🧑‍💻 Contributing

Contributions from teachers, students, and curious developers are welcome! Fork the repo and create a pull request, or open an issue to discuss improvements.
