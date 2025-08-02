from enum import Enum
import httpx
import numpy as np
from dataclasses import dataclass
from typing import Optional, Sequence, Union


class Color(Enum):
    """Console color enumeration matching C# System.ConsoleColor names."""
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
        # Preserve exact casing for parsing compatibility
        return self.value

@dataclass(frozen=True)
class Tile:
    """Represents a game tile with two characters."""
    left: str
    right: str

    def __init__(self, data: Union[str, tuple[str, str]]):
        if isinstance(data, str):
            if len(data) != 2:
                raise ValueError("Tile string must have exactly 2 characters.")
            left, right = data[0], data[1]
        else:
            left, right = data
        object.__setattr__(self, 'left', left)
        object.__setattr__(self, 'right', right)

    def __repr__(self) -> str:
        return f"Tile({self.left}{self.right})"


@dataclass
class MovementResult:
    moved_successfully: bool
    is_agent_alive: bool


class RemoteGameSession:
    """
    A remote game session that communicates with the server over HTTP.
    Provides both synchronous and asynchronous move methods.
    """

    def __init__(self, server_url: str, session_id: str):
        self.server_url = server_url.rstrip('/')
        self.session_id = session_id
        self._sync_client = httpx.Client()
        self.last_response_message: Optional[str] = None
        self.is_agent_alive: bool = True

    @property
    def discovered_tile(self) -> Optional[Tile]:
        # Server-driven discovery not yet supported
        return None

    def move(self, move: Union[np.ndarray, Sequence[int]]) -> MovementResult:
        # převede tuple/list/Vector na numpy pole (2,)
        if not isinstance(move, np.ndarray):
            move = np.array(move, dtype=int)
        if move.shape != (2,):
            raise ValueError(f"move must be a length-2 sequence or array, got shape {move.shape}")
        dx, dy = int(move[0]), int(move[1])

        payload = {"sessionId": self.session_id, "dx": dx, "dy": dy}
        response = self._sync_client.post(f"{self.server_url}/move", json=payload)
        response.raise_for_status()
        return self._handle_move_response(response.json())

    async def move_async(self, move: Union[np.ndarray, Sequence[int]]) -> MovementResult:
        """Asynchronously send a move request to the server."""
        # Convert sequence to numpy array if needed
        if not isinstance(move, np.ndarray):
            move = np.array(move, dtype=int)
        # Validate move
        if move.shape != (2,):
            raise ValueError(f"move must be a length-2 sequence or array, got shape {move.shape}")
        dx, dy = int(move[0]), int(move[1])
        async with httpx.AsyncClient() as client:
            payload = {"sessionId": self.session_id, "dx": dx, "dy": dy}
            response = await client.post(f"{self.server_url}/move", json=payload)
            response.raise_for_status()
            return self._handle_move_response(response.json())

    def _handle_move_response(self, result: dict) -> MovementResult:
        """Parse the server response and update session state."""
        self.last_response_message = result.get("message")
        success = bool(result.get("success", False))
        if not success:
            self.is_agent_alive = False
            return MovementResult(False, self.is_agent_alive)
        self.is_agent_alive = bool(result.get("alive", False))
        moved = bool(result.get("moved", False))
        return MovementResult(moved, self.is_agent_alive)


@dataclass
class SessionIdentifier:
    """
    Identifies a game session; enforces simple validation on identifier and color.
    """

    def __init__(self, identifier: str, color: Color):
        self.identifier = identifier
        self.color = color

    def __post_init__(self):
        if len(self.identifier) > 2:
            raise ValueError("Identifier can be at most 2 characters long.")

class RemoteGameSessionFactory:
    """
    Factory for creating RemoteGameSession instances via the server's /connect endpoint.
    """

    def __init__(self, server_url: str, username: str = "unknown"):
        self.server_url = server_url.rstrip('/')
        self.username = username
        self._sync_client = httpx.Client()

    def create(self, identifier: SessionIdentifier) -> RemoteGameSession:
        """Connect to the server, obtain session UUID, and return a RemoteGameSession."""
        payload = {
            "identifier": identifier.identifier,
            "color": str(identifier.color),
            "username": self.username
        }
        response = self._sync_client.post(f"{self.server_url}/connect", json=payload)
        response.raise_for_status()
        data = response.json()
        if not data.get("success", False):
            raise RuntimeError(data.get("message", "Unknown error during session creation."))
        uuid = data.get("uuid")
        if not uuid:
            raise RuntimeError("Invalid response from server: missing 'uuid'.")
        return RemoteGameSession(self.server_url, uuid)
