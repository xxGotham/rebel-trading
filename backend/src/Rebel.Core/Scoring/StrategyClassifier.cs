using Rebel.Core.Signals;

namespace Rebel.Core.Scoring;

/// <summary>
/// Labels a candidate with the named strategy that best explains why it ranked.
/// <para>
/// This exists from day one rather than "later, when we do subscriptions",
/// because performance-by-strategy is only meaningful if every historical
/// recommendation carries a label. Retro-fitting labels onto a year of past
/// recs is guesswork.
/// </para>
/// </summary>
public static class StrategyClassifier
{
    public const string Momentum = "Rebel Momentum";
    public const string Catalyst = "Rebel Catalyst";
    public const string Breakout = "Rebel Breakouts";
    public const string Swing = "Rebel Swing";

    public static IReadOnlyList<string> All => [Momentum, Catalyst, Breakout, Swing];

    public static string Classify(SignalSet signals, IReadOnlyList<FactorContribution> factors)
    {
        var dominant = factors
            .Where(f => f.Contribution > 0)
            .OrderByDescending(f => f.Contribution)
            .FirstOrDefault();

        // Volume and a gap together read as "something happened overnight",
        // regardless of which one edged out the other in the composite.
        if (signals.RelativeVolume > 0 && signals.GapPercent > 1.5 &&
            factors.Any(f => f.Factor == "relativeVolume" && f.ZScore > 1.0))
            return Catalyst;

        return dominant?.Factor switch
        {
            "relativeVolume" or "gap" => Catalyst,
            "proximityToHigh" => signals.PercentOff20DayHigh < 2 ? Breakout : Swing,
            "momentum20" or "relativeStrength" or "sectorStrength" => Momentum,
            "trendQuality" => Swing,
            _ => Swing
        };
    }
}
