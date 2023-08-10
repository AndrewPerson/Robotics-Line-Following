namespace PathFinding.AStar;

public static class AStar
{
    public static List<Node<DataT>> FindPath<DataT>(Node<DataT> from, Node<DataT> to, Func<Node<DataT>, float> heuristic)
    {
        var toSearch = new PriorityQueue<Node<DataT>, float>();
        toSearch.Enqueue(from, 0);

        var cameFrom = new Dictionary<Node<DataT>, Node<DataT>>();

        var shortestPathTo = new Dictionary<Node<DataT>, float>()
        {
            { from, 0 }
        };
        
        while (toSearch.Count > 0)
        {
            var current = toSearch.Dequeue();
            if (current == to)
            {
                var totalPath = new List<Node<DataT>>() { current };

                while (cameFrom.ContainsKey(current))
                {
                    current = cameFrom[current];
                    totalPath.Add(current);
                }

                totalPath.Reverse();

                return totalPath;
            }

            foreach (var (neighbour, cost, _) in current.Connections)
            {
                var pathLengthToNeighbour = shortestPathTo[current] + cost;

                if (!shortestPathTo.ContainsKey(neighbour) || pathLengthToNeighbour < shortestPathTo[neighbour])
                {
                    cameFrom[neighbour] = current;
                    shortestPathTo[neighbour] = pathLengthToNeighbour;
                    
                    var weighting = pathLengthToNeighbour + heuristic(neighbour);

                    if (!toSearch.UnorderedItems.Select(tuple => tuple.Element).Contains(neighbour))
                    {
                        toSearch.Enqueue(neighbour, weighting);
                    }
                }
            }
        }

        throw new Exception("Could not find a path!"); // TODO A better exception
    }
}