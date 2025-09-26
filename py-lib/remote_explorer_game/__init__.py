# Re-export the public API from the implementation module
from .remote_explorer_game import (
    Color,
    Tile,
    MovementResult,
    VisualSessionIdentifier,
    SessionIdentifier,
    RemoteGameSession,
    RemoteGameSessionFactory,
)

__all__ = [
    "Color",
    "Tile",
    "MovementResult",
    "VisualSessionIdentifier",
    "SessionIdentifier",
    "RemoteGameSession",
    "RemoteGameSessionFactory",
]

# Optional: version (you can update manually or automate later)
__version__ = "0.1.0"
