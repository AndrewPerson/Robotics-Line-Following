public struct SineCurve : ICurve
{
    public float Amplitude;
    public float Frequency;

    public SineCurve(float amplitude, float frequency)
    {
        Amplitude = amplitude;
        Frequency = frequency;
    }

    public double Sample(double x)
    {
        return Amplitude * Math.Sin(Frequency * x);
    }
}
