import { useState } from 'react'
import { api, useApi } from '../lib/api'
import { cn, dateTime, money, rate, toneFor } from '../lib/utils'
import { Badge, Button, Card, CardBody, EmptyState, ErrorNotice, Skeleton, Stat } from '../components/ui'

export function Portfolio() {
  const { data, loading, error, reload } = useApi(() => api.portfolio(), [])
  const [closing, setClosing] = useState<string>()
  const [message, setMessage] = useState<string>()

  async function close(id: string) {
    setClosing(id)
    setMessage(undefined)
    try {
      // No exit price supplied: the backend marks it out at the most recent close.
      await api.closePosition(id)
      reload()
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Could not close that position')
    } finally {
      setClosing(undefined)
    }
  }

  if (loading) return <Skeleton className="h-80 w-full" />
  if (error) return <ErrorNotice message={error} onRetry={reload} />
  if (!data) return null

  const open = data.positions.filter((p) => p.status === 'Open')
  const closed = data.positions.filter((p) => p.status === 'Closed')

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight text-ink-100">Paper portfolio</h1>
        <p className="mt-1 text-sm text-ink-500">
          Recommendations you chose to take, at the size you chose. Simulated — no order reaches a
          broker.
        </p>
      </div>

      {message && <ErrorNotice message={message} />}

      <Card>
        <CardBody className="grid grid-cols-2 gap-5 sm:grid-cols-4">
          <Stat label="Open positions" value={data.openPositions} />
          <Stat label="Deployed" value={money(data.deployedCapital)} />
          <Stat label="Realised P&L" value={money(data.realizedPnl)} tone={toneFor(data.realizedPnl)} />
          <Stat
            label="Realised return"
            value={rate(data.realizedPercent)}
            tone={toneFor(data.realizedPercent)}
            hint={`${data.closedPositions} closed`}
          />
        </CardBody>
      </Card>

      {data.positions.length === 0 ? (
        <Card>
          <EmptyState
            title="Nothing in the portfolio"
            description="Accept a recommendation from the morning brief with Build trade to start tracking it here."
          />
        </Card>
      ) : (
        <>
          <PositionTable
            title="Open"
            positions={open}
            onClose={close}
            closing={closing}
          />
          <PositionTable title="Closed" positions={closed} />
        </>
      )}
    </div>
  )
}

function PositionTable({
  title,
  positions,
  onClose,
  closing,
}: {
  title: string
  positions: Array<import('../lib/types').PaperPosition>
  onClose?: (id: string) => void
  closing?: string
}) {
  if (positions.length === 0) return null

  return (
    <Card>
      <div className="border-b border-ink-800 px-5 py-3">
        <h2 className="text-sm font-semibold text-ink-100">
          {title} <span className="text-ink-500">({positions.length})</span>
        </h2>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full min-w-[44rem] text-sm">
          <thead>
            <tr className="border-b border-ink-800 text-left text-[11px] uppercase tracking-wide text-ink-500">
              <th className="px-5 py-2 font-medium">Symbol</th>
              <th className="px-3 py-2 text-right font-medium">Shares</th>
              <th className="px-3 py-2 text-right font-medium">Entry</th>
              <th className="px-3 py-2 text-right font-medium">Stop</th>
              <th className="px-3 py-2 text-right font-medium">Target</th>
              <th className="px-3 py-2 text-right font-medium">Cost</th>
              <th className="px-3 py-2 text-right font-medium">Exit</th>
              <th className="px-3 py-2 text-right font-medium">P&L</th>
              <th className="px-5 py-2 font-medium">Opened</th>
              {onClose && <th className="px-5 py-2" />}
            </tr>
          </thead>
          <tbody>
            {positions.map((p) => (
              <tr key={p.id} className="border-b border-ink-850 last:border-0">
                <td className="px-5 py-2.5">
                  <div className="flex items-center gap-2">
                    <span className="font-semibold text-ink-100">{p.symbol}</span>
                    {p.exitReason && <Badge>{p.exitReason}</Badge>}
                  </div>
                </td>
                <td className="tabular px-3 py-2.5 text-right text-ink-300">{p.shares}</td>
                <td className="tabular px-3 py-2.5 text-right text-ink-300">{money(p.entryPrice)}</td>
                <td className="tabular px-3 py-2.5 text-right text-loss-400">{money(p.stopLoss)}</td>
                <td className="tabular px-3 py-2.5 text-right text-gain-400">{money(p.target)}</td>
                <td className="tabular px-3 py-2.5 text-right text-ink-300">{money(p.costBasis)}</td>
                <td className="tabular px-3 py-2.5 text-right text-ink-300">{money(p.exitPrice)}</td>
                <td className={cn('tabular px-3 py-2.5 text-right font-medium', toneFor(p.realizedPnl))}>
                  {p.realizedPnl === null ? '—' : `${money(p.realizedPnl)} (${rate(p.realizedPercent)})`}
                </td>
                <td className="px-5 py-2.5 text-ink-500">{dateTime(p.openedAtUtc)}</td>
                {onClose && (
                  <td className="px-5 py-2.5 text-right">
                    <Button
                      size="sm"
                      variant="danger"
                      onClick={() => onClose(p.id)}
                      disabled={closing === p.id}
                    >
                      Close
                    </Button>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  )
}
