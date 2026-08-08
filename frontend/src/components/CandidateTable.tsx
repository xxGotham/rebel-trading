import { useState } from 'react'
import type { Candidate } from '../lib/types'
import { cn, money, number, rate, toneFor } from '../lib/utils'
import { Badge, Button } from './ui'

/**
 * The ranked screen, including what was thrown out and why.
 *
 * Showing the rejects is the point: a recommendation only means something
 * relative to the field it beat, and an exclusion reason is the cheapest way to
 * catch a filter that is quietly eating the entire universe.
 */
export function CandidateTable({ candidates }: { candidates: Candidate[] }) {
  const [showExcluded, setShowExcluded] = useState(false)

  const included = candidates.filter((c) => !c.exclusionReason)
  const excluded = candidates.filter((c) => c.exclusionReason)
  const rows = showExcluded ? excluded : included

  return (
    <div>
      <div className="flex flex-wrap items-center gap-2 px-5 py-3">
        <Button
          size="sm"
          variant={showExcluded ? 'ghost' : 'secondary'}
          onClick={() => setShowExcluded(false)}
        >
          Ranked ({included.length})
        </Button>
        <Button
          size="sm"
          variant={showExcluded ? 'secondary' : 'ghost'}
          onClick={() => setShowExcluded(true)}
        >
          Filtered out ({excluded.length})
        </Button>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full min-w-[46rem] text-sm">
          <thead>
            <tr className="border-y border-ink-800 text-left text-[11px] uppercase tracking-wide text-ink-500">
              <th className="px-5 py-2 font-medium">#</th>
              <th className="px-3 py-2 font-medium">Symbol</th>
              <th className="px-3 py-2 text-right font-medium">Score</th>
              <th className="px-3 py-2 text-right font-medium">Price</th>
              {showExcluded ? (
                <th className="px-5 py-2 font-medium">Why it was dropped</th>
              ) : (
                <>
                  <th className="px-3 py-2 text-right font-medium">Gap</th>
                  <th className="px-3 py-2 text-right font-medium">Rel vol</th>
                  <th className="px-3 py-2 text-right font-medium">20d mom</th>
                  <th className="px-3 py-2 text-right font-medium">vs SPY</th>
                  <th className="px-3 py-2 text-right font-medium">RSI</th>
                  <th className="px-3 py-2 text-right font-medium">Off high</th>
                  <th className="px-5 py-2 text-right font-medium">ATR</th>
                </>
              )}
            </tr>
          </thead>
          <tbody>
            {rows.map((c) => (
              <tr
                key={c.symbol}
                className={cn(
                  'border-b border-ink-850 transition-colors hover:bg-ink-850/50',
                  c.selected && 'bg-rebel-500/5',
                )}
              >
                <td className="tabular px-5 py-2 text-ink-500">{c.exclusionReason ? '—' : c.rank}</td>
                <td className="px-3 py-2">
                  <div className="flex items-center gap-2">
                    <span className="font-semibold text-ink-100">{c.symbol}</span>
                    {c.selected && <Badge tone="rebel">rec</Badge>}
                  </div>
                </td>
                <td className="tabular px-3 py-2 text-right font-medium text-ink-100">
                  {c.exclusionReason ? '—' : number(c.score, 1)}
                </td>
                <td className="tabular px-3 py-2 text-right text-ink-300">{money(c.lastPrice)}</td>

                {showExcluded ? (
                  <td className="px-5 py-2 text-ink-500">{c.exclusionReason}</td>
                ) : (
                  <>
                    <td className={cn('tabular px-3 py-2 text-right', toneFor(c.gapPercent))}>
                      {rate(c.gapPercent)}
                    </td>
                    <td className="tabular px-3 py-2 text-right text-ink-300">{number(c.relativeVolume, 3)}</td>
                    <td className={cn('tabular px-3 py-2 text-right', toneFor(c.momentum20))}>
                      {rate(c.momentum20)}
                    </td>
                    <td className={cn('tabular px-3 py-2 text-right', toneFor(c.relativeStrengthVsMarket))}>
                      {rate(c.relativeStrengthVsMarket)}
                    </td>
                    <td className="tabular px-3 py-2 text-right text-ink-300">{number(c.rsi14, 0)}</td>
                    <td className="tabular px-3 py-2 text-right text-ink-300">{rate(c.percentOff20DayHigh)}</td>
                    <td className="tabular px-5 py-2 text-right text-ink-300">{rate(c.atrPercent)}</td>
                  </>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {rows.length === 0 && (
        <p className="px-5 py-8 text-center text-sm text-ink-500">Nothing in this list for that run.</p>
      )}
    </div>
  )
}
