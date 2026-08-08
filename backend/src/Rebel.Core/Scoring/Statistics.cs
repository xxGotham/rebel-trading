namespace Rebel.Core.Scoring;

public static class Statistics
{
    public static double Mean(IReadOnlyList<double> values) =>
        values.Count == 0 ? 0d : values.Sum() / values.Count;

    /// <summary>Sample standard deviation (n−1). Returns 0 for fewer than two values.</summary>
    public static double StandardDeviation(IReadOnlyList<double> values)
    {
        if (values.Count < 2) return 0d;

        var mean = Mean(values);
        var sum = 0d;
        foreach (var v in values)
        {
            var d = v - mean;
            sum += d * d;
        }

        return Math.Sqrt(sum / (values.Count - 1));
    }

    /// <summary>
    /// Cross-sectional z-score, clamped to ±<paramref name="winsorize"/>.
    /// A single runaway name would otherwise dominate the composite and push
    /// everything else to the same rank.
    /// </summary>
    public static double ZScore(double value, double mean, double stdDev, double winsorize = 3d)
    {
        if (stdDev <= 1e-12) return 0d;
        return Math.Clamp((value - mean) / stdDev, -winsorize, winsorize);
    }

    /// <summary>Standard normal CDF, used to map a composite z-score onto a 0–100 percentile.</summary>
    public static double NormalCdf(double z) => 0.5 * (1d + Erf(z / Math.Sqrt(2d)));

    /// <summary>Abramowitz &amp; Stegun 7.1.26 — max absolute error 1.5e-7, ample for ranking.</summary>
    public static double Erf(double x)
    {
        const double a1 = 0.254829592, a2 = -0.284496736, a3 = 1.421413741,
                     a4 = -1.453152027, a5 = 1.061405429, p = 0.3275911;

        var sign = x < 0 ? -1 : 1;
        x = Math.Abs(x);

        var t = 1d / (1d + p * x);
        var y = 1d - ((((a5 * t + a4) * t + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

        return sign * y;
    }
}
