namespace PathFinding.AStar;

public static class AStar
{
    public static List<Node<DataT>> FindPath<DataT>(Node<DataT> from, Node<DataT> to, Func<Node<DataT>, float> heuristic)
    {
        var openSet = new PriorityQueue<Node<DataT>, float>();
        openSet.Enqueue(from, 0);

        var cameFrom = new Dictionary<Node<DataT>, Node<DataT>>();

        var gScore = new Dictionary<Node<DataT>, float>()
        {
            { from, 0 }
        };

        var fScore = new Dictionary<Node<DataT>, float>()
        {
            { from, heuristic(from) }
        };
        
        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();
            if (current == to)
            {
                // TODO Return path
                throw new NotImplementedException();
            }

            foreach (var (neighbour, cost, _) in current.Connections)
            {
                var tentativeGScore = gScore.GetValueOrDefault(current, float.MaxValue) + cost;
                if (tentativeGScore < gScore.GetValueOrDefault(neighbour, float.MaxValue))
                {
                    cameFrom[neighbour] = current;
                    gScore[neighbour] = tentativeGScore;
                    fScore[neighbour] = tentativeGScore + heuristic(neighbour);

                    if (!openSet.UnorderedItems.Select(tuple => tuple.Element).Contains(neighbour))
                    {
                        openSet.Enqueue(neighbour, -fScore[neighbour]);
                    }
                }
            }
        }

        throw new Exception("Could not find a path!"); // TODO A better exception
    }
}