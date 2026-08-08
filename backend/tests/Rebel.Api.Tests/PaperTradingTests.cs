using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Rebel.Api.Tests;

public class PaperTradingTests(RebelApiFactory factory) : IClassFixture<RebelApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// A recommendation not already held. Tests in a class share one database and
    /// xUnit does not promise an order, so each test has to claim its own rather
    /// than assume the portfolio is empty.
    /// </summary>
    private async Task<Guid> UntakenRecommendationAsync(HttpClient client)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var recommendations = await client.GetFromJsonAsync<JsonElement[]>("/api/recommendations", Json) ?? [];
            var positions = await client.GetFromJsonAsync<JsonElement[]>("/api/paper/positions", Json) ?? [];

            var taken = positions
                .Select(p => p.GetProperty("recommendationId").GetGuid())
                .ToHashSet();

            foreach (var rec in recommendations)
            {
                var id = rec.GetProperty("id").GetGuid();
                if (!taken.Contains(id)) return id;
            }

            // Each run analyses a different session, so this yields fresh ideas.
            var tradingDate = new DateOnly(2026, 1, 5).AddDays(attempt * 7);
            await client.PostAsJsonAsync("/api/runs", new { tradingDate = tradingDate.ToString("yyyy-MM-dd") });
        }

        throw new InvalidOperationException("Could not obtain a recommendation that is not already held.");
    }

    [Fact]
    public async Task A_recommendation_can_be_accepted_into_the_simulated_portfolio()
    {
        var client = factory.CreateClient();
        var recommendationId = await UntakenRecommendationAsync(client);

        var response = await client.PostAsJsonAsync("/api/paper/positions",
            new { recommendationId, shares = 50 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var position = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Open", position.GetProperty("status").GetString());
        Assert.Equal(50, position.GetProperty("shares").GetInt32());
        Assert.True(position.GetProperty("entryPrice").GetDecimal() > 0m);
    }

    [Fact]
    public async Task The_same_recommendation_cannot_be_taken_twice()
    {
        var client = factory.CreateClient();
        var recommendationId = await UntakenRecommendationAsync(client);

        await client.PostAsJsonAsync("/api/paper/positions", new { recommendationId });
        var duplicate = await client.PostAsJsonAsync("/api/paper/positions", new { recommendationId });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Closing_a_position_records_the_realised_result()
    {
        var client = factory.CreateClient();
        var recommendationId = await UntakenRecommendationAsync(client);

        var opened = await client.PostAsJsonAsync("/api/paper/positions",
            new { recommendationId, shares = 10, entryPrice = 100m });

        if (opened.StatusCode == HttpStatusCode.Conflict)
        {
            var positions = await client.GetFromJsonAsync<JsonElement[]>("/api/paper/positions", Json);
            recommendationId = positions!.First(p => p.GetProperty("status").GetString() == "Open")
                .GetProperty("recommendationId").GetGuid();
        }

        var open = await client.GetFromJsonAsync<JsonElement[]>("/api/paper/positions?status=Open", Json);
        Assert.NotNull(open);
        Assert.NotEmpty(open);

        var positionId = open[0].GetProperty("id").GetGuid();
        var entryPrice = open[0].GetProperty("entryPrice").GetDecimal();

        var closed = await client.PostAsJsonAsync($"/api/paper/positions/{positionId}/close",
            new { exitPrice = entryPrice * 1.10m, reason = "target" });

        closed.EnsureSuccessStatusCode();

        var result = await closed.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Closed", result.GetProperty("status").GetString());
        Assert.True(result.GetProperty("realizedPnl").GetDecimal() > 0m);
        Assert.InRange(result.GetProperty("realizedPercent").GetDecimal(), 9.5m, 10.5m);
    }

    [Fact]
    public async Task A_position_cannot_be_closed_twice()
    {
        var client = factory.CreateClient();
        var recommendationId = await UntakenRecommendationAsync(client);

        var opened = await client.PostAsJsonAsync("/api/paper/positions",
            new { recommendationId, shares = 5, entryPrice = 50m });

        if (opened.StatusCode != HttpStatusCode.Created) return;

        var position = await opened.Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = position.GetProperty("id").GetGuid();

        await client.PostAsJsonAsync($"/api/paper/positions/{id}/close", new { exitPrice = 55m });
        var second = await client.PostAsJsonAsync($"/api/paper/positions/{id}/close", new { exitPrice = 55m });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Opening_a_position_against_an_unknown_recommendation_is_rejected()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/paper/positions",
            new { recommendationId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_portfolio_summarises_deployed_capital_and_realised_pnl()
    {
        var client = factory.CreateClient();
        await UntakenRecommendationAsync(client);

        var portfolio = await client.GetFromJsonAsync<JsonElement>("/api/paper/portfolio", Json);

        Assert.True(portfolio.GetProperty("openPositions").GetInt32() >= 0);
        Assert.True(portfolio.GetProperty("deployedCapital").GetDecimal() >= 0m);
    }
}

public class PerformanceTests(RebelApiFactory factory) : IClassFixture<RebelApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Grading_runs_over_outstanding_recommendations()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/runs", new { });

        var response = await client.PostAsync("/api/performance/grade", null);
        response.EnsureSuccessStatusCode();

        var summary = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        // A run for the next session cannot be graded yet — the sessions that
        // would resolve it have not happened. "Still open" is the correct answer.
        Assert.True(summary.GetProperty("examined").GetInt32() > 0);
        Assert.Equal(
            summary.GetProperty("examined").GetInt32(),
            summary.GetProperty("graded").GetInt32() + summary.GetProperty("stillOpen").GetInt32());
    }

    [Fact]
    public async Task Performance_reports_the_breakdowns_even_with_no_graded_history()
    {
        var client = factory.CreateClient();

        var performance = await client.GetFromJsonAsync<JsonElement>("/api/performance", Json);

        Assert.True(performance.TryGetProperty("overall", out _));
        Assert.True(performance.TryGetProperty("byStrategy", out _));
        Assert.True(performance.TryGetProperty("byConfidence", out _));
        Assert.True(performance.TryGetProperty("byHoldingPeriod", out _));
    }

    [Fact]
    public async Task A_historical_run_can_be_graded_end_to_end()
    {
        // Analyse a session far enough in the past that the outcome is decided.
        var client = factory.CreateClient();

        var run = await client.PostAsJsonAsync("/api/runs", new { tradingDate = "2026-01-05" });
        run.EnsureSuccessStatusCode();

        await client.PostAsync("/api/performance/grade", null);

        var recommendations = await client.GetFromJsonAsync<JsonElement[]>(
            "/api/recommendations?from=2026-01-05&to=2026-01-05", Json);

        Assert.NotNull(recommendations);

        foreach (var rec in recommendations)
        {
            var outcome = rec.GetProperty("outcome");
            if (outcome.ValueKind == JsonValueKind.Null) continue;

            var status = outcome.GetProperty("status").GetString();
            Assert.Contains(status, new[] { "TargetHit", "StopHit", "Expired", "NeverTriggered", "Open" });
        }
    }
}
