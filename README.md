# Rebel Trading

A configurable, voice-first AI trading co-pilot that helps traders follow their own strategy in real time.

> **Your strategy. Your rules. Your AI trading co-pilot.**

---

## What It Is

Rebel Trading is a real-time AI coaching app designed to run alongside a trader's existing platform (NinjaTrader, TradingView, a broker app). It does not replace those tools — it runs on the trader's phone and coaches through voice.

The trader defines their **Playbook** — entry conditions, stop rules, profit targets, risk limits. Rebel watches live market data, evaluates those rules deterministically, detects meaningful events, and speaks when something matters. The trader can ask Rebel questions at any time by voice.

**Rebel coaches. The trader executes.**

## What It Is Not

- Another charting platform
- A BUY/SELL signal service
- A black-box trading algorithm
- An autonomous execution bot
- An LLM processing every individual market tick

## Core Experience

```
1. Open Rebel on your phone.
2. Select a Playbook and instrument.
3. Confirm connections (broker, market data, AI, voice).
4. Tap Start Coaching.
5. Rebel monitors market state, positions, orders, risk, and Playbook conditions.
6. Rebel speaks only when meaningful events occur.
7. Ask Rebel questions at any time by voice.
8. Rebel automatically journals the session.
```

**Example:**

> Rebel: *"MNQ has entered your setup area. Two of your three entry conditions are satisfied. Momentum confirmation has not occurred yet."*
>
> Trader: *"What are we waiting for?"*
>
> Rebel: *"Your Playbook requires momentum confirmation before entering. That condition has not triggered yet."*

---

## Architecture

### Core Principle

There is a strict separation between deterministic trading logic and AI conversation.

```
Live Market Data
    → Normalized Market State
    → Deterministic Rules Engine   ← evaluates the Playbook mathematically
    → Structured Trading Event
    → AI Provider                  ← converts the event to natural language
    → Voice
    → Mobile App
```

The **C# backend** calculates everything:
- Current price, entry price, P&L
- Distance from entry, stop, target
- Risk, position size
- Rule condition evaluation
- Threshold crossings
- Daily loss, trade count

The **LLM** only communicates and reasons about those pre-computed facts. It never determines whether a mathematical threshold occurred — only whether to say something about it.

### Provider-Agnostic Design

Every external integration is pluggable:

| Layer | Interface | Implementations |
|---|---|---|
| Broker | `IBrokerProvider` | Synthetic (demo), Alpaca, Tradier, … |
| Market Data | `IMarketDataProvider` | Synthetic (demo), Alpaca, Databento, Polygon, … |
| AI | `IAIProvider` | Synthetic (demo), Anthropic Claude, OpenAI GPT, … |
| Voice | `IVoiceProvider` | expo-speech TTS, OpenAI Realtime, … |

A fresh clone runs with **synthetic providers** — real code path, simulated prices and broker state, loudly labelled in the UI.

### Subscription & Entitlements

Rebel uses a plan/entitlement model (not a boolean `IsPaid` flag). Plans: `FounderBeta`, `Free`, `Pro`, `Premium`. Entitlements gate features: `LiveCoaching`, `VoiceCoaching`, `AdvancedJournaling`, etc.

### API Cost Metering

Every billable external call (LLM tokens, voice minutes, market data, broker APIs) generates an `ApiUsageEvent` tied to the user, session, provider, model, and cost. Provider pricing is stored in versioned `ProviderPricing` records so historical costs remain accurate when pricing changes.

---

## Repository Layout

