using PathFinding.AStar;

namespace PathFinding;

public static class RoutePlanner
{
    public static List<(ConnectionType, CorrectionType)> FindRoute(Node<(ConnectionType, CorrectionType)> start, Node<(ConnectionType, CorrectionType)> pickup, Node<(ConnectionType, CorrectionType)> drop)
    {
        var pickupRoute = GetRouteFromPath(AStar.AStar.FindPath(start, pickup, node => 0)); // TODO Implement heuristic
        var dropRoute = GetRouteFromPath(AStar.AStar.FindPath(pickup, drop, node => 0));
        dropRoute.RemoveAt(0); // Remove the first connection, which is the one we're currently on when we finish collecting the box

        var route = new List<(ConnectionType, CorrectionType)>(pickupRoute.Count + dropRoute.Count + 2);

        route.AddRange(pickupRoute);
        route.Add((ConnectionType.CollectBox, CorrectionType.None));
        route.AddRange(dropRoute);
        route.Add((ConnectionType.DropBox, CorrectionType.None));

        return route;
    }

    private static List<(ConnectionType, CorrectionType)> GetRouteFromPath(List<Node<(ConnectionType, CorrectionType)>> path)
    {
        return path.Zip(path.Skip(1))
                    .Select(nodes => nodes.First.Connections[nodes.Second].Item2)
                    .ToList();
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

public enum CorrectionType
{
    Left,
    Right,
    None
}