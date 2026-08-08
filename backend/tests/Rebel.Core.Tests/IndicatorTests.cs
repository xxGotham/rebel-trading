using Rebel.Core.Indicators;
using Xunit;

namespace Rebel.Core.Tests;

public class IndicatorTests
{
    [Fact]
    public void Sma_is_null_through_warmup_then_matches_the_rolling_mean()
    {
        double[] values = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

        var sma = TechnicalIndicators.Sma(values, 3);

        Assert.Null(sma[0]);
        Assert.Null(sma[1]);
        Assert.Equal(2d, sma[2]!.Value, 10);
        Assert.Equal(9d, sma[9]!.Value, 10);
        Assert.Equal(values.Length, sma.Length);
    }

    [Fact]
    public void Sma_stays_aligned_with_its_input()
    {
        // Alignment is the guard against pairing an indicator with the wrong bar,
        // which is how lookahead bias sneaks into a backtest silently.
        double[] values = [5, 5, 5, 5, 20];

        var sma = TechnicalIndicators.Sma(values, 2);

        Assert.Equal(5d, sma[3]!.Value, 10);
        Assert.Equal(12.5d, sma[4]!.Value, 10);
    }

    [Fact]
    public void Ema_seeds_from_the_simple_average_then_weights_recent_values()
    {
        double[] values = [1, 2, 3, 4, 5];

        var ema = TechnicalIndicators.Ema(values, 3);

        Assert.Null(ema[1]);
        Assert.Equal(2d, ema[2]!.Value, 10);
        // k = 2/(3+1) = 0.5 → 4*0.5 + 2*0.5 = 3
        Assert.Equal(3d, ema[3]!.Value, 10);
        Assert.Equal(4d, ema[4]!.Value, 10);
    }

    [Fact]
    public void Rsi_matches_a_hand_computed_wilder_series()
    {
        // period 2: changes are +1, -1, +1
        // seed  avgGain = 0.5, avgLoss = 0.5  → RSI 50
        // next  avgGain = 0.75, avgLoss = 0.25 → RS 3 → RSI 75
        double[] closes = [10, 11, 10, 11];

        var rsi = TechnicalIndicators.Rsi(closes, 2);

        Assert.Null(rsi[0]);
        Assert.Null(rsi[1]);
        Assert.Equal(50d, rsi[2]!.Value, 8);
        Assert.Equal(75d, rsi[3]!.Value, 8);
    }

    [Fact]
    public void Rsi_pins_to_100_on_an_unbroken_advance_and_0_on_an_unbroken_decline()
    {
        var rising = Enumerable.Range(1, 40).Select(i => (double)i).ToArray();
        var falling = rising.Reverse().ToArray();

        Assert.Equal(100d, TechnicalIndicators.Rsi(rising).Latest()!.Value, 6);
        Assert.Equal(0d, TechnicalIndicators.Rsi(falling).Latest()!.Value, 6);
    }

    [Fact]
    public void True_range_accounts_for_gaps_against_the_previous_close()
    {
        double[] highs = [10, 20];
        double[] lows = [8, 18];
        double[] closes = [9, 19];

        var tr = TechnicalIndicators.TrueRange(highs, lows, closes);

        Assert.Equal(2d, tr[0], 10);
        // The gap from a close of 9 up to a high of 20 dominates the 2-point bar range.
        Assert.Equal(11d, tr[1], 10);
    }

    [Fact]
    public void Atr_matches_a_hand_computed_wilder_series()
    {
        // TR = [2, 3, 2]; ATR[1] = 2.5; ATR[2] = (2.5 + 2) / 2 = 2.25
        double[] highs = [10, 12, 13];
        double[] lows = [8, 9, 11];
        double[] closes = [9, 11, 12];

        var atr = TechnicalIndicators.Atr(highs, lows, closes, 2);

        Assert.Null(atr[0]);
        Assert.Equal(2.5d, atr[1]!.Value, 10);
        Assert.Equal(2.25d, atr[2]!.Value, 10);
    }

    [Fact]
    public void Rate_of_change_is_a_percentage()
    {
        double[] values = [100, 110, 99];

        var roc = TechnicalIndicators.RateOfChange(values, 1);

        Assert.Null(roc[0]);
        Assert.Equal(10d, roc[1]!.Value, 10);
        Assert.Equal(-10d, roc[2]!.Value, 10);
    }

    [Fact]
    public void Realized_volatility_is_zero_for_a_flat_series_and_positive_otherwise()
    {
        var flat = Enumerable.Repeat(50d, 40).ToArray();
        var noisy = Enumerable.Range(0, 40).Select(i => 50d + (i % 2 == 0 ? 1 : -1)).ToArray();

        Assert.Equal(0d, TechnicalIndicators.RealizedVolatility(flat, 20).Latest()!.Value, 8);
        Assert.True(TechnicalIndicators.RealizedVolatility(noisy, 20).Latest()!.Value > 0);
    }

    [Fact]
    public void Highest_high_and_lowest_low_use_a_trailing_window()
    {
        double[] highs = [1, 9, 3, 4];
        double[] lows = [1, 9, 3, 4];

        Assert.Equal(9d, TechnicalIndicators.HighestHigh(highs, 3)[3]!.Value, 10);
        Assert.Equal(3d, TechnicalIndicators.LowestLow(lows, 2)[3]!.Value, 10);
    }

    [Fact]
    public void Latest_returns_null_while_the_series_is_still_warming_up()
    {
        double[] values = [1, 2];

        Assert.Null(TechnicalIndicators.Sma(values, 5).Latest());
    }

    [Fact]
    public void Mismatched_series_lengths_are_rejected_rather_than_silently_truncated()
    {
        double[] highs = [1, 2, 3];
        double[] lows = [1, 2];
        double[] closes = [1, 2, 3];

        Assert.Throws<ArgumentException>(() => TechnicalIndicators.Atr(highs, lows, closes, 2));
    }
}
