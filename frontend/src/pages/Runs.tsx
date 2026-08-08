import { Link, useParams } from 'react-router-dom'
import { api, useApi } from '../lib/api'
import { dateTime, number } from '../lib/utils'
import { CandidateTable } from '../components/CandidateTable'
import { RecommendationCard } from '../components/RecommendationCard'
import {
  Badge,
  Card,
  CardBody,
  CardHeader,
  EmptyState,
  ErrorNotice,
  Skeleton,
  Stat,
} from '../components/ui'

const statusTone = {
  Completed: 'gain',
  CompletedEmpty: 'neutral',
  Failed: 'loss',
  Running: 'warn',
  Pending: 'neutral',
} as const

export function RunList() {
  const { data, loading, error, reload } = useApi(() => api.runs(50), [])

  if (loading) return <Skeleton className="h-64 w-full" />
  if (error) return <ErrorNotice message={error} onRetry={reload} />

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold tracking-tight text-ink-100">Runs</h1>

      <Card>
        {data && data.length > 0 ? (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[42rem] text-sm">
              <thead>
                <tr className="border-b border-ink-800 text-left text-[11px] uppercase tracking-wide text-ink-500">
                  <th className="px-5 py-3 font-medium">Session</th>
                  <th className="px-3 py-3 font-medium">Status</th>
                  <th className="px-3 py-3 font-medium">Trigger</th>
                  <th className="px-3 py-3 text-right font-medium">Universe</th>
                  <th className="px-3 py-3 text-right font-medium">Candidates</th>
                  <th className="px-3 py-3 text-right font-medium">Recs</th>
                  <th className="px-3 py-3 text-right font-medium">Took</th>
                  <th className="px-5 py-3 font-medium">Started</th>
                </tr>
              </thead>
              <tbody>
                {data.map((run) => (
                  <tr key={run.id} className="border-b border-ink-850 hover:bg-ink-850/50">
                    <td className="px-5 py-2.5">
                      <Link to={`/runs/${run.id}`} className="font-medium text-ink-100 hover:text-rebel-400">
                        {run.tradingDate}
                      </Link>
                    </td>
                    <td className="px-3 py-2.5">
                      <Badge tone={statusTone[run.status]}>{run.status}</Badge>
                    </td>
                    <td className="px-3 py-2.5 text-ink-500">{run.trigger}</td>
                    <td className="tabular px-3 py-2.5 text-right text-ink-300">{run.universeSize}</td>
                    <td className="tabular px-3 py-2.5 text-right text-ink-300">{run.candidateCount}</td>
                    <td className="tabular px-3 py-2.5 text-right text-ink-100">{run.recommendationCount}</td>
                    <td className="tabular px-3 py-2.5 text-right text-ink-500">
                      {run.durationSeconds ? `${number(run.durationSeconds, 1)}s` : '—'}
                    </td>
                    <td className="px-5 py-2.5 text-ink-500">{dateTime(run.startedAtUtc)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <EmptyState title="No runs yet" description="Start one from the morning brief." />
        )}
      </Card>
    </div>
  )
}

export function RunDetailPage() {
  const { id = '' } = useParams()
  const { data, loading, error, reload } = useApi(() => api.run(id), [id])

  if (loading) return <Skeleton className="h-96 w-full" />
  if (error) return <ErrorNotice message={error} onRetry={reload} />
  if (!data) return null

  const { run } = data

  return (
    <div className="space-y-6">
      <div>
        <Link to="/runs" className="text-sm text-ink-500 hover:text-ink-300">
          ← All runs
        </Link>
        <h1 className="mt-2 flex flex-wrap items-center gap-3 text-2xl font-bold tracking-tight text-ink-100">
          Session {run.tradingDate}
          <Badge tone={statusTone[run.status]}>{run.status}</Badge>
        </h1>
      </div>

      <Card>
        <CardBody className="grid grid-cols-2 gap-5 sm:grid-cols-4 lg:grid-cols-7">
          <Stat label="Universe" value={run.universeSize} />
          <Stat label="Candidates" value={run.candidateCount} />
          <Stat label="Recommended" value={run.recommendationCount} />
          <Stat label="Scoring" value={run.scoringVersion} hint={run.configHash} />
          <Stat label="Prompt" value={run.promptVersion} />
          <Stat label="Model" value={run.model ?? '—'} hint={
            run.promptTokens ? `${run.promptTokens + run.completionTokens} tokens` : undefined
          } />
          <Stat label="Took" value={run.durationSeconds ? `${number(run.durationSeconds, 1)}s` : '—'} />
        </CardBody>
      </Card>

      {run.errorMessage && <ErrorNotice message={run.errorMessage} />}

      {data.recommendations.length > 0 && (
        <div className="space-y-4">
          <h2 className="text-lg font-semibold text-ink-100">Recommendations</h2>
          {data.recommendations.map((rec) => (
            <RecommendationCard key={rec.id} recommendation={rec} />
          ))}
        </div>
      )}

      <Card>
        <CardHeader>
          <h2 className="text-sm font-semibold text-ink-100">The screen</h2>
          <p className="mt-0.5 text-xs text-ink-500">
            Every name that was considered, and the reason each one was dropped.
          </p>
        </CardHeader>
        <CandidateTable candidates={data.candidates} />
      </Card>

      <Card>
        <CardHeader>
          <h2 className="text-sm font-semibold text-ink-100">Stage log</h2>
          <p className="mt-0.5 text-xs text-ink-500">What each step of the pipeline actually did.</p>
        </CardHeader>
        <CardBody>
          <pre className="overflow-x-auto font-mono text-xs leading-relaxed text-ink-300">
            {data.stageLog.join('\n')}
          </pre>
        </CardBody>
      </Card>
    </div>
  )
}
