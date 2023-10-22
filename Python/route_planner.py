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
    
    pickup_route: list = get_route_from_path(shortest_path(start, pickup))
    drop_route: list = get_route_from_path(shortest_path(pickup, dropoff))

    drop_route.pop(0)

    route = [
        *pickup_route,
        (ConnectionType.CollectBox, CorrectionType.NoCorrection),
        *drop_route,
        (ConnectionType.DropBox, CorrectionType.NoCorrection)
    ]

    return route

def get_route_from_path(nodes: list[Node[tuple[ConnectionType, CorrectionType]]]):
    return list(
        map(
            lambda nodes: nodes[0].connections[nodes[1]][1],
            zip(nodes, nodes[1:])
        )
    )