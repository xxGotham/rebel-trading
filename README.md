# Rebel Trading

AI-powered trading assistant for individual investors. **Power to the trader.**

Domain: `rebeltrading.co`

## Vision

Give individual investors a simplified version of the tools institutions have —
market data, research, signals, and algorithms — through a conversational AI
assistant, not a stock screener.

## Architecture Philosophy

No "ask the LLM what to buy." A **deterministic pipeline** does the work; the
LLM only interprets and explains it.

```
universe → market data → signals → score → rank → select
  → news → trade plans → AI prose → validate → persist → grade
```

The ordering is the design. Everything that decides *what* to recommend happens
before the model is called. The model runs last and only describes a decision
that has already been made and priced.

This is enforced in code, not by convention:

- **The model returns no numbers at all.** It contributes six prose fields and a
  yes/no on whether a candidate is worth showing. Entry, target, stop, size,
  risk/reward, confidence and holding period are all computed by `Rebel.Core`.
- **`NumberGuard` rejects any prose containing a figure absent from the payload
  the model was given.** One corrective retry, then the recommendation is
  dropped rather than shown. A fabricated price target never reaches the user.
- **Every run is replayable.** Scoring version, prompt version, pipeline version
  and a config hash are stamped on each run; the exact signals and headlines the
  model saw are frozen onto each recommendation.

## Quick start

Nothing is required beyond the .NET SDK and Node. With no credentials, Rebel
runs the full pipeline against **synthetic market data** and a deterministic
template thesis writer — real code path, fake prices, loudly labelled in the UI.

```bash
# Terminal 1 — API on http://localhost:5199
cd backend/src/Rebel.Api
dotnet run

# Terminal 2 — UI on http://localhost:5173
cd frontend
npm install
npm run dev
```

Open http://localhost:5173 and press **Run analysis**.

### Going live

| Capability | Set | Result |
|---|---|---|
| Real market data + news | `Rebel__Alpaca__KeyId`, `Rebel__Alpaca__SecretKey` | Alpaca replaces the synthetic provider |
| Real theses | `Rebel__OpenAi__ApiKey` | OpenAI replaces the template generator |
| Postgres | `ConnectionStrings__Rebel` | Migrations run on startup; SQLite otherwise |
| Scheduled runs | `Rebel__Pipeline__EnableScheduledRuns=true` | Hangfire: 07:15 ET brief, 17:30 ET grading |

See `.env.example`. Providers are selected by an `auto` policy — the real vendor
when credentials exist, the offline stand-in when they don't — so a fresh clone
runs end to end before anyone has signed up for anything.

```bash
docker compose up -d   # Postgres, if you want it
```

## Layout

```
backend/
  src/
    Rebel.Core/            no external dependencies — domain, indicators,
                           scoring, trade plans, grading, market calendar
    Rebel.Infrastructure/  EF Core, Alpaca, OpenAI, the pipeline orchestrator
    Rebel.Api/             minimal API + Hangfire
  tests/
    Rebel.Core.Tests/      90 tests: golden vectors, lookahead guards, grading rules
    Rebel.Api.Tests/       19 tests: the real pipeline over SQLite, offline providers
frontend/                  Vite + React + TypeScript + Tailwind
```

`Rebel.Core` is dependency-free on purpose: the indicator and scoring code has to
be reusable by a backtester without dragging EF Core and HTTP clients along.

## How a recommendation is built

**Signals** — SMA 20/50, RSI(14), ATR(14), 20-day momentum, distance from the
20-day high, realized volatility, relative strength vs SPY, sector strength via
the sector ETF, and pre-market gap and participation from a snapshot.

**Filters** — price, average dollar volume, minimum history, maximum gap (don't
chase), above the 50-day average (long only), maximum RSI (not parabolic),
maximum ATR (sizable). Failing a filter records a *reason*, not a bad score, and
the reason is shown in the UI.

**Scoring** — nine factors, cross-sectionally z-scored against the same-day
cohort, winsorized at ±3, weighted, then mapped through the normal CDF to a
0–100 percentile. "RSI 62" means nothing alone; "top decile relative volume among
100 liquid large caps this morning" is a signal.

