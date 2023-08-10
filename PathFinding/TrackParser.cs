using Sprache;
using PathFinding.AStar;

using ConnectionTypeEnum = PathFinding.ConnectionType;

namespace PathFinding;

public static class TrackParser
{
    public static readonly Parser<string> Identifier = Parse.LetterOrDigit.Or(Parse.Char(' ')).AtLeastOnce().Text();

    public static readonly Parser<ConnectionTypeEnum> ConnectionType =
        Parse.String("forward").Return(ConnectionTypeEnum.Forward)
                .Or(Parse.String("left").Return(ConnectionTypeEnum.Left))
                .Or(Parse.String("right").Return(ConnectionTypeEnum.Right))
                .Or(Parse.String("blue line").Return(ConnectionTypeEnum.BlueLine));

    public static readonly Parser<IntersectionPair> IntersectionPair =
        from firstName in Identifier
        from _ in Parse.String("->").Token()
        from secondName in Identifier
        from __ in Parse.Char('(').Token()
        from connectionType in ConnectionType
        from ___ in Parse.Char(',').Token()
        from length in Parse.DecimalInvariant.Token()
        from ____ in Parse.Char(')').Token()
        select new IntersectionPair(firstName.Trim(), secondName.Trim(), connectionType, float.Parse(length));

    public static readonly Parser<IntersectionPair[]> Intersections =
        IntersectionPair.Token().Many().Select(pairs => pairs.ToArray()).End();

    public static Dictionary<string, Node<ConnectionTypeEnum>> ParseTrack(string track)
    {
        var intersections = Intersections.Parse(track);

        var nodes = new Dictionary<string, Node<ConnectionTypeEnum>>();

        foreach (var (firstName, secondName, connectionType, length) in intersections)
        {
            Node<ConnectionTypeEnum> firstNode;

            if (nodes.ContainsKey(firstName))
            {
                firstNode = nodes[firstName];
            }
            else
            {
                firstNode = new Node<ConnectionTypeEnum>(new List<(Node<ConnectionTypeEnum>, float, ConnectionTypeEnum)>());
                nodes[firstName] = firstNode;
            }

            Node<ConnectionTypeEnum> secondNode;

            if (nodes.ContainsKey(secondName))
            {
                secondNode = nodes[secondName];
            }
            else
            {
                secondNode = new Node<ConnectionTypeEnum>(new List<(Node<ConnectionTypeEnum>, float, ConnectionTypeEnum)>());
                nodes[secondName] = secondNode;
            }

            firstNode.Connections.Add((secondNode, length, connectionType));
        }

        return nodes;
    }
}

public record struct IntersectionPair
(
    string firstName,
    string secondName,
    ConnectionTypeEnum ConnectionType,
    float length
);