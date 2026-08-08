import { useState } from 'react'
import { RefreshCw } from 'lucide-react'
import { api, useApi } from '../lib/api'
import type { Stats } from '../lib/types'
import { cn, number, rate, toneFor } from '../lib/utils'
import { Button, Card, CardBody, CardHeader, EmptyState, ErrorNotice, Skeleton, Spinner, Stat } from '../components/ui'

export function Performance() {
  const { data, loading, error, reload } = useApi(() => api.performance(), [])
  const [grading, setGrading] = useState(false)
  const [message, setMessage] = useState<string>()

  async function grade() {
    setGrading(true)
    setMessage(undefined)
    try {
      const result = await api.grade()
      setMessage(
        `${result.graded} graded, ${result.stillOpen} still inside their holding window.`,
      )
      reload()
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Grading failed')
    } finally {
      setGrading(false)
    }
  }

  if (loading) return <Skeleton className="h-96 w-full" />
  if (error) return <ErrorNotice message={error} onRetry={reload} />

  const overall = data?.overall

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-ink-100">Performance</h1>
          <p className="mt-1 max-w-2xl text-sm text-ink-500">
            Every recommendation ever made is graded against what the market actually did — the
            losers included. Ideas that never traded into their entry band are excluded, since they
            say nothing about signal quality.
          </p>
        </div>

        <Button onClick={grade} disabled={grading}>
          {grading ? <Spinner /> : <RefreshCw className="h-4 w-4" />}
          Grade now
        </Button>
      </div>

      {message && <p className="text-sm text-ink-300">{message}</p>}

      {!overall || overall.trades === 0 ? (
        <Card>
          <EmptyState
            title="No graded history yet"
            description="Recommendations are graded once their holding window closes or price resolves to the target or the stop. Run the pipeline over a past session to see this populate."
          />
        </Card>
      ) : (
        <>
          <Card>
            <CardBody className="grid grid-cols-2 gap-5 sm:grid-cols-4 lg:grid-cols-8">
              <Stat label="Trades" value={overall.trades} hint={`${overall.neverTriggered} never triggered`} />
              <Stat label="Win rate" value={rate(overall.winRate, 0)} />
              <Stat
                label="Avg return"
                value={rate(overall.averageReturnPercent)}
                tone={toneFor(overall.averageReturnPercent)}
              />
              <Stat label="Avg winner" value={rate(overall.averageWinPercent)} tone="text-gain-400" />
              <Stat label="Avg loser" value={rate(overall.averageLossPercent)} tone="text-loss-400" />
              <Stat label="Profit factor" value={number(overall.profitFactor)} />
              <Stat
                label="Expectancy"
                value={`${number(overall.expectancy)}R`}
                tone={toneFor(overall.expectancy)}
              />
              <Stat
                label="Alpha vs S&P"
                value={rate(overall.alphaPercent)}
                tone={toneFor(overall.alphaPercent)}
                hint={`S&P ${rate(overall.averageBenchmarkPercent)}`}
              />
            </CardBody>
          </Card>

          <div className="grid gap-6 lg:grid-cols-2">
            <Breakdown title="By strategy" subtitle="Which named strategy is actually working." rows={data.byStrategy} />
            <Breakdown
              title="By confidence"
              subtitle="If high-confidence ideas do not beat low-confidence ones, the score is not measuring conviction."
              rows={data.byConfidence}
            />
            <Breakdown title="By holding period" subtitle="How long the edge takes to show up." rows={data.byHoldingPeriod} />
            <Breakdown title="By outcome" subtitle="How trades resolve: target, stop, or expiry." rows={data.byOutcome} />
          </div>
        </>
      )}
    </div>
  )
}

function Breakdown({ title, subtitle, rows }: { title: string; subtitle: string; rows: Stats[] }) {
  return (
    <Card>
      <CardHeader>
        <h2 className="text-sm font-semibold text-ink-100">{title}</h2>
        <p className="mt-0.5 text-xs text-ink-500">{subtitle}</p>
      </CardHeader>

      {rows.length === 0 ? (
        <CardBody>
          <p className="text-sm text-ink-500">Nothing graded in this dimension yet.</p>
        </CardBody>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[30rem] text-sm">
            <thead>
              <tr className="border-b border-ink-800 text-left text-[11px] uppercase tracking-wide text-ink-500">
                <th className="px-5 py-2 font-medium">Segment</th>
                <th className="px-3 py-2 text-right font-medium">n</th>
                <th className="px-3 py-2 text-right font-medium">Win rate</th>
                <th className="px-3 py-2 text-right font-medium">Avg</th>
                <th className="px-3 py-2 text-right font-medium">Expectancy</th>
                <th className="px-5 py-2 text-right font-medium">Alpha</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.segment} className="border-b border-ink-850 last:border-0">
                  <td className="px-5 py-2 font-medium text-ink-100">{row.segment}</td>
                  <td className="tabular px-3 py-2 text-right text-ink-500">{row.trades}</td>
                  <td className="tabular px-3 py-2 text-right text-ink-300">{rate(row.winRate, 0)}</td>
                  <td className={cn('tabular px-3 py-2 text-right', toneFor(row.averageReturnPercent))}>
                    {rate(row.averageReturnPercent)}
                  </td>
                  <td className={cn('tabular px-3 py-2 text-right', toneFor(row.expectancy))}>
                    {number(row.expectancy)}R
                  </td>
                  <td className={cn('tabular px-5 py-2 text-right', toneFor(row.alphaPercent))}>
                    {rate(row.alphaPercent)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Card>
  )
}
