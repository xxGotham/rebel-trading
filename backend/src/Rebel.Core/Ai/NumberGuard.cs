using System.Globalization;
using System.Text.RegularExpressions;

namespace Rebel.Core.Ai;

public sealed record NumberViolation(string Field, string Token, decimal Value)
{
    public override string ToString() => $"{Field}: '{Token}' is not in the input payload";
}

/// <summary>
/// Enforces the architectural rule that makes this whole system trustworthy:
/// <b>the model may not introduce numbers.</b>
/// <para>
/// Every numeric token in the model's prose must trace back to a number we put
/// in its payload. A hallucinated price target, an invented revenue figure, a
/// made-up percentage — all of it is caught here rather than being shown to a
/// user as if the pipeline had computed it.
/// </para>
/// <para>
/// The check is deliberately permissive about small integers and years, which
/// appear in ordinary prose ("three reasons", "Q3", "2026") and carry no risk of
/// being mistaken for market data.
/// </para>
/// </summary>
public static partial class NumberGuard
{
    /// <summary>Bare integers at or below this are treated as prose, not data.</summary>
    private const int ProseIntegerCeiling = 12;

    private const int MinPlausibleYear = 1900;
    private const int MaxPlausibleYear = 2100;

    /// <summary>Relative tolerance for matching a quoted number to an allowed one.</summary>
    private const decimal RelativeTolerance = 0.005m;

    [GeneratedRegex(@"-?\$?\d[\d,]*(?:\.\d+)?%?", RegexOptions.CultureInvariant)]
    private static partial Regex NumericToken();

    /// <summary>
    /// Builds the set of numbers the model is allowed to quote, by harvesting
    /// every numeric token from the serialised payload it was given. Harvesting
    /// from the payload text — rather than from a hand-maintained field list —
    /// means headlines and summaries are covered automatically.
    /// </summary>
    public static HashSet<decimal> BuildAllowList(params string[] payloads)
    {
        var allowed = new HashSet<decimal>();

        foreach (var payload in payloads)
        {
            if (string.IsNullOrEmpty(payload)) continue;

            foreach (Match match in NumericToken().Matches(payload))
            {
                if (!TryParse(match.Value, out var value)) continue;

                allowed.Add(value);
                allowed.Add(decimal.Round(value, 0));
                allowed.Add(decimal.Round(value, 1));
                allowed.Add(decimal.Round(value, 2));
            }
        }

        return allowed;
    }

    public static IReadOnlyList<NumberViolation> Inspect(
        ThesisSelection selection,
        HashSet<decimal> allowed)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(allowed);

        var violations = new List<NumberViolation>();

        foreach (var (field, text) in selection.ProseFields())
        {
            if (string.IsNullOrWhiteSpace(text)) continue;

            foreach (Match match in NumericToken().Matches(text))
            {
                if (!TryParse(match.Value, out var value)) continue;
                if (IsAllowed(value, allowed)) continue;

                violations.Add(new NumberViolation(field, match.Value.Trim(), value));
            }
        }

        return violations;
    }

    private static bool IsAllowed(decimal value, HashSet<decimal> allowed)
    {
        if (allowed.Contains(value)) return true;

        var abs = Math.Abs(value);

        // Ordinary prose numbers: "three catalysts", "two risks", "top 5".
        if (value == decimal.Truncate(value) && abs <= ProseIntegerCeiling) return true;

        // Years read as dates, not as market data.
        if (value == decimal.Truncate(value) && abs is >= MinPlausibleYear and <= MaxPlausibleYear)
            return true;

        // Rounding: the payload says 4.12%, the model writes "about 4.1%".
        foreach (var candidate in allowed)
        {
            var tolerance = Math.Max(0.01m, Math.Abs(candidate) * RelativeTolerance);
            if (Math.Abs(candidate - value) <= tolerance) return true;
        }

        return false;
    }

    private static bool TryParse(string token, out decimal value)
    {
        var cleaned = token.Replace("$", string.Empty)
                           .Replace(",", string.Empty)
                           .Replace("%", string.Empty)
                           .Trim();

        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