```
rebel-trading/
├── mobile/                     React Native + Expo mobile app (primary coaching client)
│   ├── app/
│   │   ├── (auth)/            Login, Register, Forgot Password
│   │   └── (app)/             Coach (home), Playbooks, History, Settings
│   │       └── session.tsx    Live coaching screen — the core product experience
│   ├── components/coaching/   SessionHeader, PositionCard, RebelMessage, AskRebelButton
│   ├── hooks/                 useCoachingSession, useVoice
│   ├── lib/                   API client (axios + JWT refresh), SignalR client, types
│   └── store/                 Zustand stores (auth, session)
│
├── backend/
│   └── src/
│       ├── Rebel.Core/        Pure domain — no external dependencies
│       │   ├── Domain/        Playbook, TradingSession, enums, entities
│       │   ├── Trading/       MarketState, TradingEvent, RulesEngine
│       │   └── Abstractions/  IBrokerProvider, IMarketDataProvider, IAIProvider
│       ├── Rebel.Infrastructure/
│       │   ├── Identity/      ApplicationUser (ASP.NET Core Identity)
│       │   ├── Persistence/   EF Core + PostgreSQL (RebelDbContext)
│       │   ├── Providers/     Synthetic market data, broker, and AI implementations
│       │   └── Services/      TokenService (JWT), CoachingSessionService (SignalR loop)
│       └── Rebel.Api/
│           ├── Endpoints/     Auth, Playbooks, Sessions, Providers (minimal API)
│           ├── Hubs/          CoachingHub (SignalR real-time)
│           └── Program.cs
│
└── frontend/                   Web app (account management, Playbook editor, journals)
```

---

## Tech Stack

| Layer | Technology |
|---|---|
| Mobile | React Native, TypeScript, Expo SDK 52, Expo Router |
| Real-time | SignalR (@microsoft/signalr) |
| State | Zustand, TanStack Query |
| Voice | expo-av (recording), expo-speech (TTS) |
| Backend | .NET 10, ASP.NET Core, C# |
| Auth | ASP.NET Core Identity, JWT, refresh tokens |
| Database | PostgreSQL + Entity Framework Core 10 |
| Real-time (server) | SignalR |
| Web | React, TypeScript, Vite, Tailwind CSS |

---

## Quick Start

### Prerequisites

- .NET 10 SDK
- Node.js 20+
- PostgreSQL (or use Docker)
- Expo CLI (`npm install -g expo-cli`)

### 1. Database

```bash
docker compose up -d   # starts PostgreSQL
```

Or point to an existing instance by setting `ConnectionStrings__Default`.

### 2. Backend

```bash
cd backend/src/Rebel.Api
dotnet run
# API on http://localhost:5000
# OpenAPI at http://localhost:5000/openapi/v1.json
```

The database schema is created automatically on first start (`EnsureCreated`). No migration command needed for development.

### 3. Mobile App

```bash
cd mobile
cp .env.example .env          # set EXPO_PUBLIC_API_URL=http://localhost:5000
npm install
npx expo start
# Press 'i' for iOS simulator, 'a' for Android emulator, or scan QR with Expo Go
```

### Environment Variables

#### Backend (`appsettings.Development.json` or environment)

| Variable | Description | Default |
|---|---|---|
| `ConnectionStrings__Default` | PostgreSQL connection string | localhost/rebel_trading_dev |
| `Jwt__Secret` | JWT signing key (min 32 chars) | dev key |
| `Rebel__AnthropicApiKey` | Your Anthropic API key | synthetic provider used |
| `Rebel__OpenAiApiKey` | Your OpenAI API key | synthetic provider used |

#### Mobile (`.env`)

| Variable | Description |
|---|---|
| `EXPO_PUBLIC_API_URL` | Backend base URL |

---

## API Reference

### Auth
| Method | Path | Description |
|---|---|---|
| `POST` | `/auth/register` | Create account |
| `POST` | `/auth/login` | Sign in, returns JWT + refresh token |
| `POST` | `/auth/refresh` | Rotate tokens |
| `POST` | `/auth/logout` | Invalidate refresh token |
| `GET` | `/auth/me` | Current user |
| `POST` | `/auth/forgot-password` | Request password reset |

### Playbooks
| Method | Path | Description |
|---|---|---|
| `GET` | `/playbooks` | List user's Playbooks |
| `POST` | `/playbooks` | Create Playbook |
| `GET` | `/playbooks/{id}` | Get Playbook |
| `PUT` | `/playbooks/{id}` | Update Playbook |
| `DELETE` | `/playbooks/{id}` | Delete Playbook |
| `POST` | `/playbooks/{id}/activate` | Set as active Playbook |

