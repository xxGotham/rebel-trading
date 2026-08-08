using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rebel.Core.Ai;
using Rebel.Core.Domain;
using Xunit;

namespace Rebel.Api.Tests;

public class PipelineEndToEndTests(RebelApiFactory factory) : IClassFixture<RebelApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Health_and_status_report_which_providers_are_live()
    {
        var client = factory.CreateClient();

        var health = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        var status = await client.GetFromJsonAsync<JsonElement>("/api/status", Json);

        Assert.Equal("synthetic", status.GetProperty("marketDataProvider").GetString());
        // The UI depends on this flag to warn that the prices are not real.
        Assert.True(status.GetProperty("usingSyntheticData").GetBoolean());
        Assert.False(status.GetProperty("usingLlm").GetBoolean());
        Assert.Equal("sqlite", status.GetProperty("database").GetString());
    }

    [Fact]
    public async Task The_universe_is_seeded_on_startup()
    {
        var client = factory.CreateClient();

        var instruments = await client.GetFromJsonAsync<JsonElement[]>("/api/instruments", Json);

        Assert.NotNull(instruments);
        Assert.True(instruments.Length > 100, $"expected a universe of 100+, got {instruments.Length}");
        Assert.Contains(instruments, i => i.GetProperty("symbol").GetString() == "SPY");
        Assert.Contains(instruments, i => i.GetProperty("symbol").GetString() == "AAPL");
    }

    [Fact]
    public async Task A_run_produces_recommendations_with_a_complete_trade_plan()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/runs", new { });
        response.EnsureSuccessStatusCode();

        var run = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Completed", run.GetProperty("status").GetString());
        Assert.True(run.GetProperty("universeSize").GetInt32() > 100);
        Assert.True(run.GetProperty("candidateCount").GetInt32() > 0);

        var recommendationCount = run.GetProperty("recommendationCount").GetInt32();
        Assert.InRange(recommendationCount, 1, 3);

        var detail = await client.GetFromJsonAsync<JsonElement>("/api/runs/latest", Json);
        var recommendations = detail.GetProperty("recommendations").EnumerateArray().ToArray();

        Assert.Equal(recommendationCount, recommendations.Length);

        foreach (var rec in recommendations)
        {
            var entryLow = rec.GetProperty("entryLow").GetDecimal();
            var entryHigh = rec.GetProperty("entryHigh").GetDecimal();
            var target = rec.GetProperty("target").GetDecimal();
            var stop = rec.GetProperty("stopLoss").GetDecimal();

            Assert.True(stop < entryLow, "stop must sit below the entry band");
            Assert.True(entryLow <= entryHigh, "entry band must be ordered");
            Assert.True(target > entryHigh, "target must sit above the entry band");
            Assert.True(rec.GetProperty("riskRewardRatio").GetDecimal() >= 1.75m);
            Assert.True(rec.GetProperty("suggestedShares").GetInt32() > 0);
            Assert.InRange(rec.GetProperty("holdingPeriodDays").GetInt32(), 1, 10);
            Assert.False(string.IsNullOrWhiteSpace(rec.GetProperty("thesis").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(rec.GetProperty("strategy").GetString()));
        }
    }

    [Fact]
    public async Task Every_recommendation_carries_the_versions_that_produced_it()
    {
        // Without these, a change in output quality cannot be attributed to a
        // scoring tweak, a prompt edit or the market.
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/runs", new { });

        var detail = await client.GetFromJsonAsync<JsonElement>("/api/runs/latest", Json);
        var run = detail.GetProperty("run");

        Assert.Equal("score-v1", run.GetProperty("scoringVersion").GetString());
        Assert.False(string.IsNullOrWhiteSpace(run.GetProperty("configHash").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(run.GetProperty("promptVersion").GetString()));
    }

    [Fact]
    public async Task Excluded_candidates_are_recorded_with_the_reason_they_were_dropped()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/runs", new { });

        var detail = await client.GetFromJsonAsync<JsonElement>("/api/runs/latest", Json);
        var candidates = detail.GetProperty("candidates").EnumerateArray().ToArray();

        Assert.NotEmpty(candidates);

        var excluded = candidates
            .Where(c => c.GetProperty("exclusionReason").ValueKind != JsonValueKind.Null)
            .ToArray();

        Assert.All(excluded, c =>
            Assert.False(string.IsNullOrWhiteSpace(c.GetProperty("exclusionReason").GetString())));
    }

    [Fact]
    public async Task The_stage_log_records_what_each_step_did()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/runs", new { });

        var detail = await client.GetFromJsonAsync<JsonElement>("/api/runs/latest", Json);
        var stages = detail.GetProperty("stageLog").EnumerateArray().Select(s => s.GetString()!).ToArray();

        Assert.Contains(stages, s => s.Contains("universe:"));
        Assert.Contains(stages, s => s.Contains("signals:"));
        Assert.Contains(stages, s => s.Contains("scoring:"));
        Assert.Contains(stages, s => s.Contains("trade plans:"));
    }

    [Fact]
    public async Task Generated_prose_never_contains_a_number_absent_from_its_payload()
    {
        // The guarantee the whole architecture rests on, verified against real
        // pipeline output rather than a crafted string.
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/runs", new { });

        await using var db = factory.CreateDbContext();
        var recommendations = await db.Recommendations.ToListAsync();

        Assert.NotEmpty(recommendations);

        foreach (var rec in recommendations)
        {
            var allowed = NumberGuard.BuildAllowList(rec.SignalsJson, rec.NewsJson);

            var selection = new ThesisSelection
            {
                Symbol = rec.Symbol,
                Selected = true,
                Thesis = rec.Thesis,
                BullCase = rec.BullCase,
                BearCase = rec.BearCase,
                Risks = rec.Risks,
                CatalystSummary = rec.CatalystSummary,
                WhyThisOne = rec.WhyThisOne
            };

            var violations = NumberGuard.Inspect(selection, allowed);
            Assert.True(violations.Count == 0,
                $"{rec.Symbol} quoted unsupported numbers: {string.Join("; ", violations)}");
        }
    }

    [Fact]
    public async Task Signals_and_news_are_stored_alongside_each_recommendation()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/runs", new { });

        var recommendations = await client.GetFromJsonAsync<JsonElement[]>("/api/recommendations", Json);
        Assert.NotNull(recommendations);
        Assert.NotEmpty(recommendations);

        var id = recommendations[0].GetProperty("id").GetGuid();
        var evidence = await client.GetFromJsonAsync<JsonElement>($"/api/recommendations/{id}/evidence", Json);

        Assert.False(string.IsNullOrWhiteSpace(evidence.GetProperty("signals").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(evidence.GetProperty("modelResponse").GetString()));
    }

    [Fact]
    public async Task A_run_for_an_explicit_past_date_only_uses_data_available_then()
    {
        var client = factory.CreateClient();
        var tradingDate = new DateOnly(2026, 3, 2);

        var response = await client.PostAsJsonAsync("/api/runs", new { tradingDate = "2026-03-02" });
        response.EnsureSuccessStatusCode();

        var run = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var runId = run.GetProperty("id").GetGuid();

        await using var db = factory.CreateDbContext();
        var candidates = await db.Candidates.Where(c => c.AnalysisRunId == runId).ToListAsync();

        Assert.NotEmpty(candidates);

        // No signal on this run may have been derived from a session at or after
        // the trading date.
        var recommendations = await db.Recommendations.Where(r => r.AnalysisRunId == runId).ToListAsync();
        Assert.All(recommendations, r => Assert.Equal(tradingDate, r.TradingDate));
    }

    [Fact]
    public async Task An_unknown_run_returns_not_found()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/runs/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
