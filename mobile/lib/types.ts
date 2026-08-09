// ─── Auth ───────────────────────────────────────────────────────────────────

export interface User {
  id: string
  email: string
  username: string
  subscriptionPlan: SubscriptionPlan
  entitlements: Entitlement[]
}

export type SubscriptionPlan = 'FOUNDER_BETA' | 'FREE' | 'PRO' | 'PREMIUM'

export type Entitlement =
  | 'LiveCoaching'
  | 'VoiceCoaching'
  | 'MaxPlaybooks'
  | 'SessionHistory'
  | 'AdvancedJournaling'
  | 'MultipleBrokerConnections'
  | 'MultipleMarketDataProviders'
  | 'MultipleAIProviders'
  | 'AdvancedAnalytics'

export interface AuthTokens {
  accessToken: string
  refreshToken: string
}

// ─── Playbook ────────────────────────────────────────────────────────────────

export type AssetClass = 'Stocks' | 'Futures'

export interface Playbook {
  id: string
  name: string
  description: string
  assetClass: AssetClass
  instruments: string[]
  isActive: boolean
  version: number
  createdAt: string
  updatedAt: string
  rules: PlaybookRules
}

export interface PlaybookRules {
  tradingHoursStart?: string
  tradingHoursEnd?: string
  entryConditions: string[]
  exitConditions: string[]
  riskPerTrade?: number
  maxDailyRisk?: number
  stopLossRules: string[]
  stopManagementRules: string[]
  profitTargets: string[]
  trailingRules: string[]
  positionSizingRules: string[]
  maxTradesPerDay?: number
  additionalConditions: string[]
}

// ─── Market Data ─────────────────────────────────────────────────────────────

export interface Quote {
  instrument: string
  price: number
  bid: number
  ask: number
  change: number
  changePct: number
  timestamp: string
}

// ─── Position & Orders ───────────────────────────────────────────────────────

export interface Position {
  instrument: string
  assetClass: AssetClass
  side: 'Long' | 'Short'
  quantity: number
  entryPrice: number
  currentPrice: number
  stopPrice?: number
  targetPrice?: number
  unrealizedPnl: number
  unrealizedPnlPts?: number
  realizedPnl: number
}

export interface Order {
  id: string
  instrument: string
  side: 'Buy' | 'Sell'
  quantity: number
  orderType: 'Market' | 'Limit' | 'Stop' | 'StopLimit'
  limitPrice?: number
  stopPrice?: number
  status: 'Pending' | 'Filled' | 'Cancelled' | 'Rejected'
  createdAt: string
}

// ─── Coaching Session ─────────────────────────────────────────────────────────

export type SessionStatus = 'Idle' | 'Starting' | 'Active' | 'Paused' | 'Ending' | 'Ended'

export interface CoachingSession {
  id: string
  playbookId: string
  playbookName: string
  instrument: string
  assetClass: AssetClass
  status: SessionStatus
  startedAt: string
  endedAt?: string
  currentQuote?: Quote
  currentPosition?: Position
  openOrders: Order[]
  sessionPnl: number
  tradeCount: number
}

// ─── Rebel Events & Messages ─────────────────────────────────────────────────

export type RebelEventType =
  | 'POSITION_OPENED'
  | 'POSITION_CLOSED'
  | 'ORDER_CREATED'
  | 'ORDER_CHANGED'
  | 'STOP_CHANGED'
  | 'TARGET_CHANGED'
  | 'ENTRY_CONDITION_MET'
  | 'ENTRY_CONDITION_INVALIDATED'
  | 'STOP_THRESHOLD_REACHED'
  | 'PROFIT_TARGET_APPROACHING'
  | 'PLAYBOOK_RULE_TRIGGERED'
  | 'PLAYBOOK_RULE_VIOLATED'
  | 'MAX_DAILY_LOSS_REACHED'
  | 'MAX_TRADE_COUNT_REACHED'
  | 'VOLATILITY_CHANGED'
  | 'POSITION_RISK_CHANGED'
  | 'MARKET_SESSION_OPENED'
  | 'MARKET_SESSION_CLOSED'

export interface RebelMessage {
  id: string
  text: string
  timestamp: string
  eventType?: RebelEventType
  isUserQuestion: boolean
}

// ─── Provider Connections ─────────────────────────────────────────────────────

export type ProviderStatus = 'Connected' | 'Disconnected' | 'Connecting' | 'Error'

export interface ProviderConnection {
  providerId: string
  providerName: string
  providerType: 'Broker' | 'MarketData' | 'AI' | 'Voice'
  status: ProviderStatus
  keySource: 'REBEL_MANAGED' | 'USER_MANAGED_BYOK'
}

// ─── Session Pre-flight Check ─────────────────────────────────────────────────

export interface PreflightCheck {
  isAuthenticated: boolean
  hasActiveSubscription: boolean
  hasLiveCoachingEntitlement: boolean
  hasActivePlaybook: boolean
  isMarketDataConnected: boolean
  isAIConnected: boolean
  isBrokerConnected: boolean
}

// ─── Session History ──────────────────────────────────────────────────────────

export interface SessionSummary {
  id: string
  playbookName: string
  instrument: string
  startedAt: string
  endedAt: string
  durationMinutes: number
  tradeCount: number
  realizedPnl: number
  ruleViolations: number
  apiCost: number
}