**Trade plan** — the stop goes just under the 10-day swing low, bounded to
1.0–2.5 ATR. The target is the breakout objective above the 20-day high, floored
by both a minimum ATR distance and the distance needed to clear the minimum
risk/reward. A setup is rejected when clearing that minimum would demand a move
beyond 6 ATR — implausible inside a swing window.

**Grading** — every recommendation is graded against subsequent price action,
losers included. When one daily bar spans both target and stop, the **stop** is
assumed to have hit first: intraday sequence is unknowable from daily bars, so
the pessimistic reading is the honest one. Ideas that never traded into the entry
band are recorded as such and excluded from the statistics — they say nothing
about signal quality.

## Accountability

Performance is broken down by strategy, confidence, outcome and holding period,
against the S&P over the identical window. The measure that matters is
**expectancy in R** — average return per unit of risk — not win rate, which any
system can inflate with wide stops.

The grader is versioned (`grader-v1`) and the version is stored on every outcome.
A change to the grading rules that quietly improved the historical track record
would be the single most damaging bug this system could have, so it is made
visible by construction.

## Named strategies

Every candidate is labelled with the strategy that best explains its rank —
Rebel Momentum, Rebel Catalyst, Rebel Breakouts, Rebel Swing. This exists from
day one rather than "later, with subscriptions", because performance-by-strategy
is only meaningful if every historical recommendation carries a label.
Retro-fitting labels onto a year of past recs is guesswork.

## API

| | |
|---|---|
| `GET /api/status` | Which providers are live, and whether the data is real |
| `POST /api/runs` | Run the pipeline |
| `GET /api/runs/latest`, `/api/runs/{id}` | Run detail: recs, full screen, stage log |
| `GET /api/recommendations` | Filter by date or strategy |
| `GET /api/recommendations/{id}/evidence` | The exact signals, headlines and model response |
| `GET /api/performance` | Stats and breakdowns |
| `POST /api/performance/grade` | Grade everything outstanding |
| `POST /api/paper/positions` | Accept a rec into the simulated portfolio |
| `GET /api/paper/portfolio` | Open and closed positions, realised P&L |

OpenAPI at `/openapi/v1.json`, Hangfire dashboard at `/hangfire` (development).

## Tests

```bash
dotnet test backend/RebelTrading.sln
```

109 tests, no network, no credentials. The API tests run the real ingestion,
scoring, planning, grading and persistence code over SQLite — only the outermost
vendor calls are replaced. Notable coverage:

- Indicators against hand-computed Wilder series.
- A bar dated on the trading date leaves every indicator untouched — the
  lookahead guard.
- Holidays computed by rule (including Good Friday and weekend observance) so the
  calendar cannot silently expire.
- `NumberGuard` catches invented prices and statistics while tolerating rounding,
  small integers, years and figures quoted from supplied headlines.
- Generated prose from a real pipeline run contains no unsupported number.

## Scope

**V1**: one user · ~115 liquid names · long only · swing trades (1–10 days) ·
3 recs per run · paper only.

**Out of scope**: options, crypto, day trading, multiple brokers, live trading,
social features, billing, autonomous execution.

## Status

| # | Phase | State |
|---|-------|-------|
| 1 | Foundation | Done |
| 2 | Market intelligence | Done |
| 3 | Rebel AI | Done |
| 4 | Morning brief | Done — Hangfire schedule, off by default |
| 5 | Performance tracking | Done |
| 6 | Paper trading | Done |
| 7 | Alpaca paper trading | Not started — market data client is in place |
| 8 | Live trading | Not started |

### Known limitations

- **The universe is survivorship-biased.** Every name in `universe.csv` is a
  company that still exists and still trades. Any backtest over it will look
  better than reality. This needs point-in-time constituents before backtesting
  means anything.
- **Pre-market relative volume is only comparable cross-sectionally.** It is a
  small number by construction and is used solely as a z-score against the same
  cohort at the same instant. On the free IEX feed it is also thin — worth
  validating against your actual universe before leaning on it.
- **Recommended trades cluster at the minimum risk/reward.** The score favours
  names near their 20-day high, so the breakout objective usually sits below the
  risk floor and the target is set by the floor. The ratio does vary with the
  setup, but tuning the target rule (a measured move, say) is the obvious next
  experiment.
- **A run is synchronous.** Fine at ~115 symbols and a few seconds; it needs to
  move behind a job when the universe grows.

Paper trading only. Nothing here is investment advice, and no order reaches a
broker.
