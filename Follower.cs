using RoboMaster;

public class Follower
{
    public float BaseWheelSpeed { get; set; } = 150;

    public float TargetX { get; set; } = 0.35f;

    public float PSensitivity { get; set; } = 1;
    public float DSensitivity { get; set; } = 0.5f;
    public float ISensitivity { get; set; } = 0;

    public ICurve PointWeights = new NormalDistribution(2, 0.5);

    public ICurve ErrorCurve = new SineCurve(1, 90);

    private double previousError = 0;
    private double cumulativeError = 0;

    public (float, float) GetWheelSpeed(Line line)
    {
        var weights = line.Points.Select((_, index) => PointWeights.Sample(index));
        var weightsSum = weights.Sum();

        var actual = line.Points.Zip(weights).Select((tuple, index) => tuple.Item1.X * (tuple.Item2 / weightsSum)).Sum();

        var pError = TargetX - actual;
        var dError = pError - previousError;
        var iError = cumulativeError += pError;

        previousError = pError;

        var totalError = Math.Clamp(pError * PSensitivity + dError * DSensitivity + iError * ISensitivity, -1, 1);

        float leftWeight;
        float rightWeight;

        if (totalError == 0)
        {
            leftWeight = 1;
            rightWeight = 1;
        }
        else if (totalError > 1) // Go Left
        {
            var balance = ErrorCurve.Sample(totalError);

            leftWeight = 1;
            rightWeight = (float)(balance * -2 + 1);
        }
        else // Go Right
        {
            var balance = -ErrorCurve.Sample(totalError);

            leftWeight = (float)(balance * -2 + 1);
            rightWeight = 1;
        }

        var totalWeight = Math.Abs(leftWeight) + Math.Abs(rightWeight);

        var normalisedLeftWeight = leftWeight / totalWeight;
        var normalisedRightWeight = rightWeight / totalWeight;

        return (normalisedLeftWeight * BaseWheelSpeed, normalisedRightWeight * BaseWheelSpeed);
    }
}