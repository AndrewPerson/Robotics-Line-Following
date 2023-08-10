namespace PathFinding.AStar;

public class Node<DataT>
{
    public List<(Node<DataT>, float, DataT)> Connections { get; }

    public Node(List<(Node<DataT>, float, DataT)> connections)
    {
        Connections = connections;
    }
}