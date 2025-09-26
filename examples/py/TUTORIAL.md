# Running Python Examples

This short guide explains how to install the library and run the Python examples.

> **Where am I?**  
> This file lives in `examples/py/`. The recommended way to run examples is to execute the commands **from the project root** so imports work out of the box.

---

## 1) Install (editable mode)

From the **project root** (the folder that contains `pyproject.toml`), run:

```bash
pip install -e .
```

- Installs the package in **editable** mode.
- Any changes in `py-lib/remote_explorer_game/` are picked up immediately (no reinstall needed).
- If you change dependencies in `pyproject.toml`, run the command above again.

Optional: verify the version

```bash
python -c "import remote_explorer_game; print(remote_explorer_game.__version__)"
```

---

## 2) Run the simple example

From the **project root**:

```bash
python examples/py/01-simple/example-simple.py
```

Check the Server's test world to see your example agent moving.  
If you encounter any errors, refer to [Adjusting the example](#3-adjusting-the-example) or [Common issues](#4-common-issues) below.

> **Tip:** Everyone should use a unique two-character identifier so agents don't clash on the server display.

---

## 3) Adjusting the example

Open `examples/py/01-simple/example-simple.py` and edit:

- **Server address**: `address = "http://127.0.0.1:8080/"`
- **Username**: set your own name instead of `"Example"`
- **Identifier & color**:
  ```python
  identifier = VisualSessionIdentifier("[]", Color.Magenta)  # two characters max
  ```

---

## 4) Common issues

- **Module not found**: Ensure you ran `pip install -e .` from the project root and you're launching Python from the same root.
- **Server not reachable**: Check the server address/port and that the server is running.
- **Identifier conflicts**: Use a unique two-character identifier per user.
