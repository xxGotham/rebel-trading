namespace Rebel.Core.Indicators;

/// <summary>
/// Technical indicators over aligned price series.
/// <para>
/// Every method returns an array the same length as its input, with
/// <c>null</c> in the warm-up region. Keeping the alignment means a caller can
/// never accidentally pair an indicator value with the wrong bar — the single
/// most common source of silent lookahead bias.
/// </para>
/// </summary>
public static class TechnicalIndicators
{
    public static double?[] Sma(IReadOnlyList<double> values, int period)
    {
        Guard(values, period);
        var result = new double?[values.Count];
        var sum = 0d;

        for (var i = 0; i < values.Count; i++)
        {
            sum += values[i];
            if (i >= period) sum -= values[i - period];
            if (i >= period - 1) result[i] = sum / period;
        }

        return result;
    }

    public static double?[] Ema(IReadOnlyList<double> values, int period)
    {
        Guard(values, period);
        var result = new double?[values.Count];
        if (values.Count < period) return result;

        var k = 2d / (period + 1);
        var seed = 0d;
        for (var i = 0; i < period; i++) seed += values[i];
        var ema = seed / period;
        result[period - 1] = ema;

        for (var i = period; i < values.Count; i++)
        {
            ema = values[i] * k + ema * (1 - k);
            result[i] = ema;
        }

        return result;
    }

    /// <summary>Wilder's RSI. Values run 0–100; the first value lands at index <paramref name="period"/>.</summary>
    public static double?[] Rsi(IReadOnlyList<double> closes, int period = 14)
    {
        Guard(closes, period);
        var result = new double?[closes.Count];
        if (closes.Count <= period) return result;

        double gainSum = 0, lossSum = 0;
        for (var i = 1; i <= period; i++)
        {
            var change = closes[i] - closes[i - 1];
            if (change >= 0) gainSum += change; else lossSum -= change;
        }

        var avgGain = gainSum / period;
        var avgLoss = lossSum / period;
        result[period] = RsiFrom(avgGain, avgLoss);

        for (var i = period + 1; i < closes.Count; i++)
        {
            var change = closes[i] - closes[i - 1];
            var gain = change > 0 ? change : 0;
            var loss = change < 0 ? -change : 0;

            avgGain = (avgGain * (period - 1) + gain) / period;
            avgLoss = (avgLoss * (period - 1) + loss) / period;
            result[i] = RsiFrom(avgGain, avgLoss);
        }

        return result;
    }

    private static double RsiFrom(double avgGain, double avgLoss)
    {
        if (avgLoss == 0) return avgGain == 0 ? 50d : 100d;
        var rs = avgGain / avgLoss;
        return 100d - 100d / (1d + rs);
    }

    public static double[] TrueRange(
        IReadOnlyList<double> highs,
        IReadOnlyList<double> lows,
        IReadOnlyList<double> closes)
    {
        RequireSameLength(highs, lows, closes);
        var tr = new double[highs.Count];
        if (highs.Count == 0) return tr;

        tr[0] = highs[0] - lows[0];
        for (var i = 1; i < highs.Count; i++)
        {
            var hl = highs[i] - lows[i];
            var hc = Math.Abs(highs[i] - closes[i - 1]);
            var lc = Math.Abs(lows[i] - closes[i - 1]);
            tr[i] = Math.Max(hl, Math.Max(hc, lc));
        }

        return tr;
    }

    /// <summary>Wilder's ATR: simple mean of the first <paramref name="period"/> true ranges, smoothed after.</summary>
    public static double?[] Atr(
        IReadOnlyList<double> highs,
        IReadOnlyList<double> lows,
        IReadOnlyList<double> closes,
        int period = 14)
    {
        RequireSameLength(highs, lows, closes);
        Guard(closes, period);

        var tr = TrueRange(highs, lows, closes);
        var result = new double?[tr.Length];
        if (tr.Length < period) return result;

        var sum = 0d;
        for (var i = 0; i < period; i++) sum += tr[i];
        var atr = sum / period;
        result[period - 1] = atr;

        for (var i = period; i < tr.Length; i++)
        {
            atr = (atr * (period - 1) + tr[i]) / period;
            result[i] = atr;
        }

        return result;
    }

    /// <summary>Percent change over <paramref name="period"/> bars.</summary>
    public static double?[] RateOfChange(IReadOnlyList<double> values, int period)
    {
        Guard(values, period);
        var result = new double?[values.Count];

        for (var i = period; i < values.Count; i++)
        {
            var prior = values[i - period];
            if (prior != 0) result[i] = (values[i] - prior) / prior * 100d;
        }

        return result;
    }

    /// <summary>Annualised standard deviation of daily log returns, in percent.</summary>
    public static double?[] RealizedVolatility(IReadOnlyList<double> closes, int period = 20)
    {
        Guard(closes, period);
        var result = new double?[closes.Count];
        if (closes.Count <= period) return result;

        var returns = new double[closes.Count];
        for (var i = 1; i < closes.Count; i++)
            returns[i] = closes[i - 1] > 0 ? Math.Log(closes[i] / closes[i - 1]) : 0d;

        for (var i = period; i < closes.Count; i++)
        {
            var mean = 0d;
            for (var j = i - period + 1; j <= i; j++) mean += returns[j];
            mean /= period;

            var variance = 0d;
            for (var j = i - period + 1; j <= i; j++)
            {
                var d = returns[j] - mean;
                variance += d * d;
            }

            // Sample variance: we are estimating from a window, not measuring a population.
            variance /= period - 1;
            result[i] = Math.Sqrt(variance) * Math.Sqrt(252d) * 100d;
        }

        return result;
    }

    public static double?[] HighestHigh(IReadOnlyList<double> highs, int period)
    {
        Guard(highs, period);
        var result = new double?[highs.Count];

        for (var i = period - 1; i < highs.Count; i++)
        {
            var max = double.MinValue;
            for (var j = i - period + 1; j <= i; j++) max = Math.Max(max, highs[j]);
            result[i] = max;
        }

        return result;
    }

    public static double?[] LowestLow(IReadOnlyList<double> lows, int period)
    {
        Guard(lows, period);
        var result = new double?[lows.Count];

        for (var i = period - 1; i < lows.Count; i++)
        {
            var min = double.MaxValue;
            for (var j = i - period + 1; j <= i; j++) min = Math.Min(min, lows[j]);
            result[i] = min;
        }

        return result;
    }

    /// <summary>Last non-null value of an indicator series, or null if still warming up.</summary>
    public static double? Latest(this double?[] series)
    {
        for (var i = series.Length - 1; i >= 0; i--)
            if (series[i].HasValue) return series[i];
        return null;
    }

    /// <summary>Value at a specific index, or null when the index is outside the series.</summary>
    public static double? At(this double?[] series, int index) =>
        index >= 0 && index < series.Length ? series[index] : null;

    private static void Guard<T>(IReadOnlyList<T> values, int period)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period), period, "Period must be at least 1.");
    }

    private static void RequireSameLength(
        IReadOnlyList<double> a, IReadOnlyList<double> b, IReadOnlyList<double> c)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        ArgumentNullException.ThrowIfNull(c);
        if (a.Count != b.Count || b.Count != c.Count)
            throw new ArgumentException("High, low and close series must be the same length.");
    }
}
