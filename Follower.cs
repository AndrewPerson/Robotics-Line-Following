using RoboMaster;

public class Follower
{
    public float BaseWheelSpeed { get; set; } = 120;

    public float TargetX { get; set; } = 0.35f;

    public float PSensitivity { get; set; } = 3f;
    public float DSensitivity { get; set; } = 2f;
    public float ISensitivity { get; set; } = 0;

    public ICurve PointWeights { get; set; } = new NormalDistribution(0, 0.6);

    private double previousError = 0;
    private double cumulativeError = 0;

    public (float, float) GetWheelSpeed(Line line)
    {
        var weights = line.Points.Select((_, index) => PointWeights.Sample(index));
        var weightsSum = weights.Sum();

        var normalisedWeights = weights.Select(weight => weight / weightsSum); // Sums to one

        double actual = line.Points.Zip(normalisedWeights).Select(tuple => tuple.Item1.X * tuple.Item2).Sum();

        double pError = TargetX - actual;
        double dError = pError - previousError;
        double iError = cumulativeError += pError;

        previousError = pError;

        double totalError = Math.Clamp(pError * PSensitivity + dError * DSensitivity + iError * ISensitivity, -1, 1);

        float leftWeight;
        float rightWeight;

        if (totalError == 0)
        {
            leftWeight = 1;
            rightWeight = 1;
        }
        else if (totalError < 0) // Go Left
        {
            // var balance = ErrorCurve.Sample(totalError);
            var balance = -totalError;

            leftWeight = 1;
            rightWeight = (float)(balance * -2 + 1);
        }
        else // Go Right
        {
            // var balance = ErrorCurve.Sample(-totalError);
            var balance = totalError;

            leftWeight = (float)(balance * -2 + 1);
            rightWeight = 1;
        }

        var totalWeight = Math.Abs(leftWeight) + Math.Abs(rightWeight);

        var normalisedLeftWeight = leftWeight / totalWeight;
        var normalisedRightWeight = rightWeight / totalWeight;

        return (normalisedLeftWeight * BaseWheelSpeed, normalisedRightWeight * BaseWheelSpeed);
    }
}