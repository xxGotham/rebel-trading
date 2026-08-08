using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rebel.Core.Abstractions;
using Rebel.Core.Ai;
using Rebel.Core.Domain;
using Rebel.Core.Indicators;
using Rebel.Core.Market;
using Rebel.Core.Scoring;
using Rebel.Core.Signals;
using Rebel.Core.Trading;
using Rebel.Infrastructure.Configuration;
using Rebel.Infrastructure.Persistence;

namespace Rebel.Infrastructure.Pipeline;

public sealed record RunRequest
{
    /// <summary>Session to analyse. Defaults to the next actionable session.</summary>
    public DateOnly? TradingDate { get; init; }

    public RunTrigger Trigger { get; init; } = RunTrigger.Manual;

    /// <summary>Pull fresh bars and snapshots before scoring. Off replays what is already stored.</summary>
    public bool RefreshMarketData { get; init; } = true;
}

/// <summary>
/// The deterministic pipeline, start to finish:
/// <c>universe → market data → signals → score → rank → select → news → trade plans
/// → AI prose → validate → persist</c>.
/// <para>
/// The ordering is the design. Everything that decides <em>what</em> to
/// recommend happens before the model is called; the model runs last and only
/// gets to describe a decision that has already been made and priced. Moving
/// the AI call any earlier would make the system an LLM stock picker with a
/// quant veneer, which is exactly what it is meant not to be.
/// </para>
/// </summary>
public sealed class AnalysisPipeline(
    RebelDbContext db,
    MarketDataIngestionService ingestion,
    IMarketDataProvider marketData,
    INewsProvider news,
    IThesisGenerator thesisGenerator,
    ScoringEngine scoring,
    TradePlanBuilder planner,
    IClock clock,
    IOptions<PipelineOptions> pipelineOptions,
    ILogger<AnalysisPipeline> log)
{
    public const string Version = "pipeline-v1";

    private readonly PipelineOptions _options = pipelineOptions.Value;
    private readonly List<string> _stages = [];

    public async Task<AnalysisRun> RunAsync(RunRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = clock.UtcNow;
        var tradingDate = request.TradingDate ?? MarketCalendar.CurrentTradingDate(now);

        var run = new AnalysisRun
        {
            Id = Guid.NewGuid(),
            StartedAtUtc = now,
            Status = RunStatus.Running,
            Trigger = request.Trigger,
            TradingDate = tradingDate,
            ScoringVersion = scoring.Weights.Version,
            PromptVersion = thesisGenerator.PromptVersion,
            PipelineVersion = Version,
            ConfigHash = scoring.Weights.ComputeHash(),
            Model = thesisGenerator.Model
        };

        db.AnalysisRuns.Add(run);
        await db.SaveChangesAsync(ct);

        try
        {
            await ExecuteAsync(run, request, tradingDate, ct);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Analysis run {RunId} failed", run.Id);
            run.Status = RunStatus.Failed;
            run.ErrorMessage = ex.Message;
        }
        finally
        {
            run.FinishedAtUtc = clock.UtcNow;
            run.StageLog = string.Join('\n', _stages);
            await db.SaveChangesAsync(ct);
        }

        return run;
    }

    private async Task ExecuteAsync(AnalysisRun run, RunRequest request, DateOnly tradingDate, CancellationToken ct)
    {
        // 1. Universe
        await ingestion.SyncUniverseAsync(ct);
        var instruments = await db.Instruments.Where(i => i.IsActive).ToListAsync(ct);
        var tradable = instruments.Where(i => !i.IsBenchmark).ToList();
        run.UniverseSize = tradable.Count;
        Stage($"universe: {tradable.Count} tradable, {instruments.Count - tradable.Count} benchmark/sector");

        // 2. Market data
        var historyFrom = tradingDate.AddDays(-_options.HistoryDays);
        if (request.RefreshMarketData)
        {
            var ingested = await ingestion.IngestBarsAsync(
                instruments, historyFrom, MarketCalendar.PreviousTradingDay(tradingDate), ct);
            Stage($"bars: {ingested}");
        }
        else
        {
            Stage("bars: refresh skipped, replaying stored history");
        }

        var barsByInstrument = await LoadBarsAsync(historyFrom, tradingDate, ct);
        Stage($"history: {barsByInstrument.Sum(kv => kv.Value.Count)} bars across {barsByInstrument.Count} instruments");

        // 3. Snapshots — the only view of the pre-market session
        var snapshots = new Dictionary<string, Snapshot>(StringComparer.OrdinalIgnoreCase);
        if (request.RefreshMarketData)
        {
            snapshots = (await marketData.GetSnapshotsAsync(
                    tradable.Select(i => i.Symbol).ToArray(), ct))
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

            await PersistSnapshotsAsync(run, tradable, snapshots, ct);
            Stage($"snapshots: {snapshots.Count}/{tradable.Count} symbols quoted");
        }

        // 4. Signals
        var bySymbol = instruments.ToDictionary(i => i.Symbol, StringComparer.OrdinalIgnoreCase);
        var signals = new List<SignalSet>();
        var skipped = 0;

        foreach (var instrument in tradable)
        {
            if (!barsByInstrument.TryGetValue(instrument.Id, out var bars)) { skipped++; continue; }

            var input = new SignalInput
            {
                Instrument = instrument,
                Bars = bars,
                Snapshot = snapshots.GetValueOrDefault(instrument.Symbol),
                MarketBars = BarsFor(bySymbol, barsByInstrument, "SPY"),
                SectorBars = BarsFor(bySymbol, barsByInstrument, instrument.SectorEtf)
            };

            var result = SignalCalculator.Calculate(input, tradingDate, run.StartedAtUtc);
            if (result.Signals is not null) signals.Add(result.Signals);
            else skipped++;
        }

        Stage($"signals: {signals.Count} computed, {skipped} skipped for missing or short history");

        if (signals.Count < _options.MinimumUniverseCoverage)
            throw new InvalidOperationException(
                $"Only {signals.Count} instruments produced signals, below the minimum coverage of " +
                $"{_options.MinimumUniverseCoverage}. Refusing to rank on a broken data set.");

        // 5. Score and rank
        var scored = scoring.Score(signals);
        var included = scored.Count(c => c.Included);
        Stage($"scoring: {included} passed filters, {scored.Count - included} excluded ({scoring.Weights.Version})");

        await PersistCandidatesAsync(run, scored, bySymbol, ct);
        run.CandidateCount = included;

        var shortlist = scoring.SelectTop(scored, _options.CandidateCount);
        Stage($"shortlist: top {shortlist.Count} above score floor {scoring.Weights.MinimumScore:F0}");

        if (shortlist.Count == 0)
        {
            run.Status = RunStatus.CompletedEmpty;
            Stage("no candidate cleared the score floor — recommending nothing");
            return;
        }

        // 6. News for the shortlist only — the expensive calls are scoped to what survived
        var newsBySymbol = await FetchNewsAsync(shortlist, bySymbol, ct);
        Stage($"news: {newsBySymbol.Sum(kv => kv.Value.Count)} headlines across {newsBySymbol.Count} symbols");

        // 7. Trade plans — every number a user will see is fixed here, before the model runs
        var planned = new List<(ScoredCandidate Candidate, TradePlan Plan)>();
        foreach (var candidate in shortlist)
        {
            var result = planner.Build(candidate.Signals);
            if (result.Plan is not null) planned.Add((candidate, result.Plan));
            else Stage($"  {candidate.Symbol}: no tradable plan — {result.Rejection}");
        }

        Stage($"trade plans: {planned.Count}/{shortlist.Count} produced a valid risk/reward");

        if (planned.Count == 0)
        {
            run.Status = RunStatus.CompletedEmpty;
            return;
        }

        // 8. AI prose
        var thesisRequest = new ThesisRequest
        {
            TradingDate = tradingDate,
            MaxSelections = _options.MaxRecommendations,
            MarketContext = BuildMarketContext(signals, bySymbol, barsByInstrument, tradingDate),
            Candidates = planned.Select(p => ToThesisCandidate(p.Candidate, p.Plan, newsBySymbol)).ToArray()
        };

        var thesis = await thesisGenerator.GenerateAsync(thesisRequest, ct);
        run.Model = thesis.Model;
        run.PromptTokens = thesis.PromptTokens;
        run.CompletionTokens = thesis.CompletionTokens;
        Stage($"ai: {thesis.Model} returned {thesis.Selections.Count(s => s.Selected)} selections " +
              $"({thesis.PromptTokens}+{thesis.CompletionTokens} tokens)");

        // 9. Persist recommendations
        var count = await PersistRecommendationsAsync(
            run, planned, thesis, newsBySymbol, bySymbol, thesisRequest, ct);

        run.RecommendationCount = count;
        run.Status = count > 0 ? RunStatus.Completed : RunStatus.CompletedEmpty;
        Stage($"recommendations: {count} persisted");
    }

    private async Task<Dictionary<int, List<DailyBar>>> LoadBarsAsync(
        DateOnly from, DateOnly tradingDate, CancellationToken ct)
    {
        // Loaded strictly before the trading date: the signal layer enforces this
        // too, but keeping the future out of memory entirely means a later bug
        // cannot quietly reintroduce lookahead.
        var bars = await db.DailyBars
            .Where(b => b.Date >= from && b.Date < tradingDate)
            .OrderBy(b => b.Date)
            .ToListAsync(ct);

        return bars.GroupBy(b => b.InstrumentId).ToDictionary(g => g.Key, g => g.ToList());
    }

    private static IReadOnlyList<DailyBar> BarsFor(
        Dictionary<string, Instrument> bySymbol,
        Dictionary<int, List<DailyBar>> barsByInstrument,
        string symbol) =>
        !string.IsNullOrEmpty(symbol) &&
        bySymbol.TryGetValue(symbol, out var instrument) &&
        barsByInstrument.TryGetValue(instrument.Id, out var bars)
            ? bars
            : [];

    private async Task PersistSnapshotsAsync(
        AnalysisRun run,
        IReadOnlyList<Instrument> instruments,
        IReadOnlyDictionary<string, Snapshot> snapshots,
        CancellationToken ct)
    {
        foreach (var instrument in instruments)
        {
            if (!snapshots.TryGetValue(instrument.Symbol, out var snapshot)) continue;

            snapshot.InstrumentId = instrument.Id;
            snapshot.AnalysisRunId = run.Id;
            db.Snapshots.Add(snapshot);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task PersistCandidatesAsync(
        AnalysisRun run,
        IReadOnlyList<ScoredCandidate> scored,
        Dictionary<string, Instrument> bySymbol,
        CancellationToken ct)
    {
        foreach (var c in scored)
        {
            if (!bySymbol.TryGetValue(c.Symbol, out var instrument)) continue;
            var s = c.Signals;

            db.Candidates.Add(new Candidate
            {
                AnalysisRunId = run.Id,
                InstrumentId = instrument.Id,
                Symbol = c.Symbol,
                Rank = c.Rank,
                Score = c.Score,
                Selected = false,
                ExclusionReason = c.ExclusionReason,
                LastPrice = s.LastPrice,
                PreviousClose = s.PreviousClose,
                GapPercent = s.GapPercent,
                RelativeVolume = s.RelativeVolume,
                Rsi14 = s.Rsi14,
                Atr14 = s.Atr14,
                AtrPercent = s.AtrPercent,
                Sma20 = s.Sma20,
                Sma50 = s.Sma50,
                Momentum20 = s.Momentum20,
                PercentOff20DayHigh = s.PercentOff20DayHigh,
                RealizedVolatility20 = s.RealizedVolatility20,
                RelativeStrengthVsMarket = s.RelativeStrengthVsMarket,
                SectorStrength = s.SectorStrength,
                AverageVolume20 = s.AverageVolume20,
                FactorBreakdownJson = JsonSerializer.Serialize(
                    c.Factors.ToDictionary(
                        f => f.Factor,
                        f => new { raw = Round(f.Raw), z = Round(f.ZScore), weight = f.Weight, contribution = Round(f.Contribution) }),
                    ThesisPromptJson)
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<Dictionary<string, IReadOnlyList<NewsItem>>> FetchNewsAsync(
        IReadOnlyList<ScoredCandidate> shortlist,
        Dictionary<string, Instrument> bySymbol,
        CancellationToken ct)
    {
        var symbols = shortlist.Select(c => c.Symbol).ToArray();
        var since = clock.UtcNow.AddHours(-_options.NewsLookbackHours);

        var fetched = await news.GetNewsAsync(symbols, since, _options.NewsPerSymbol, ct);
        var result = new Dictionary<string, IReadOnlyList<NewsItem>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (symbol, items) in fetched)
        {
            if (items.Count == 0 || !bySymbol.TryGetValue(symbol, out var instrument)) continue;

            var externalIds = items.Select(i => i.ExternalId).ToArray();
            var known = await db.NewsItems
                .Where(n => n.InstrumentId == instrument.Id && externalIds.Contains(n.ExternalId))
                .Select(n => n.ExternalId)
                .ToListAsync(ct);

            foreach (var item in items)
            {
                if (known.Contains(item.ExternalId)) continue;
                item.InstrumentId = instrument.Id;
                db.NewsItems.Add(item);
            }

            result[symbol] = items;
        }

        await db.SaveChangesAsync(ct);
        return result;
    }

    private static ThesisCandidate ToThesisCandidate(
        ScoredCandidate candidate,
        TradePlan plan,
        IReadOnlyDictionary<string, IReadOnlyList<NewsItem>> newsBySymbol)
    {
        var s = candidate.Signals;
        var headlines = newsBySymbol.GetValueOrDefault(candidate.Symbol) ?? [];

        return new ThesisCandidate
        {
            Symbol = s.Symbol,
            CompanyName = s.Symbol,
            Sector = s.Sector,
            Strategy = candidate.Strategy,
            Rank = candidate.Rank,
            Score = Round(candidate.Score),
            Confidence = ConfidenceScale.FromScore(candidate.Score).ToString(),
            LastPrice = s.LastPrice,
            PreviousClose = s.PreviousClose,
            GapPercent = Round(s.GapPercent),
            RelativeVolume = Round(s.RelativeVolume),
            Rsi14 = Round(s.Rsi14),
            Atr14 = Round(s.Atr14),
            AtrPercent = Round(s.AtrPercent),
            Sma20 = Round(s.Sma20),
            Sma50 = Round(s.Sma50),
            Momentum20Percent = Round(s.Momentum20),
            PercentOff20DayHigh = Round(s.PercentOff20DayHigh),
            RealizedVolatility = Round(s.RealizedVolatility20),
            RelativeStrengthVsMarket = Round(s.RelativeStrengthVsMarket),
            SectorStrength = Round(s.SectorStrength),
            AverageVolume20 = s.AverageVolume20,
            EntryLow = plan.EntryLow,
            EntryHigh = plan.EntryHigh,
            Target = plan.Target,
            StopLoss = plan.StopLoss,
            RiskRewardRatio = plan.RiskRewardRatio,
            HoldingPeriodDays = plan.HoldingPeriodDays,
            News = headlines.Select(n => new ThesisNewsItem
            {
                Id = n.ExternalId,
                Headline = n.Headline,
                Summary = n.Summary,
                Source = n.Source,
                Url = n.Url,
                PublishedAtUtc = n.PublishedAtUtc
            }).ToArray()
        };
    }

    private async Task<int> PersistRecommendationsAsync(
        AnalysisRun run,
        IReadOnlyList<(ScoredCandidate Candidate, TradePlan Plan)> planned,
        ThesisResponse thesis,
        IReadOnlyDictionary<string, IReadOnlyList<NewsItem>> newsBySymbol,
        Dictionary<string, Instrument> bySymbol,
        ThesisRequest thesisRequest,
        CancellationToken ct)
    {
        var selections = thesis.Selections
            .Where(s => s.Selected)
            .ToDictionary(s => s.Symbol, StringComparer.OrdinalIgnoreCase);

        var payload = new Dictionary<string, ThesisCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in thesisRequest.Candidates) payload[c.Symbol] = c;

        var count = 0;

        foreach (var (candidate, plan) in planned.OrderBy(p => p.Candidate.Rank))
        {
            if (count >= _options.MaxRecommendations) break;
            if (!selections.TryGetValue(candidate.Symbol, out var selection)) continue;
            if (!bySymbol.TryGetValue(candidate.Symbol, out var instrument)) continue;

            var notes = Describe(selection);
            var headlines = newsBySymbol.GetValueOrDefault(candidate.Symbol) ?? [];

            db.Recommendations.Add(new Recommendation
            {
                Id = Guid.NewGuid(),
                AnalysisRunId = run.Id,
                InstrumentId = instrument.Id,
                Symbol = candidate.Symbol,
                CreatedAtUtc = clock.UtcNow,
                TradingDate = run.TradingDate,
                Direction = TradeDirection.Long,

                ReferencePrice = plan.ReferencePrice,
                EntryLow = plan.EntryLow,
                EntryHigh = plan.EntryHigh,
                Target = plan.Target,
                StopLoss = plan.StopLoss,
                RiskRewardRatio = plan.RiskRewardRatio,
                StopDistancePercent = plan.StopDistancePercent,
                TargetDistancePercent = plan.TargetDistancePercent,
                SuggestedShares = plan.SuggestedShares,
                SuggestedRiskAmount = plan.SuggestedRiskAmount,
                HoldingPeriodDays = plan.HoldingPeriodDays,

                Score = candidate.Score,
                Rank = candidate.Rank,
                Confidence = ConfidenceScale.FromScore(candidate.Score),
                Strategy = candidate.Strategy,

                Thesis = selection.Thesis,
                BullCase = selection.BullCase,
                BearCase = selection.BearCase,
                Risks = selection.Risks,
                CatalystSummary = selection.CatalystSummary,
                WhyThisOne = selection.WhyThisOne,

                Model = thesis.Model,
                PromptVersion = thesisGenerator.PromptVersion,
                LlmRawJson = Clip(thesis.RawJson, 100_000),
                SignalsJson = JsonSerializer.Serialize(payload.GetValueOrDefault(candidate.Symbol), ThesisPromptJson),
                NewsJson = JsonSerializer.Serialize(headlines.Select(n => new
                {
                    id = n.ExternalId,
                    headline = n.Headline,
                    source = n.Source,
                    url = n.Url,
                    publishedAtUtc = n.PublishedAtUtc
                }), ThesisPromptJson),
                ValidationNotes = notes
            });

            // Mark the candidate row so the run view can show which of the ranked
            // names actually became a recommendation.
            var candidateRow = await db.Candidates
                .FirstOrDefaultAsync(x => x.AnalysisRunId == run.Id && x.Symbol == candidate.Symbol, ct);
            if (candidateRow is not null)
            {
                candidateRow.Selected = true;
                candidateRow.NewsCount = headlines.Count;
            }

            count++;
        }

        await db.SaveChangesAsync(ct);
        return count;
    }

    /// <summary>Flags thin output without blocking it — worth reviewing, not worth discarding.</summary>
    private static string? Describe(ThesisSelection selection)
    {
        var empty = selection.ProseFields()
            .Where(f => string.IsNullOrWhiteSpace(f.Text))
            .Select(f => f.Field)
            .ToArray();

        return empty.Length == 0 ? null : $"empty prose fields: {string.Join(", ", empty)}";
    }

    private string BuildMarketContext(
        IReadOnlyList<SignalSet> signals,
        Dictionary<string, Instrument> bySymbol,
        Dictionary<int, List<DailyBar>> barsByInstrument,
        DateOnly tradingDate)
    {
        var breadth = signals.Count == 0
            ? 0
            : (int)Math.Round(signals.Count(s => s.AboveSma50) / (double)signals.Count * 100);

        var spy = BarsFor(bySymbol, barsByInstrument, "SPY");
        var spyMomentum = 0d;
        if (spy.Count > 21)
        {
            var closes = spy.Select(b => (double)b.Close).ToArray();
            spyMomentum = TechnicalIndicators.RateOfChange(closes, 20).Latest() ?? 0d;
        }

        var tone = spyMomentum switch
        {
            > 3 => "risk-on",
            > 0 => "constructive",
            > -3 => "choppy",
            _ => "risk-off"
        };

        var sb = new StringBuilder();
        sb.Append($"Session {tradingDate:yyyy-MM-dd}. Broad market is {tone}. ");
        sb.Append($"{breadth}% of the universe is trading above its 50-day average. ");
        sb.Append("Breadth below roughly 40% argues for smaller size and fewer positions.");
        return sb.ToString();
    }

    private static readonly JsonSerializerOptions ThesisPromptJson = new(JsonSerializerDefaults.Web);

    private static double Round(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Clip(string value, int length) =>
        value.Length <= length ? value : value[..length];

    private void Stage(string message)
    {
        _stages.Add($"[{clock.UtcNow:HH:mm:ss}] {message}");
        log.LogInformation("pipeline: {Stage}", message);
    }
}
