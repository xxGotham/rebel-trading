using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Rebel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analysis_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Trigger = table.Column<int>(type: "integer", nullable: false),
                    TradingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UniverseSize = table.Column<int>(type: "integer", nullable: false),
                    CandidateCount = table.Column<int>(type: "integer", nullable: false),
                    RecommendationCount = table.Column<int>(type: "integer", nullable: false),
                    ScoringVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PipelineVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ConfigHash = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Model = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PromptTokens = table.Column<int>(type: "integer", nullable: false),
                    CompletionTokens = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    StageLog = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "instruments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Symbol = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Sector = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SectorEtf = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Exchange = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsBenchmark = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_instruments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "candidates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AnalysisRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstrumentId = table.Column<int>(type: "integer", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<double>(type: "double precision", nullable: false),
                    Selected = table.Column<bool>(type: "boolean", nullable: false),
                    ExclusionReason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LastPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PreviousClose = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    GapPercent = table.Column<double>(type: "double precision", nullable: false),
                    RelativeVolume = table.Column<double>(type: "double precision", nullable: false),
                    Rsi14 = table.Column<double>(type: "double precision", nullable: false),
                    Atr14 = table.Column<double>(type: "double precision", nullable: false),
                    AtrPercent = table.Column<double>(type: "double precision", nullable: false),
                    Sma20 = table.Column<double>(type: "double precision", nullable: false),
                    Sma50 = table.Column<double>(type: "double precision", nullable: false),
                    Momentum20 = table.Column<double>(type: "double precision", nullable: false),
                    PercentOff20DayHigh = table.Column<double>(type: "double precision", nullable: false),
                    RealizedVolatility20 = table.Column<double>(type: "double precision", nullable: false),
                    RelativeStrengthVsMarket = table.Column<double>(type: "double precision", nullable: false),
                    SectorStrength = table.Column<double>(type: "double precision", nullable: false),
                    AverageVolume20 = table.Column<long>(type: "bigint", nullable: false),
                    FactorBreakdownJson = table.Column<string>(type: "jsonb", nullable: false),
                    NewsCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_candidates_analysis_runs_AnalysisRunId",
                        column: x => x.AnalysisRunId,
                        principalTable: "analysis_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_candidates_instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "daily_bars",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstrumentId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Open = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    High = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Low = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Close = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Volume = table.Column<long>(type: "bigint", nullable: false),
                    Vwap = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_bars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_daily_bars_instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "news_items",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstrumentId = table.Column<int>(type: "integer", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Headline = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Source = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    FetchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_news_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_news_items_instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstrumentId = table.Column<int>(type: "integer", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TradingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    ReferencePrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    EntryLow = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    EntryHigh = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Target = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    StopLoss = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    RiskRewardRatio = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    StopDistancePercent = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    TargetDistancePercent = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    SuggestedShares = table.Column<int>(type: "integer", nullable: false),
                    SuggestedRiskAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    HoldingPeriodDays = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<double>(type: "double precision", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    Confidence = table.Column<int>(type: "integer", nullable: false),
                    Strategy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Thesis = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    BullCase = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    BearCase = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Risks = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CatalystSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    WhyThisOne = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Model = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LlmRawJson = table.Column<string>(type: "jsonb", nullable: false),
                    SignalsJson = table.Column<string>(type: "jsonb", nullable: false),
                    NewsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ValidationNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recommendations_analysis_runs_AnalysisRunId",
                        column: x => x.AnalysisRunId,
                        principalTable: "analysis_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_recommendations_instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "snapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstrumentId = table.Column<int>(type: "integer", nullable: false),
                    AnalysisRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    AsOfUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PreviousClose = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    SessionVolume = table.Column<long>(type: "bigint", nullable: false),
                    DayHigh = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    DayLow = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_snapshots_instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "paper_positions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecommendationId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstrumentId = table.Column<int>(type: "integer", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EntryPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Shares = table.Column<int>(type: "integer", nullable: false),
                    StopLoss = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Target = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    ExitReason = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paper_positions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_paper_positions_recommendations_RecommendationId",
                        column: x => x.RecommendationId,
                        principalTable: "recommendations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recommendation_outcomes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecommendationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    GradedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EntryPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    ExitDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    ReturnPercent = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    RMultiple = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    MaxFavorablePercent = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    MaxAdversePercent = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    DaysHeld = table.Column<int>(type: "integer", nullable: true),
                    BenchmarkReturnPercent = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    GraderVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendation_outcomes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recommendation_outcomes_recommendations_RecommendationId",
                        column: x => x.RecommendationId,
                        principalTable: "recommendations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_analysis_runs_StartedAtUtc",
                table: "analysis_runs",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_runs_TradingDate",
                table: "analysis_runs",
                column: "TradingDate");

            migrationBuilder.CreateIndex(
                name: "IX_candidates_AnalysisRunId_Rank",
                table: "candidates",
                columns: new[] { "AnalysisRunId", "Rank" });

            migrationBuilder.CreateIndex(
                name: "IX_candidates_InstrumentId",
                table: "candidates",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_daily_bars_InstrumentId_Date",
                table: "daily_bars",
                columns: new[] { "InstrumentId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_instruments_IsActive_IsBenchmark",
                table: "instruments",
                columns: new[] { "IsActive", "IsBenchmark" });

            migrationBuilder.CreateIndex(
                name: "IX_instruments_Symbol",
                table: "instruments",
                column: "Symbol",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_news_items_InstrumentId_ExternalId",
                table: "news_items",
                columns: new[] { "InstrumentId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_news_items_PublishedAtUtc",
                table: "news_items",
                column: "PublishedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_paper_positions_RecommendationId",
                table: "paper_positions",
                column: "RecommendationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_paper_positions_Status",
                table: "paper_positions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_recommendation_outcomes_RecommendationId",
                table: "recommendation_outcomes",
                column: "RecommendationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recommendation_outcomes_Status",
                table: "recommendation_outcomes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_recommendations_AnalysisRunId",
                table: "recommendations",
                column: "AnalysisRunId");

            migrationBuilder.CreateIndex(
                name: "IX_recommendations_InstrumentId",
                table: "recommendations",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_recommendations_Strategy",
                table: "recommendations",
                column: "Strategy");

            migrationBuilder.CreateIndex(
                name: "IX_recommendations_TradingDate_Symbol",
                table: "recommendations",
                columns: new[] { "TradingDate", "Symbol" });

            migrationBuilder.CreateIndex(
                name: "IX_snapshots_AnalysisRunId",
                table: "snapshots",
                column: "AnalysisRunId");

            migrationBuilder.CreateIndex(
                name: "IX_snapshots_InstrumentId_AsOfUtc",
                table: "snapshots",
                columns: new[] { "InstrumentId", "AsOfUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidates");

            migrationBuilder.DropTable(
                name: "daily_bars");

            migrationBuilder.DropTable(
                name: "news_items");

            migrationBuilder.DropTable(
                name: "paper_positions");

            migrationBuilder.DropTable(
                name: "recommendation_outcomes");

            migrationBuilder.DropTable(
                name: "snapshots");

            migrationBuilder.DropTable(
                name: "recommendations");

            migrationBuilder.DropTable(
                name: "analysis_runs");

            migrationBuilder.DropTable(
                name: "instruments");
        }
    }
}
