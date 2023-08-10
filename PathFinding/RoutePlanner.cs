using PathFinding.AStar;

namespace PathFinding;

public static class RoutePlanner
{
    public static List<ConnectionType> FindRoute(Node<ConnectionType> from, Node<ConnectionType> to)
    {
        var path = AStar.AStar.FindPath(from, to, node => 0); // TODO Implement heuristic

        return path.Zip(path.Skip(1))
                    .Select
                    (
                        nodes =>
                            nodes.First.Connections
                            [
                                nodes.First.Connections.FindIndex
                                (
                                    connection => connection.Item1 == nodes.Second
                                )
                            ].Item3
                    ).ToList();
    }
}

public enum ConnectionType
{
    Forward,
    Left,
    Right,
    BlueLine
}