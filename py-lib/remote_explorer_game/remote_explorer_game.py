from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
from typing import Any, Dict, Optional, Sequence, Tuple, Union, overload

import httpx
import numpy as np
import asyncio
import threading

# ==============================
# Colors
# ==============================

class Color(Enum):
    """
    Console color for rendering identifiers.

    The string value is sent to the server exactly as defined here.
    """
    Black = "Black"
    DarkBlue = "DarkBlue"
    DarkGreen = "DarkGreen"
    DarkCyan = "DarkCyan"
    DarkRed = "DarkRed"
    DarkMagenta = "DarkMagenta"
    DarkYellow = "DarkYellow"
    Gray = "Gray"
    DarkGray = "DarkGray"
    Blue = "Blue"
    Green = "Green"
    Cyan = "Cyan"
    Red = "Red"
    Magenta = "Magenta"
    Yellow = "Yellow"
    White = "White"

    def __str__(self) -> str:
        return self.value


# ==============================
# Tiles
# ==============================

@dataclass(frozen=True)
class Tile:
    """
    Two-character map tile.

    Create from a 2-character string (e.g., "AB") or a tuple of two single-character strings.
    """
    left: str
    right: str

    def __init__(self, data: Union[str, Tuple[str, str]]):
        if isinstance(data, str):
            if len(data) != 2:
                raise ValueError("Tile string must have exactly 2 characters.")
            left, right = data[0], data[1]
        else:
            left, right = data
            if len(left) != 1 or len(right) != 1:
                raise ValueError("Tile tuple must be (char, char).")
        object.__setattr__(self, "left", left)
        object.__setattr__(self, "right", right)

    def __repr__(self) -> str:
        return f"Tile({self.left}{self.right})"

    def __str__(self) -> str:
        return f"{self.left}{self.right}"

    @staticmethod
    def to_json(tile: Optional["Tile"]) -> Optional[Dict[str, str]]:
        """Serialize a tile into a JSON-compatible dict, or None."""
        if tile is None:
            return None
        return {"str": str(tile)}

    @staticmethod
    def from_json(obj: Optional[Dict[str, Any]]) -> Optional["Tile"]:
        """Deserialize a tile from a JSON-compatible dict, or None."""
        if obj is None:
            return None
        s = obj.get("str")
        if s is None or len(s) != 2:
            raise ValueError("Invalid tile JSON: missing/invalid 'str'.")
        return Tile(s)


# ==============================
# Movement result
# ==============================

@dataclass(frozen=True)
class MovementResult:
    """
    Outcome of a movement attempt.

    Attributes:
        moved_successfully: True if the move executed.
        is_agent_alive: True if the agent survived after the move.
        discovered_tile: Tile discovered by this move, if any.
    """
    moved_successfully: bool
    is_agent_alive: bool
    discovered_tile: Optional[Tile] = None

@dataclass
class AsyncMovementResult:
    """
    Async result handle for a movement request.

    Attributes:
        ready: True when movement_result is available.
        movement_result: The parsed MovementResult once ready.
        task: asyncio.Task if scheduled on a running loop, otherwise None.
    """
    def __init__(self) -> None:
        self.ready: bool = False
        self.movement_result: Optional[MovementResult] = None
        self.task: Optional[asyncio.Task] = None
        self._done = threading.Event()

    def wait(self, timeout: Optional[float] = None) -> Optional[MovementResult]:
        """Block until finished. Returns the result (or None on timeout)."""
        finished = self._done.wait(timeout)
        return self.movement_result if finished else None

# ==============================
# Visual Session Identifier (VSID)
# ==============================

class VisualSessionIdentifier:
    """
    Visual identifier used to render an agent.

    Minimal client-side checks:
      - identifier_str must be length ≤ 2 (to avoid accidental long strings).
      - color passed through as-is.

    All other validation/sanitization is handled by the server.
    """

    def __init__(self, identifier_str: str, color: Color = Color.White):
        if len(identifier_str) > 2:
            raise ValueError("Identifier string can be 2 characters at most.")
        self._identifier_str = identifier_str
        self._color = color

    @property
    def identifier_str(self) -> str:
        """Two-character (or shorter) string used for rendering."""
        return self._identifier_str

    @identifier_str.setter
    def identifier_str(self, value: str) -> None:
        if len(value) > 2:
            raise ValueError("Identifier string can be 2 characters at most.")
        self._identifier_str = value

    @property
    def color(self) -> Color:
        """Console color associated with this visual identifier."""
        return self._color

    @color.setter
    def color(self, value: Color) -> None:
        self._color = value

