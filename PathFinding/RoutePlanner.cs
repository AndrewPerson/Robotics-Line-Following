using PathFinding.AStar;

namespace PathFinding;

public static class RoutePlanner
{
    public static List<ConnectionType> FindRoute(Node<ConnectionType> from, Node<ConnectionType> to)
    {
        var path = AStar.AStar.FindPath(from, to, node => 0); // TODO Implement heuristic

        var route = path.Zip(path.Skip(1))
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

        var inverseRoute = route.Select(stage => stage switch
        {
            ConnectionType.Forward => ConnectionType.Forward,
            ConnectionType.Right => ConnectionType.Left,
            ConnectionType.Left => ConnectionType.Right,
            ConnectionType.BlueLine => ConnectionType.BlueLine,
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
        }).Reverse().ToList();

        route.Add(ConnectionType.CollectBox);

        route.AddRange(inverseRoute);

        return route;
    }
}

public enum ConnectionType
{
    Forward,
    Left,
    Right,
    BlueLine,
    CollectBox
}