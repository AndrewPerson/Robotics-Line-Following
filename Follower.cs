using RoboMaster;

public class Follower
{
    public float BaseWheelSpeed { get; set; } = 75;

    public float TargetX { get; set; } = 0.35f;

    public float PSensitivity { get; set; } = 500;
    public float DSensitivity { get; set; } = 250;
    public float ISensitivity { get; set; } = 0;

    public NormalDistribution PointWeights = new(2, 0.5);

    private double previousError = 0;
    private double cumulativeError = 0;

    public (float, float) GetWheelSpeed(Line line)
    {
        var actual = line.Points.Select((point, index) => point.X * PointWeights.Sample(index)).Sum();

        var pError = TargetX - actual;
        var dError = pError - previousError;
        var iError = cumulativeError += pError;

        previousError = pError;

        var totalError = pError * PSensitivity + dError * DSensitivity + iError * ISensitivity;

        return ((float)(BaseWheelSpeed + totalError), (float)(BaseWheelSpeed - totalError));
    }
}