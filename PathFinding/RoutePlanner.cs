using PathFinding.AStar;

namespace PathFinding;

public static class RoutePlanner
{
    public static List<ConnectionType> FindRoute(Node<ConnectionType> start, Node<ConnectionType> pickup, Node<ConnectionType> drop)
    {
        var pickupRoute = GetRouteFromPath(AStar.AStar.FindPath(start, pickup, node => 0)); // TODO Implement heuristic
        var dropRoute = GetRouteFromPath(AStar.AStar.FindPath(pickup, drop, node => 0));
        dropRoute.RemoveAt(0); // Remove the first connection, which is the one we're currently on when we finish collecting the box

        var route = new List<ConnectionType>(pickupRoute.Count + dropRoute.Count + 2);

        route.AddRange(pickupRoute);
        route.Add(ConnectionType.CollectBox);
        route.AddRange(dropRoute);
        route.Add(ConnectionType.DropBox);

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
    CollectBox,
    DropBox
}