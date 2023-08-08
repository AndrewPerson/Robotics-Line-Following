public struct StraightLine : ICurve
{
    public float Gradient { get; set; }
    public float Offset { get; set; }

    public StraightLine(float gradient = 1, float offset = 0)
    {
        Gradient = gradient;
        Offset = offset;
    }

    public double Sample(double x)
    {
        return Gradient * x + Offset;
    }
}
