import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Play } from 'lucide-react'
import { api, useApi } from '../lib/api'
import type { RunDetail } from '../lib/types'
import { dateTime, number } from '../lib/utils'
import { RecommendationCard } from '../components/RecommendationCard'
import { Button, Card, CardBody, EmptyState, ErrorNotice, Skeleton, Spinner, Stat } from '../components/ui'

export function Brief() {
  const { data, loading, error, reload } = useApi<RunDetail | undefined>(() => api.latestRun(), [])
  const [running, setRunning] = useState(false)
  const [runError, setRunError] = useState<string>()
  const [accepted, setAccepted] = useState<Set<string>>(new Set())
  const [accepting, setAccepting] = useState<string>()

  async function startRun() {
    setRunning(true)
    setRunError(undefined)
    try {
      await api.startRun()
      reload()
    } catch (err) {
      setRunError(err instanceof Error ? err.message : 'The run failed')
    } finally {
      setRunning(false)
    }
  }

  async function accept(recommendationId: string) {
    setAccepting(recommendationId)
    try {
      await api.openPosition(recommendationId)
      setAccepted((current) => new Set(current).add(recommendationId))
    } catch (err) {
      setRunError(err instanceof Error ? err.message : 'Could not add that to the portfolio')
    } finally {
      setAccepting(undefined)
    }
  }

  const run = data?.run

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-ink-100">Morning brief</h1>
          <p className="mt-1 text-sm text-ink-500">
            {run
              ? `Session ${run.tradingDate} · ${run.universeSize} names screened · ${run.candidateCount} cleared the filters`
              : 'Run the pipeline to see what the screen finds.'}
          </p>
        </div>

        <Button variant="primary" size="lg" onClick={startRun} disabled={running}>
          {running ? <Spinner /> : <Play className="h-4 w-4" />}
          {running ? 'Analysing the market…' : 'Run analysis'}
        </Button>
      </div>

      {runError && <ErrorNotice message={runError} onRetry={startRun} />}
      {error && <ErrorNotice message={error} onRetry={reload} />}

      {running && (
        <Card>
          <CardBody className="space-y-3">
            <p className="text-sm text-ink-300">
              Pulling market data, computing signals, ranking the universe, then writing the
              theses. This takes a few seconds.
            </p>
            <Skeleton className="h-3 w-2/3" />
            <Skeleton className="h-3 w-1/2" />
          </CardBody>
        </Card>
      )}

      {loading && !running && <Skeleton className="h-56 w-full" />}

      {run && !running && (
        <>
          <Card>
            <CardBody className="grid grid-cols-2 gap-5 sm:grid-cols-3 lg:grid-cols-6">
              <Stat label="Status" value={run.status} />
              <Stat label="Universe" value={run.universeSize} />
              <Stat label="Passed filters" value={run.candidateCount} />
              <Stat label="Recommended" value={run.recommendationCount} />
              <Stat
                label="Took"
                value={run.durationSeconds ? `${number(run.durationSeconds, 1)}s` : '—'}
              />
              <Stat label="Started" value={dateTime(run.startedAtUtc)} hint={run.configHash} />
            </CardBody>
          </Card>

          {run.errorMessage && <ErrorNotice message={run.errorMessage} />}

          {data.recommendations.length === 0 ? (
            <Card>
              <EmptyState
                title="Nothing worth trading today"
                description={
                  run.status === 'CompletedEmpty'
                    ? 'Every candidate either failed a filter or could not produce a trade plan worth its risk. Recommending nothing is a valid outcome — the screen is not obliged to find something.'
                    : 'This run produced no recommendations.'
                }
                action={
                  <Link to={`/runs/${run.id}`}>
                    <Button size="sm">See what was screened</Button>
                  </Link>
                }
              />
            </Card>
          ) : (
            <div className="space-y-4">
              {data.recommendations.map((rec) => (
                <RecommendationCard
                  key={rec.id}
                  recommendation={rec}
                  accepting={accepting === rec.id}
                  onAccept={accepted.has(rec.id) ? undefined : accept}
                />
              ))}
            </div>
          )}

          <div className="flex justify-center">
            <Link to={`/runs/${run.id}`}>
              <Button variant="ghost" size="sm">
                Show the full screen and the stage log
              </Button>
            </Link>
          </div>
        </>
      )}

      {!loading && !run && !running && (
        <Card>
          <EmptyState
            title="No runs yet"
            description="Run the analysis to screen the universe, rank the candidates and generate recommendations."
            action={
              <Button variant="primary" onClick={startRun}>
                <Play className="h-4 w-4" />
                Run analysis
              </Button>
            }
          />
        </Card>
      )}
    </div>
  )
}