# ==============================
# Session Identifier (SID + VSID)
# ==============================

class SessionIdentifier:
    """
    Identifies a session:
      - sid: server-assigned session ID (set after /connect).
      - vsid: optional visual identifier used for rendering.

    Convenience properties `identifier_str` and `color` proxy to the `vsid`.
    """

    def __init__(self, vsid: Optional[VisualSessionIdentifier] = None, sid: Optional[str] = None):
        self.sid: Optional[str] = sid
        self.vsid: Optional[VisualSessionIdentifier] = vsid

    @classmethod
    def from_visual(cls, identifier_str: str, color: Color = Color.White) -> "SessionIdentifier":
        return cls(VisualSessionIdentifier(identifier_str, color), None)

    @property
    def connection_ready(self) -> bool:
        """True if `sid` is present (ready for /move)."""
        return self.sid is not None

    @property
    def has_vsid(self) -> bool:
        """True if a visual identifier is present."""
        return self.vsid is not None

    @property
    def identifier_str(self) -> str:
        if self.vsid is None:
            raise ValueError("No VSID associated with this SessionIdentifier.")
        return self.vsid.identifier_str

    @identifier_str.setter
    def identifier_str(self, value: str) -> None:
        if self.vsid is None:
            raise ValueError("No VSID associated with this SessionIdentifier.")
        self.vsid.identifier_str = value

    @property
    def color(self) -> Color:
        if self.vsid is None:
            raise ValueError("No VSID associated with this SessionIdentifier.")
        return self.vsid.color

    @color.setter
    def color(self, value: Color) -> None:
        if self.vsid is None:
            raise ValueError("No VSID associated with this SessionIdentifier.")
        self.vsid.color = value

SessionIdentifierLike = Union[
    "SessionIdentifier",
    "VisualSessionIdentifier",
    Tuple[str, "Color"],
    str,
    None,
]

def _coerce_session_identifier(value: SessionIdentifierLike) -> Optional["SessionIdentifier"]:
    """
    Convert various identifier-like inputs into a SessionIdentifier.

    - SessionIdentifier -> returned as-is
    - VisualSessionIdentifier -> wrapped into SessionIdentifier(vsid)
    - (str, Color) -> VisualSessionIdentifier(str, Color) -> SessionIdentifier
    - str -> VisualSessionIdentifier(str, Color.White) -> SessionIdentifier
    - None -> None
    """
    if value is None:
        return None
    from typing import cast
    if isinstance(value, SessionIdentifier):
        return value
    if isinstance(value, VisualSessionIdentifier):
        return SessionIdentifier(value)
    if isinstance(value, tuple):
        ident, color = value
        return SessionIdentifier(VisualSessionIdentifier(ident, color))
    if isinstance(value, str):
        return SessionIdentifier(VisualSessionIdentifier(value, Color.White))
    raise TypeError(
        "identifier must be SessionIdentifier | VisualSessionIdentifier | (str, Color) | str | None"
    )

# ==============================
# Remote game session
# ==============================

