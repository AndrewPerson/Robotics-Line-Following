from typing import TypeVar, Generic

DataT = TypeVar("DataT")
class Node(Generic[DataT]):
    connections: dict["Node[DataT]", tuple[float, DataT]]

    def __init__(self, connections: dict["Node[DataT]", tuple[float, DataT]]):
        self.connections = connections
