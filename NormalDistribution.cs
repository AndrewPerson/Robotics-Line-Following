public struct NormalDistribution
{
    public double Mean { get; set; } = 0;
    public double StandardDeviation { get; set; } = 1;

    public NormalDistribution(double mean, double standardDeviation)
    {
        Mean = mean;
        StandardDeviation = standardDeviation;
    }

    public double Sample(double x)
    {
        // Formula from https://www.math.net/gaussian-distribution
        return 1 / (StandardDeviation * Math.Sqrt(2 * Math.PI))
            * Math.Pow
            (
                Math.E,
                -0.5 * Math.Pow
                (
                    (x - Mean) / StandardDeviation,
                    2
                )
            );
    }
}