class RemoteGameSession:
    """
    Remote game session bound to a server.

    Provides synchronous and asynchronous methods to move the agent.
    """

    def __init__(self, server_url: str, sid: str, *, timeout: Optional[float] = 10.0):
        self.server_url = server_url.rstrip("/")
        self.sid = sid
        self._sync_client = httpx.Client(timeout=timeout)
        self.last_response_message: Optional[str] = None
        self.is_agent_alive: bool = True
        self._discovered_tile: Optional[Tile] = None

    @property
    def discovered_tile(self) -> Optional[Tile]:
        """Tile discovered by the last move, if any."""
        return self._discovered_tile

    @staticmethod
    def _normalize_move(move: Union[np.ndarray, Sequence[int]]) -> Tuple[int, int]:
        arr = np.array(move, dtype=int) if not isinstance(move, np.ndarray) else move
        if arr.shape != (2,):
            raise ValueError(f"move must be a length-2 sequence/array, got shape {arr.shape}")
        return int(arr[0]), int(arr[1])

    def _parse_move_response(self, payload: Dict[str, Any]) -> MovementResult:
        self.last_response_message = payload.get("message")
        discovered_obj = payload.get("discovered")
        discovered_tile = Tile.from_json(discovered_obj) if discovered_obj is not None else None
        self._discovered_tile = discovered_tile

        success = bool(payload.get("success", False))
        if not success:
            self.is_agent_alive = False
            return MovementResult(False, self.is_agent_alive, discovered_tile)

        moved = bool(payload.get("moved", False))
        self.is_agent_alive = bool(payload.get("alive", False))
        return MovementResult(moved, self.is_agent_alive, discovered_tile)

    def move(self, move: Union[np.ndarray, Sequence[int]]) -> MovementResult:
        """
        Send a synchronous move request.

        Args:
            move: Length-2 vector (dx, dy).

        Returns:
            MovementResult with success, alive status, and discovered tile.
        """
        dx, dy = self._normalize_move(move)
        resp = self._sync_client.post(f"{self.server_url}/move", json={"sid": self.sid, "dx": dx, "dy": dy})
        resp.raise_for_status()
        return self._parse_move_response(resp.json())

    def move_async(self, move: Union[np.ndarray, Sequence[int]]) -> AsyncMovementResult:
        """
        Start a move in the background and return a handle immediately.

        - If an asyncio event loop is running in this thread, schedule the HTTP request on it.
        - Otherwise, run the request on a background thread.
        """
        dx, dy = self._normalize_move(move)
        payload = {"sid": self.sid, "dx": dx, "dy": dy}
        handle = AsyncMovementResult()

        async def _runner_async():
            try:
                async with httpx.AsyncClient() as client:
                    resp = await client.post(f"{self.server_url}/move", json=payload)
                    resp.raise_for_status()
                    result = self._parse_move_response(resp.json())
            except Exception as e:
                self.last_response_message = str(e)
                result = MovementResult(False, False, None)
            handle.movement_result = result
            handle.ready = True
            handle._done.set()

        def _runner_thread():
            try:
                with httpx.Client(timeout=self._sync_client.timeout) as client:
                    resp = client.post(f"{self.server_url}/move", json=payload)
                    resp.raise_for_status()
                    result = self._parse_move_response(resp.json())
            except Exception as e:
                self.last_response_message = str(e)
                result = MovementResult(False, False, None)
            handle.movement_result = result
            handle.ready = True
            handle._done.set()

        # Try to use an existing running loop; otherwise fall back to a thread
        try:
            loop = asyncio.get_running_loop()
        except RuntimeError:
            t = threading.Thread(target=_runner_thread, daemon=True)
            t.start()
            handle.task = None
        else:
            handle.task = loop.create_task(_runner_async())

        return handle


# ==============================
# Session factory
# ==============================

class RemoteGameSessionFactory:
    """
    Factory for connecting to the server and creating remote sessions.

    Endpoint:
      - POST /connect: body { "vsid": { "identifierStr": str, "color": str } | null,
                              "username": str }
                       -> { "success": bool, "sid": str }
    """

    def __init__(self, server_url: str, username: str = "unknown", *, timeout: Optional[float] = 10.0):
        self.server_url = server_url.rstrip("/")
        self.username = username
        self._sync_client = httpx.Client(timeout=timeout)

    def create(self, identifier: SessionIdentifierLike = None) -> RemoteGameSession:
        """
        Connect to the server and create a new remote session.

        Args:
            identifier: Optional session identifier with a VSID.

        Returns:
            A RemoteGameSession bound to the created `sid`.
        """

        ident_obj = _coerce_session_identifier(identifier)

        vsid_payload = None
        if ident_obj is not None and ident_obj.vsid is not None:
            vsid_payload = {
                "identifierStr": ident_obj.vsid.identifier_str,
                "color": str(ident_obj.vsid.color),
            }

        resp = self._sync_client.post(
            f"{self.server_url}/connect",
            json={"vsid": vsid_payload, "username": self.username},
        )
        resp.raise_for_status()
        data = resp.json()

        if not data.get("success", False):
            raise RuntimeError(data.get("message", "Unknown error during session creation."))

        sid = data.get("sid")
        if not sid:
            raise RuntimeError("Invalid response from server: missing 'sid'.")

        if ident_obj is not None:
            ident_obj.sid = sid

        return RemoteGameSession(self.server_url, sid)