### Sessions
| Method | Path | Description |
|---|---|---|
| `POST` | `/sessions/preflight` | Check connections before starting |
| `POST` | `/sessions/start` | Start a coaching session |
| `POST` | `/sessions/{id}/end` | End session, trigger journal |
| `GET` | `/sessions/{id}` | Get session state |
| `GET` | `/sessions/history` | Past session summaries |
| `POST` | `/sessions/{id}/ask` | Ask Rebel a question |

### Providers
| Method | Path | Description |
|---|---|---|
| `GET` | `/providers` | List connected providers |
| `POST` | `/providers/connect` | Connect a provider |
| `DELETE` | `/providers/{id}` | Disconnect a provider |

### SignalR Hub — `/hubs/coaching?sessionId={id}`

Connect with `access_token` query param. The hub pushes:

| Event | Payload |
|---|---|
| `QuoteUpdated` | `{ instrument, price, bid, ask, change, changePct }` |
| `PositionUpdated` | `{ side, quantity, entryPrice, stopPrice, targetPrice, unrealizedPnl, … }` |
| `OrdersUpdated` | `Order[]` |
| `RebelSpeaking` | `{ id, text, timestamp, eventType, isUserQuestion }` |
| `SessionStatusChanged` | `"Active" \| "Ended" \| …` |

---

## Trading Events

Rebel's Rules Engine emits structured events that trigger AI coaching messages:

| Event | When |
|---|---|
| `POSITION_OPENED` | Broker reports a new position |
| `POSITION_CLOSED` | Position no longer held |
| `STOP_THRESHOLD_REACHED` | Price reaches a stop management level defined in the Playbook |
| `PROFIT_TARGET_APPROACHING` | Within 20% of the target |
| `PLAYBOOK_RULE_VIOLATED` | Trader action conflicts with Playbook rules |
| `MAX_DAILY_LOSS_REACHED` | Daily loss limit hit |
| `MAX_TRADE_COUNT_REACHED` | Maximum trades per day reached |
| `ENTRY_CONDITION_MET` | All Playbook entry conditions satisfied |
| … | 10 more event types |

---

## Development Phases

| Phase | Description | Status |
|---|---|---|
| 1 | Foundation — auth, DB, mobile app, provider abstractions | ✅ Done |
| 2 | Billing — subscriptions, entitlements, API cost metering | 🔜 Next |
| 3 | Market Data — real streaming providers (Alpaca, Databento) | 🔜 |
| 4 | Broker Integration — real positions and orders | 🔜 |
| 5 | Playbooks — full editor, versioning, validation | 🔜 |
| 6 | Rules Engine — complete deterministic evaluation | 🔜 |
| 7 | AI Providers — Anthropic + OpenAI with coaching context | 🔜 |
| 8 | Voice — full realtime voice pipeline | 🔜 |
| 9 | Journaling — session timeline, transcript, adherence | 🔜 |
| 10 | Admin Analytics — cost/revenue per user, coaching hours | 🔜 |
| 11 | Paper Trading — full simulation before live | 🔜 |

---

## Bring Your Own Key (BYOK)

Users can supply their own AI or market-data API keys. Provider credentials are stored encrypted and never exposed to the frontend or logged. Rebel distinguishes `REBEL_MANAGED` from `USER_MANAGED_BYOK` for unit economics tracking.

---

## Security

- TLS required in production
- JWT with short-lived access tokens (15 min) + rotating refresh tokens (30 days)
- Passwords hashed by ASP.NET Core Identity (PBKDF2)
- Provider credentials stored encrypted, never logged or returned to clients
- All user resources scoped by `UserId` — strict tenant isolation
- Rate limiting on auth endpoints

---

## Not Built Yet (By Design)

- Autonomous trading
- Copy trading or social features
- Strategy marketplace
- Hundreds of indicators or charting
- Custom AI models
- Guaranteed signal services

The proprietary value comes from the **Playbook system**, **deterministic Rules Engine**, **event system**, **voice UX**, and **session journaling** — not from a black box.

---

*Paper trading only in early phases. Nothing here is investment advice, and no order reaches a broker automatically.*
