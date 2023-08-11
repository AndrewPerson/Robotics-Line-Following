using PathFinding.AStar;

namespace PathFinding;

public static class RoutePlanner
{
    public static List<ConnectionType> FindRoute(Node<ConnectionType> from, Node<ConnectionType> to)
    {
        var thereRoute = GetRouteFromPath(AStar.AStar.FindPath(from, to, node => 0)); // TODO Implement heuristic
        var returnRoute = GetRouteFromPath(AStar.AStar.FindPath(to, from, node => 0));

        var route = new List<ConnectionType>(thereRoute.Count + 1 + returnRoute.Count);

        route.AddRange(thereRoute);
        route.Add(ConnectionType.CollectBox);
        route.AddRange(returnRoute);

        return route;
    }

    private static List<ConnectionType> GetRouteFromPath(List<Node<ConnectionType>> path)
    {
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
    BlueLine,
    CollectBox
}