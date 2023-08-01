using RoboMaster;

public class Follower : IObserver<FollowerData>
{
    public const float BaseWheelSpeed = 50;

    public const float TargetX = 0.35f;

    public const float PSensitivity = 500;
    public const float DSensitivity = 250;
    public const float ISensitivity = 0.01f;

    public const float LookAheadSensitivityDropoff = 0.5f;

    public RoboMasterClient Robot { get; }
    public Feed<(float, float)> WheelSpeed { get; } = new();

    private float previousError = 0;
    private float cumulativeError = 0;

    public Follower(RoboMasterClient robot)
    {
        Robot = robot;
    }

    public void OnCompleted()
    {
        throw new NotImplementedException();
    }

    public void OnError(Exception error)
    {
        throw new NotImplementedException();
    }

    public void OnNext(FollowerData value)
    {
        var (line, distance) = value;

        WheelSpeed.Notify(GetWheelSpeed(line, distance));   
    }

    private (float, float) GetWheelSpeed(Line line, float distance)
    {
        if (distance < 30)
        {
            return (0, 0);
        }

        if (line.Points.Length == 0)
        {
            return (0, 0);
        }
        else if (line.Type == LineType.Intersection)
        {
            return (0, 0);
        }
        else
        {
            var weights = line.Points.Select((_, i) => 1 - Math.Pow(LookAheadSensitivityDropoff, i));
            var weightSum = weights.Sum();
            var normalisedWeights = weights.Select(w => w / weightSum);

            var weightedXs = line.Points.Select((p, i) => p.X * normalisedWeights.ElementAt(i));

            var actual = (float)weightedXs.Sum();

            var pError = TargetX - actual;
            var dError = pError - previousError;
            var iError = cumulativeError += pError;

            var scaledError = pError * PSensitivity + dError * DSensitivity + iError * ISensitivity;

            previousError = pError;

            return (BaseWheelSpeed + scaledError, BaseWheelSpeed - scaledError);
        }
    }
}

public record struct FollowerData
(
    Line Line,
    float Distance
);
