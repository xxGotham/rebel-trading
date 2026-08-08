import { NavLink, Outlet } from 'react-router-dom'
import { AlertTriangle, BarChart3, Briefcase, History, Zap } from 'lucide-react'
import { api, useApi } from '../lib/api'
import { cn } from '../lib/utils'

const links = [
  { to: '/', label: 'Brief', icon: Zap, end: true },
  { to: '/runs', label: 'Runs', icon: History, end: false },
  { to: '/performance', label: 'Performance', icon: BarChart3, end: false },
  { to: '/portfolio', label: 'Portfolio', icon: Briefcase, end: false },
]

export function Layout() {
  const { data: status } = useApi(() => api.status(), [])

  return (
    <div className="min-h-dvh">
      <header className="sticky top-0 z-20 border-b border-ink-800 bg-ink-950/85 backdrop-blur">
        <div className="mx-auto flex max-w-7xl flex-wrap items-center gap-x-6 gap-y-3 px-4 py-3 sm:px-6">
          <NavLink to="/" className="flex items-center gap-2">
            <span className="grid h-8 w-8 place-items-center rounded-lg bg-rebel-500 font-black text-ink-950">
              R
            </span>
            <span className="text-sm font-bold tracking-tight text-ink-100">
              Rebel<span className="text-rebel-400"> Trading</span>
            </span>
          </NavLink>

          <nav className="flex items-center gap-1">
            {links.map(({ to, label, icon: Icon, end }) => (
              <NavLink
                key={to}
                to={to}
                end={end}
                className={({ isActive }) =>
                  cn(
                    'flex items-center gap-2 rounded-lg px-3 py-1.5 text-sm font-medium transition-colors',
                    isActive ? 'bg-ink-850 text-ink-100' : 'text-ink-500 hover:text-ink-300',
                  )
                }
              >
                <Icon className="h-4 w-4" />
                {label}
              </NavLink>
            ))}
          </nav>

          {status && (
            <div className="ml-auto hidden items-center gap-3 text-xs text-ink-500 lg:flex">
              <span>{status.marketDataProvider} data</span>
              <span className="text-ink-800">·</span>
              <span>{status.thesisModel}</span>
              <span className="text-ink-800">·</span>
              <span>{status.scoringVersion}</span>
            </div>
          )}
        </div>

        {status?.usingSyntheticData && <SyntheticDataWarning />}
      </header>

      <main className="mx-auto max-w-7xl px-4 py-6 sm:px-6 sm:py-8">
        <Outlet />
      </main>

      <footer className="mx-auto max-w-7xl px-4 py-8 text-xs text-ink-500 sm:px-6">
        Paper trading only. Nothing here is investment advice, and no order reaches a broker.
      </footer>
    </div>
  )
}

/**
 * Deliberately impossible to miss.
 *
 * A user who mistakes generated prices for real ones could act on a
 * recommendation built from noise. That failure is severe enough to justify a
 * permanent banner rather than a subtle badge.
 */
function SyntheticDataWarning() {
  return (
    <div className="border-t border-amber-500/30 bg-amber-500/10">
      <div className="mx-auto flex max-w-7xl items-center gap-2 px-4 py-2 text-xs font-medium text-amber-400 sm:px-6">
        <AlertTriangle className="h-4 w-4 shrink-0" />
        <span>
          Synthetic market data. These prices are generated for development and have no relationship
          to any real security. Add Alpaca credentials to analyse the live market.
        </span>
      </div>
    </div>
  )
}
