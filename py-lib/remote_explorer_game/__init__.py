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

import importlib.metadata

__version__ = importlib.metadata.version("remote-explorer-game")
