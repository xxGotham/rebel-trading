using Rebel.Core.Ai;
using Xunit;

namespace Rebel.Core.Tests;

public class NumberGuardTests
{
    private const string Payload =
        """
        {"symbol":"AMD","lastPrice":142.55,"gapPercent":3.2,"rsi14":64.8,
         "entryLow":141.20,"entryHigh":143.90,"target":152.40,"stopLoss":137.10,
         "news":[{"headline":"AMD lifts guidance by 12% on data centre demand"}]}
        """;

    private static ThesisSelection Selection(string thesis) => new()
    {
        Symbol = "AMD",
        Selected = true,
        Thesis = thesis
    };

    [Fact]
    public void Numbers_taken_from_the_payload_are_allowed()
    {
        var allowed = NumberGuard.BuildAllowList(Payload);

        var violations = NumberGuard.Inspect(
            Selection("Price is 142.55 with the target at 152.40 and a stop at 137.10."), allowed);

        Assert.Empty(violations);
    }

    [Fact]
    public void An_invented_price_target_is_caught()
    {
        // The failure this whole class exists to prevent: a fabricated figure
        // presented to the user as if the pipeline had computed it.
        var allowed = NumberGuard.BuildAllowList(Payload);

        var violations = NumberGuard.Inspect(
            Selection("Analysts see this reaching $210.00 by year end."), allowed);

        Assert.Single(violations);
        Assert.Equal(210.00m, violations[0].Value);
    }

    [Fact]
    public void An_invented_statistic_is_caught()
    {
        var allowed = NumberGuard.BuildAllowList(Payload);

        var violations = NumberGuard.Inspect(
            Selection("Revenue grew 47.3% last quarter."), allowed);

        Assert.Single(violations);
        Assert.Equal(47.3m, violations[0].Value);
    }

    [Fact]
    public void Small_integers_and_years_read_as_prose_not_data()
    {
        var allowed = NumberGuard.BuildAllowList(Payload);

        var violations = NumberGuard.Inspect(
            Selection("There are 3 reasons to like this into 2026, and 2 that concern me."), allowed);

        Assert.Empty(violations);
    }

    [Fact]
    public void Reasonable_rounding_of_a_supplied_figure_is_tolerated()
    {
        var allowed = NumberGuard.BuildAllowList(Payload);

        var violations = NumberGuard.Inspect(
            Selection("It gapped about 3.2% and RSI is around 65."), allowed);

        Assert.Empty(violations);
    }

    [Fact]
    public void Numbers_inside_supplied_headlines_are_allowed()
    {
        // The allow-list is harvested from the payload text, so figures quoted in
        // a headline we handed the model are legitimate to repeat.
        var allowed = NumberGuard.BuildAllowList(Payload);

        var violations = NumberGuard.Inspect(
            Selection("Guidance went up 12% on data centre demand."), allowed);

        Assert.Empty(violations);
    }

    [Fact]
    public void Every_prose_field_is_inspected_not_just_the_thesis()
    {
        var allowed = NumberGuard.BuildAllowList(Payload);

        var selection = new ThesisSelection
        {
            Symbol = "AMD",
            Selected = true,
            Thesis = "Clean.",
            BullCase = "Could add 35.7% from here.",
            BearCase = "Clean.",
            Risks = "A drop to 88.25 would end it.",
            CatalystSummary = "Clean.",
            WhyThisOne = "Clean."
        };

        var violations = NumberGuard.Inspect(selection, allowed);

        Assert.Equal(2, violations.Count);
        Assert.Contains(violations, v => v.Field == nameof(ThesisSelection.BullCase));
        Assert.Contains(violations, v => v.Field == nameof(ThesisSelection.Risks));
    }

    [Fact]
    public void Currency_and_thousands_separators_are_parsed()
    {
        var allowed = NumberGuard.BuildAllowList("{\"averageVolume\":1234567}");

        Assert.Empty(NumberGuard.Inspect(Selection("Volume around 1,234,567 shares."), allowed));
        Assert.Single(NumberGuard.Inspect(Selection("Volume around 9,876,543 shares."), allowed));
    }

    [Fact]
    public void Prose_with_no_numbers_always_passes()
    {
        var allowed = NumberGuard.BuildAllowList(Payload);

        var violations = NumberGuard.Inspect(
            Selection("Volume is running well above its recent average and the trend is intact."), allowed);

        Assert.Empty(violations);
    }
}
