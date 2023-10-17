from enum import Enum
from node import Node


class ConnectionType(Enum):
    Forward = 1
    Left = 2
    Right = 3
    BlueLine = 4
    CollectBox = 5
    DropBox = 6


class CorrectionType(Enum):
    Left = 1
    Right = 2
    NoCorrection = 3


def find_route(start: Node[tuple[ConnectionType, CorrectionType]], pickup: Node[tuple[ConnectionType, CorrectionType]],
               dropoff: Node[tuple[ConnectionType, CorrectionType]]) -> list[tuple[ConnectionType, CorrectionType]]:
    ...

