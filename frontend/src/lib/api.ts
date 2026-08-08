import { useCallback, useEffect, useState } from 'react'
import type {
  Performance,
  Portfolio,
  Recommendation,
  RunDetail,
  RunSummary,
  SystemStatus,
} from './types'

const BASE = import.meta.env.VITE_API_BASE ?? ''

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly detail?: string,
  ) {
    super(message)
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${BASE}${path}`, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...init?.headers },
  })

  if (response.status === 204) return undefined as T

  const text = await response.text()
  const body = text ? JSON.parse(text) : undefined

  if (!response.ok) {
    // The backend returns ProblemDetails for pipeline failures; surfacing the
    // detail matters because it names the stage that actually broke.
    throw new ApiError(
      body?.title ?? body?.error ?? `Request failed (${response.status})`,
      response.status,
      body?.detail,
    )
  }

  return body as T
}

export const api = {
  status: () => request<SystemStatus>('/api/status'),
  runs: (limit = 25) => request<RunSummary[]>(`/api/runs?limit=${limit}`),
  latestRun: () => request<RunDetail | undefined>('/api/runs/latest'),
  run: (id: string) => request<RunDetail>(`/api/runs/${id}`),
  startRun: (tradingDate?: string) =>
    request<RunSummary>('/api/runs', {
      method: 'POST',
      body: JSON.stringify({ tradingDate: tradingDate ?? null, refreshMarketData: true }),
    }),
  recommendations: (limit = 50) => request<Recommendation[]>(`/api/recommendations?limit=${limit}`),
  performance: () => request<Performance>('/api/performance'),
  grade: () => request<{ examined: number; graded: number; stillOpen: number }>(
    '/api/performance/grade',
    { method: 'POST' },
  ),
  portfolio: () => request<Portfolio>('/api/paper/portfolio'),
  openPosition: (recommendationId: string, shares?: number) =>
    request('/api/paper/positions', {
      method: 'POST',
      body: JSON.stringify({ recommendationId, shares: shares ?? null, entryPrice: null }),
    }),
  closePosition: (id: string, exitPrice?: number, reason = 'manual') =>
    request(`/api/paper/positions/${id}/close`, {
      method: 'POST',
      body: JSON.stringify({ exitPrice: exitPrice ?? null, reason }),
    }),
}

export interface AsyncState<T> {
  data: T | undefined
  loading: boolean
  error: string | undefined
  reload: () => void
}

/** Minimal fetch-on-mount hook — this app has no need for a caching layer yet. */
export function useApi<T>(fetcher: () => Promise<T>, deps: unknown[] = []): AsyncState<T> {
  const [data, setData] = useState<T>()
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string>()
  const [nonce, setNonce] = useState(0)

  // eslint-disable-next-line react-hooks/exhaustive-deps
  const run = useCallback(fetcher, deps)

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    setError(undefined)

    run()
      .then((result) => {
        if (!cancelled) setData(result)
      })
      .catch((err: unknown) => {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Something went wrong')
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })

    return () => {
      cancelled = true
    }
  }, [run, nonce])

  return { data, loading, error, reload: () => setNonce((n) => n + 1) }
}
