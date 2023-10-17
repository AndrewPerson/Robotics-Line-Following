using Sprache;
using PathFinding.AStar;

using ConnectionTypeEnum = PathFinding.ConnectionType;
using CorrectionTypeEnum = PathFinding.CorrectionType;

namespace PathFinding;

public static class TrackParser
{
    public static readonly Parser<string> Identifier = Parse.LetterOrDigit.Or(Parse.Char(' ')).AtLeastOnce().Text();

    public static readonly Parser<ConnectionTypeEnum> ConnectionType =
        Parse.String("forward").Return(ConnectionTypeEnum.Forward)
                .Or(Parse.String("left").Return(ConnectionTypeEnum.Left))
                .Or(Parse.String("right").Return(ConnectionTypeEnum.Right))
                .Or(Parse.String("blue line").Return(ConnectionTypeEnum.BlueLine));

    public static readonly Parser<CorrectionTypeEnum> CorrectionType = 
        Parse.String("left").Return(CorrectionTypeEnum.Left)
                .Or(Parse.String("right").Return(CorrectionTypeEnum.Right))
                .Or(Parse.String("none").Return(CorrectionTypeEnum.None));

    public static readonly Parser<IntersectionPair> IntersectionPair =
        from firstName in Identifier
        from _ in Parse.String("->").Token()
        from secondName in Identifier
        from __ in Parse.Char('(').Token()
        from connectionType in ConnectionType
        from ___ in Parse.Char(',').Token()
        from length in Parse.DecimalInvariant.Token()
        from correctionType in (
            from ____ in Parse.Char(',').Token()
            from correctionType in CorrectionType.Token()
            select correctionType
        ).Optional()
        from ____ in Parse.Char(')').Token()
        select new IntersectionPair(firstName.Trim(), secondName.Trim(), connectionType, correctionType.GetOrElse(CorrectionTypeEnum.None), float.Parse(length));

    public static readonly Parser<IntersectionPair[]> Intersections =
        IntersectionPair.Token().Many().Select(pairs => pairs.ToArray()).End();

    public static Dictionary<string, Node<(ConnectionTypeEnum, CorrectionTypeEnum)>> ParseTrack(string track)
    {
        var intersections = Intersections.Parse(track);

        var nodes = new Dictionary<string, Node<(ConnectionTypeEnum, CorrectionTypeEnum)>>();

        foreach (var (firstName, secondName, connectionType, correctionType, length) in intersections)
        {
            Node<(ConnectionTypeEnum, CorrectionTypeEnum)> firstNode;

            if (nodes.ContainsKey(firstName))
            {
                firstNode = nodes[firstName];
            }
            else
            {
                firstNode = new Node<(ConnectionTypeEnum, CorrectionTypeEnum)>(new ());
                nodes[firstName] = firstNode;
            }

            Node<(ConnectionTypeEnum, CorrectionTypeEnum)> secondNode;

            if (nodes.ContainsKey(secondName))
            {
                secondNode = nodes[secondName];
            }
            else
            {
                secondNode = new Node<(ConnectionTypeEnum, CorrectionTypeEnum)>(new ());
                nodes[secondName] = secondNode;
            }

            firstNode.Connections[secondNode] = (length, (connectionType, correctionType));
        }

        return nodes;
    }
}

public record struct IntersectionPair
(
    string FirstName,
    string SecondName,
    ConnectionTypeEnum ConnectionType,
    CorrectionTypeEnum CorrectionType,
    float Length
);