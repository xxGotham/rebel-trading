export interface SystemStatus {
  marketDataProvider: string
  newsProvider: string
  thesisModel: string
  usingSyntheticData: boolean
  usingLlm: boolean
  database: string
  scoringVersion: string
  configHash: string
  scheduledRunsEnabled: boolean
  nextTradingDate: string
}

export interface RunSummary {
  id: string
  tradingDate: string
  status: 'Pending' | 'Running' | 'Completed' | 'Failed' | 'CompletedEmpty'
  trigger: string
  startedAtUtc: string
  finishedAtUtc: string | null
  durationSeconds: number | null
  universeSize: number
  candidateCount: number
  recommendationCount: number
  scoringVersion: string
  promptVersion: string
  configHash: string
  model: string | null
  promptTokens: number
  completionTokens: number
  errorMessage: string | null
}

export interface NewsItem {
  id: string
  headline: string
  source: string
  url: string
  publishedAtUtc: string
}

export interface Outcome {
  status: 'Open' | 'TargetHit' | 'StopHit' | 'Expired' | 'NeverTriggered'
  entryDate: string | null
  entryPrice: number | null
  exitDate: string | null
  exitPrice: number | null
  returnPercent: number | null
  rMultiple: number | null
  maxFavorablePercent: number | null
  maxAdversePercent: number | null
  daysHeld: number | null
  benchmarkReturnPercent: number | null
  gradedAtUtc: string | null
}

export interface Recommendation {
  id: string
  runId: string
  symbol: string
  name: string | null
  sector: string | null
  tradingDate: string
  direction: string
  strategy: string
  confidence: 'Low' | 'Medium' | 'High'
  score: number
  rank: number
  referencePrice: number
  entryLow: number
  entryHigh: number
  target: number
  stopLoss: number
  riskRewardRatio: number
  stopDistancePercent: number
  targetDistancePercent: number
  suggestedShares: number
  suggestedRiskAmount: number
  holdingPeriodDays: number
  thesis: string
  bullCase: string
  bearCase: string
  risks: string
  catalystSummary: string
  whyThisOne: string
  model: string
  promptVersion: string
  validationNotes: string | null
  news: NewsItem[]
  outcome: Outcome | null
}

export interface Candidate {
  symbol: string
  name: string | null
  rank: number
  score: number
  selected: boolean
  exclusionReason: string | null
  lastPrice: number
  gapPercent: number
  relativeVolume: number
  rsi14: number
  atrPercent: number
  momentum20: number
  percentOff20DayHigh: number
  relativeStrengthVsMarket: number
  sectorStrength: number
  newsCount: number
}

export interface RunDetail {
  run: RunSummary
  stageLog: string[]
  recommendations: Recommendation[]
  candidates: Candidate[]
}

export interface Stats {
  segment: string
  trades: number
  wins: number
  losses: number
  neverTriggered: number
  winRate: number
  averageReturnPercent: number
  averageWinPercent: number
  averageLossPercent: number
  bestPercent: number
  worstPercent: number
  profitFactor: number
  expectancy: number
  averageHoldingDays: number
  averageBenchmarkPercent: number
  alphaPercent: number
  compoundedReturnPercent: number
}

export interface Performance {
  overall: Stats
  byStrategy: Stats[]
  byConfidence: Stats[]
  byOutcome: Stats[]
  byHoldingPeriod: Stats[]
}

export interface PaperPosition {
  id: string
  recommendationId: string
  symbol: string
  status: 'Open' | 'Closed'
  openedAtUtc: string
  entryPrice: number
  shares: number
  stopLoss: number
  target: number
  costBasis: number
  closedAtUtc: string | null
  exitPrice: number | null
  exitReason: string | null
  realizedPnl: number | null
  realizedPercent: number | null
}

export interface Portfolio {
  openPositions: number
  closedPositions: number
  deployedCapital: number
  realizedPnl: number
  realizedPercent: number
  positions: PaperPosition[]
}
