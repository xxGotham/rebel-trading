import { useState } from 'react'
import { ChevronDown, ExternalLink, ShieldCheck, TrendingDown, TrendingUp } from 'lucide-react'
import type { Recommendation } from '../lib/types'
import { cn, money, number, rate, shortDate, toneFor } from '../lib/utils'
import { Badge, Button, Card, CardBody, CardHeader, Stat } from './ui'

const confidenceTone = { High: 'rebel', Medium: 'neutral', Low: 'neutral' } as const

const outcomeTone = {
  TargetHit: 'gain',
  StopHit: 'loss',
  Expired: 'neutral',
  NeverTriggered: 'neutral',
  Open: 'warn',
} as const

const outcomeLabel = {
  TargetHit: 'Target hit',
  StopHit: 'Stopped out',
  Expired: 'Closed at expiry',
  NeverTriggered: 'Never triggered',
  Open: 'Open',
} as const

export function RecommendationCard({
  recommendation: rec,
  onAccept,
  accepting,
}: {
  recommendation: Recommendation
  onAccept?: (id: string) => void
  accepting?: boolean
}) {
  const [expanded, setExpanded] = useState(false)

  return (
    <Card className="animate-fade-up overflow-hidden">
      <CardHeader className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="text-lg font-bold tracking-tight text-ink-100">{rec.symbol}</h3>
            <Badge tone={confidenceTone[rec.confidence]}>{rec.confidence} confidence</Badge>
            <Badge>{rec.strategy}</Badge>
            {rec.outcome && rec.outcome.status !== 'Open' && (
              <Badge tone={outcomeTone[rec.outcome.status]}>
                {outcomeLabel[rec.outcome.status]}
                {rec.outcome.returnPercent !== null && ` ${rate(rec.outcome.returnPercent)}`}
              </Badge>
            )}
          </div>
          <p className="mt-1 truncate text-sm text-ink-500">
            {rec.name ?? rec.symbol}
            {rec.sector && ` · ${rec.sector}`}
            {` · rank #${rec.rank} of the screen`}
          </p>
        </div>

        <div className="text-right">
          <div className="tabular text-2xl font-bold text-rebel-400">{number(rec.score, 1)}</div>
          <div className="text-[11px] uppercase tracking-wide text-ink-500">score</div>
        </div>
      </CardHeader>

      <CardBody className="space-y-4">
        <p className="text-sm leading-relaxed text-ink-100">{rec.thesis}</p>

        {/* Every figure below was computed by the pipeline, never by the model. */}
        <div className="grid grid-cols-2 gap-4 rounded-lg border border-ink-800 bg-ink-950/60 p-4 sm:grid-cols-3 lg:grid-cols-6">
          <Stat label="Entry" value={`${money(rec.entryLow)} – ${money(rec.entryHigh)}`} />
          <Stat
            label="Target"
            value={money(rec.target)}
            tone="text-gain-400"
            hint={`+${rate(rec.targetDistancePercent)}`}
          />
          <Stat
            label="Stop"
            value={money(rec.stopLoss)}
            tone="text-loss-400"
            hint={`−${rate(rec.stopDistancePercent)}`}
          />
          <Stat label="Risk / reward" value={`${number(rec.riskRewardRatio)} : 1`} />
          <Stat
            label="Size"
            value={`${rec.suggestedShares} sh`}
            hint={`${money(rec.suggestedRiskAmount)} at risk`}
          />
          <Stat label="Hold" value={`${rec.holdingPeriodDays} days`} />
        </div>

        {rec.outcome && rec.outcome.status !== 'Open' && (
          <div className="grid grid-cols-2 gap-4 rounded-lg border border-ink-800 px-4 py-3 sm:grid-cols-4">
            <Stat
              label="Result"
              value={rate(rec.outcome.returnPercent)}
              tone={toneFor(rec.outcome.returnPercent)}
            />
            <Stat label="R multiple" value={number(rec.outcome.rMultiple)} tone={toneFor(rec.outcome.rMultiple)} />
            <Stat
              label="vs S&P"
              value={rate(rec.outcome.benchmarkReturnPercent)}
              tone={toneFor(rec.outcome.benchmarkReturnPercent)}
            />
            <Stat
              label="Held"
              value={rec.outcome.daysHeld !== null ? `${rec.outcome.daysHeld} days` : '—'}
              hint={
                rec.outcome.entryDate
                  ? `${shortDate(rec.outcome.entryDate)} → ${shortDate(rec.outcome.exitDate)}`
                  : undefined
              }
            />
          </div>
        )}

        <button
          onClick={() => setExpanded((value) => !value)}
          className="flex w-full items-center justify-between rounded-lg px-1 py-1 text-sm font-medium text-ink-300 transition-colors hover:text-ink-100"
          aria-expanded={expanded}
        >
          <span>{expanded ? 'Hide the full case' : 'Dig deeper'}</span>
          <ChevronDown className={cn('h-4 w-4 transition-transform', expanded && 'rotate-180')} />
        </button>

        {expanded && (
          <div className="animate-fade-up space-y-4 border-t border-ink-800 pt-4">
            <Case icon={<TrendingUp className="h-4 w-4 text-gain-400" />} title="Bull case" body={rec.bullCase} />
            <Case icon={<TrendingDown className="h-4 w-4 text-loss-400" />} title="Bear case" body={rec.bearCase} />
            <Case icon={<ShieldCheck className="h-4 w-4 text-ink-500" />} title="What breaks it" body={rec.risks} />
            <Case title="Catalyst" body={rec.catalystSummary} />
            <Case title="Why this one" body={rec.whyThisOne} />

            {rec.news.length > 0 && (
              <div>
                <h4 className="text-xs font-semibold uppercase tracking-wide text-ink-500">Sources</h4>
                <ul className="mt-2 space-y-1.5">
                  {rec.news.map((item) => (
                    <li key={item.id}>
                      <a
                        href={item.url}
                        target="_blank"
                        rel="noreferrer noopener"
                        className="group flex items-start gap-2 text-sm text-ink-300 hover:text-rebel-400"
                      >
                        <ExternalLink className="mt-0.5 h-3.5 w-3.5 shrink-0 opacity-60" />
                        <span>
                          {item.headline}
                          <span className="ml-2 text-xs text-ink-500">
                            {item.source} · {shortDate(item.publishedAtUtc)}
                          </span>
                        </span>
                      </a>
                    </li>
                  ))}
                </ul>
              </div>
            )}

            <p className="text-[11px] text-ink-500">
              Prose written by {rec.model} ({rec.promptVersion}). Every price, level and size on this
              card was computed by the pipeline — the model is not permitted to state a number.
              {rec.validationNotes && ` Validator note: ${rec.validationNotes}`}
            </p>
          </div>
        )}
      </CardBody>

      {onAccept && (
        <div className="flex items-center justify-between gap-3 border-t border-ink-800 bg-ink-950/40 px-5 py-3">
          <span className="text-xs text-ink-500">
            Paper only — nothing is sent to a broker.
          </span>
          <Button variant="primary" size="sm" onClick={() => onAccept(rec.id)} disabled={accepting}>
            Build trade
          </Button>
        </div>
      )}
    </Card>
  )
}

function Case({ icon, title, body }: { icon?: React.ReactNode; title: string; body: string }) {
  if (!body) return null

  return (
    <div>
      <h4 className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-ink-500">
        {icon}
        {title}
      </h4>
      <p className="mt-1.5 text-sm leading-relaxed text-ink-300">{body}</p>
    </div>
  )
}
