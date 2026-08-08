using Microsoft.EntityFrameworkCore;
using Rebel.Core.Domain;

namespace Rebel.Infrastructure.Persistence;

public class RebelDbContext(DbContextOptions<RebelDbContext> options) : DbContext(options)
{
    public DbSet<Instrument> Instruments => Set<Instrument>();
    public DbSet<DailyBar> DailyBars => Set<DailyBar>();
    public DbSet<Snapshot> Snapshots => Set<Snapshot>();
    public DbSet<NewsItem> NewsItems => Set<NewsItem>();
    public DbSet<AnalysisRun> AnalysisRuns => Set<AnalysisRun>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();
    public DbSet<RecommendationOutcome> RecommendationOutcomes => Set<RecommendationOutcome>();
    public DbSet<PaperPosition> PaperPositions => Set<PaperPosition>();

    /// <summary>True when running on Npgsql; SQLite is the no-Docker local/dev path.</summary>
    public bool IsPostgres => Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true;

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Instrument>(e =>
        {
            e.ToTable("instruments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Symbol).HasMaxLength(16).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Sector).HasMaxLength(64);
            e.Property(x => x.SectorEtf).HasMaxLength(16);
            e.Property(x => x.Exchange).HasMaxLength(32);
            e.HasIndex(x => x.Symbol).IsUnique();
            e.HasIndex(x => new { x.IsActive, x.IsBenchmark });
        });

        b.Entity<DailyBar>(e =>
        {
            e.ToTable("daily_bars");
            e.HasKey(x => x.Id);
            e.Ignore(x => x.DollarVolume);
            Money(e.Property(x => x.Open));
            Money(e.Property(x => x.High));
            Money(e.Property(x => x.Low));
            Money(e.Property(x => x.Close));
            Money(e.Property(x => x.Vwap));

            // The pipeline re-ingests overlapping windows constantly; this index is
            // what makes that idempotent instead of duplicating history.
            e.HasIndex(x => new { x.InstrumentId, x.Date }).IsUnique();

            e.HasOne(x => x.Instrument)
             .WithMany(i => i.Bars)
             .HasForeignKey(x => x.InstrumentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Snapshot>(e =>
        {
            e.ToTable("snapshots");
            e.HasKey(x => x.Id);
            e.Ignore(x => x.GapPercent);
            Money(e.Property(x => x.LastPrice));
            Money(e.Property(x => x.PreviousClose));
            Money(e.Property(x => x.DayHigh));
            Money(e.Property(x => x.DayLow));
            e.HasIndex(x => new { x.InstrumentId, x.AsOfUtc });
            e.HasIndex(x => x.AnalysisRunId);
            e.HasOne(x => x.Instrument).WithMany().HasForeignKey(x => x.InstrumentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<NewsItem>(e =>
        {
            e.ToTable("news_items");
            e.HasKey(x => x.Id);
            e.Property(x => x.ExternalId).HasMaxLength(128);
            e.Property(x => x.Headline).HasMaxLength(512);
            e.Property(x => x.Summary).HasMaxLength(2048);
            e.Property(x => x.Source).HasMaxLength(128);
            e.Property(x => x.Url).HasMaxLength(1024);
            e.HasIndex(x => new { x.InstrumentId, x.ExternalId }).IsUnique();
            e.HasIndex(x => x.PublishedAtUtc);
            e.HasOne(x => x.Instrument).WithMany().HasForeignKey(x => x.InstrumentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AnalysisRun>(e =>
        {
            e.ToTable("analysis_runs");
            e.HasKey(x => x.Id);
            e.Property(x => x.ScoringVersion).HasMaxLength(32);
            e.Property(x => x.PromptVersion).HasMaxLength(32);
            e.Property(x => x.PipelineVersion).HasMaxLength(32);
            e.Property(x => x.ConfigHash).HasMaxLength(32);
            e.Property(x => x.Model).HasMaxLength(64);
            e.HasIndex(x => x.StartedAtUtc);
            e.HasIndex(x => x.TradingDate);
        });

        b.Entity<Candidate>(e =>
        {
            e.ToTable("candidates");
            e.HasKey(x => x.Id);
            e.Property(x => x.Symbol).HasMaxLength(16);
            e.Property(x => x.ExclusionReason).HasMaxLength(256);
            Money(e.Property(x => x.LastPrice));
            Money(e.Property(x => x.PreviousClose));
            Json(e.Property(x => x.FactorBreakdownJson));
            e.HasIndex(x => new { x.AnalysisRunId, x.Rank });
            e.HasOne(x => x.AnalysisRun).WithMany(r => r.Candidates).HasForeignKey(x => x.AnalysisRunId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Instrument).WithMany().HasForeignKey(x => x.InstrumentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Recommendation>(e =>
        {
            e.ToTable("recommendations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Symbol).HasMaxLength(16);
            e.Property(x => x.Strategy).HasMaxLength(64);
            e.Property(x => x.Model).HasMaxLength(64);
            e.Property(x => x.PromptVersion).HasMaxLength(32);
            e.Property(x => x.Thesis).HasMaxLength(4000);
            e.Property(x => x.BullCase).HasMaxLength(4000);
            e.Property(x => x.BearCase).HasMaxLength(4000);
            e.Property(x => x.Risks).HasMaxLength(4000);
            e.Property(x => x.CatalystSummary).HasMaxLength(4000);
            e.Property(x => x.WhyThisOne).HasMaxLength(2000);
            e.Property(x => x.ValidationNotes).HasMaxLength(2000);

            Money(e.Property(x => x.ReferencePrice));
            Money(e.Property(x => x.EntryLow));
            Money(e.Property(x => x.EntryHigh));
            Money(e.Property(x => x.Target));
            Money(e.Property(x => x.StopLoss));
            Ratio(e.Property(x => x.RiskRewardRatio));
            Ratio(e.Property(x => x.StopDistancePercent));
            Ratio(e.Property(x => x.TargetDistancePercent));
            Money(e.Property(x => x.SuggestedRiskAmount));

            Json(e.Property(x => x.LlmRawJson));
            Json(e.Property(x => x.SignalsJson));
            Json(e.Property(x => x.NewsJson));

            e.HasIndex(x => new { x.TradingDate, x.Symbol });
            e.HasIndex(x => x.Strategy);
            e.HasOne(x => x.AnalysisRun).WithMany(r => r.Recommendations).HasForeignKey(x => x.AnalysisRunId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Instrument).WithMany().HasForeignKey(x => x.InstrumentId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Outcome).WithOne(o => o.Recommendation!)
             .HasForeignKey<RecommendationOutcome>(o => o.RecommendationId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RecommendationOutcome>(e =>
        {
            e.ToTable("recommendation_outcomes");
            e.HasKey(x => x.Id);
            e.Ignore(x => x.IsWin);
            e.Property(x => x.GraderVersion).HasMaxLength(32);
            Money(e.Property(x => x.EntryPrice));
            Money(e.Property(x => x.ExitPrice));
            Ratio(e.Property(x => x.ReturnPercent));
            Ratio(e.Property(x => x.RMultiple));
            Ratio(e.Property(x => x.MaxFavorablePercent));
            Ratio(e.Property(x => x.MaxAdversePercent));
            Ratio(e.Property(x => x.BenchmarkReturnPercent));
            e.HasIndex(x => x.RecommendationId).IsUnique();
            e.HasIndex(x => x.Status);
        });

        b.Entity<PaperPosition>(e =>
        {
            e.ToTable("paper_positions");
            e.HasKey(x => x.Id);
            e.Ignore(x => x.CostBasis);
            e.Ignore(x => x.RealizedPnl);
            e.Property(x => x.Symbol).HasMaxLength(16);
            e.Property(x => x.ExitReason).HasMaxLength(128);
            Money(e.Property(x => x.EntryPrice));
            Money(e.Property(x => x.StopLoss));
            Money(e.Property(x => x.Target));
            Money(e.Property(x => x.ExitPrice));
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.RecommendationId).IsUnique();
            e.HasOne(x => x.Recommendation).WithMany().HasForeignKey(x => x.RecommendationId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        ApplyProviderConventions(b);
    }

    /// <summary>
    /// SQLite has no decimal or jsonb type. Rather than let EF fall back to
    /// lossy REAL for money, decimals are stored as TEXT with a converter — the
    /// same values round-trip exactly on both providers.
    /// </summary>
    private void ApplyProviderConventions(ModelBuilder b)
    {
        if (IsPostgres) return;

        foreach (var entity in b.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (type == typeof(decimal)) property.SetColumnType("TEXT");
                if (property.GetColumnType() == "jsonb") property.SetColumnType("TEXT");
            }
        }
    }

    private static void Money<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<T> p) =>
        p.HasPrecision(18, 4);

    private static void Ratio<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<T> p) =>
        p.HasPrecision(12, 4);

    private void Json(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<string> p)
    {
        if (IsPostgres) p.HasColumnType("jsonb");
    }
}
