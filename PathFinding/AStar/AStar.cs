namespace PathFinding.AStar;

public record struct Node<DataT>(Dictionary<Node<DataT>, (float, DataT)> Connections);

public static class AStar
{
    public static List<Node<DataT>> FindPath<DataT>(Node<DataT> from, Node<DataT> to, Func<Node<DataT>, float> heuristic)
    {
        var toSearch = new PriorityQueue<Node<DataT>, float>();
        toSearch.Enqueue(from, 0);

        var currentlySearching = new HashSet<Node<DataT>>() { from };

        var cameFrom = new Dictionary<Node<DataT>, Node<DataT>>();

        var shortestPathTo = new Dictionary<Node<DataT>, float>() { { from, 0 } };
        
        while (toSearch.Count > 0)
        {
            var current = toSearch.Dequeue();
            currentlySearching.Remove(current);

            if (current == to) continue;

            foreach (var (neighbour, (cost, _)) in current.Connections)
            {
                var pathLengthToNeighbour = shortestPathTo[current] + cost;

                if (!shortestPathTo.ContainsKey(neighbour) || pathLengthToNeighbour < shortestPathTo[neighbour])
                {
                    cameFrom[neighbour] = current;
                    shortestPathTo[neighbour] = pathLengthToNeighbour;
                    
                    var weighting = pathLengthToNeighbour + heuristic(neighbour);

                    if (!currentlySearching.Contains(neighbour))
                    {
                        toSearch.Enqueue(neighbour, weighting);
                        currentlySearching.Add(neighbour);
                    }
                }
            }
        }

        {
            var current = to;
            var totalPath = new List<Node<DataT>>() { current };

            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                totalPath.Add(current);
            }

            totalPath.Reverse();

            return totalPath;
        }
    }
}