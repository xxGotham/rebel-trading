# Rebel Trading

AI-powered trading assistant for individual investors. **Power to the trader.**

Domain: `rebeltrading.co`

## Vision

Give individual investors a simplified version of the tools institutions have —
market data, research, signals, and algorithms — through a conversational AI
assistant, not a stock screener.

**Example interaction:**
> Rebel: AMD caught my attention this morning. Unusually strong premarket volume,
> positive semiconductor momentum, and a meaningful overnight catalyst. Three
> reasons I like it, one thing that concerns me. Want to dig deeper?
>
> **Dig Deeper → / Skip → / Build Trade →**

## Architecture Philosophy

No "ask the LLM what to buy." A **deterministic pipeline** does the work; the
LLM only interprets and explains it.

```
Market universe → market data → news/catalysts → technical & quant signals
  → score/rank candidates → select top N → structured data to AI
  → AI explains → validate AI output → save recommendations
```

App code computes prices, moving averages, RSI, relative volume, momentum,
volatility, risk/reward, position sizing, and entry/target/stop validation.
The LLM never invents prices, news, or indicators.

## Each Recommendation Includes

Ticker, direction, confidence score, rationale, news/catalysts, premarket
move, relative volume, momentum, sector strength, technicals, bull/bear case,
risks, entry range, target, stop, risk/reward, expected holding period.

## Accountability

Every recommendation is stored permanently and later graded against what
actually happened, enabling analytics: win rate, avg winner/loser, simulated
return vs. S&P 500, and performance broken down by strategy, confidence,
market regime, catalyst type, and holding period. Goal: know which signals
actually work instead of trusting the AI blindly.

## Trading Flow (progressive)

1. **Recommendation only** (paper, no execution)
2. **Paper Trade** — user accepts a rec into a simulated portfolio; track P&L
3. **Alpaca paper trading** — Build Trade → review order → execute paper order
4. **Live trading** (much later) — brokerage connection, explicit activation,
   buying-power checks, exposure/loss limits, stale-price checks, idempotency,
   audit logging, emergency disable, confirm-every-trade

Frontend never talks to the broker directly:
`User → frontend → backend → risk/validation → Alpaca API → order tracking`

## Longer-Term: Signals as a Subscription

Named strategies (Rebel Momentum, Rebel Catalyst, Rebel Breakouts, Rebel
Swing, Rebel Tech, Rebel Small Cap), each with transparent track records,
subscribable, alerting users to new opportunities they can review and execute.

## Stack

- **Frontend:** React, Vite, TypeScript, Tailwind, shadcn/ui, Vercel
- **Backend:** ASP.NET Core (C#) — chosen over Go/TS since it's the author's
  strongest stack for auth, ingestion, signals, AI orchestration, paper
  trading, brokerage integration, and risk controls
- **DB:** PostgreSQL
- **Background jobs:** Hangfire (later, a dedicated .NET worker)
- **AI:** OpenAI API, structured JSON/schema responses
- **Broker:** Alpaca (paper first, live much later)
- Possible future Python service for backtesting/ML — not needed for V1

## V1 Scope

One user (me) · ~100 liquid stocks · long only · swing trades (1–10 days) ·
3–5 recs/morning · paper trading only · one market-data provider, one news
provider, one AI provider.

**Explicitly out of scope for now:** options, crypto, day-trading automation,
multiple brokers, live trading, social features, subscription billing,
autonomous execution.

## Phases

| # | Phase | Outcome |
|---|-------|---------|
| 1 | Foundation | React/Vite + ASP.NET Core + Postgres shell, manual "Run Analysis" |
| 2 | Market Intelligence | Universe, data/news ingestion, technicals, scoring — ~100 stocks → 10–20 candidates, no LLM |
| 3 | Rebel AI | Structured candidates → LLM → validated recs (thesis, bull/bear, entry/target/stop) |
| 4 | Morning Brief | Automated pre-market run + guided walkthrough |
| 5 | Performance Tracking | Auto-grade recs, win/loss, strategy & confidence stats, benchmark |
| 6 | Paper Trading | Accept rec into simulated portfolio, track to exit |
| 7 | Alpaca Paper Trading | Build Trade → review → execute paper order → track |
| 8 | Live Trading | Real brokerage, hard risk limits, full audit, kill switch |

## Immediate Goal

Not the whole platform — just:

> Run Rebel and have it analyze real market/news data and return three
> source-backed trade opportunities with a clear explanation of why each was
> selected.

The core asset isn't the website — it's the pipeline:
**market data → signals → ranking → AI analysis → recommendations → measured outcomes.**
Everything else is built on top of that.
