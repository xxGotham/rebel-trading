import type { ButtonHTMLAttributes, HTMLAttributes, ReactNode } from 'react'
import { cn } from '../lib/utils'

/*
  A small hand-rolled component layer in the shadcn idiom: plain elements,
  variants via class maps, no runtime dependency. The app needs six primitives,
  not a design system.
*/

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: 'primary' | 'secondary' | 'ghost' | 'danger'
  size?: 'sm' | 'md' | 'lg'
}

const buttonVariants = {
  primary: 'bg-rebel-500 text-ink-950 hover:bg-rebel-400 font-semibold',
  secondary: 'bg-ink-800 text-ink-100 hover:bg-ink-700 border border-ink-700',
  ghost: 'text-ink-300 hover:text-ink-100 hover:bg-ink-850',
  danger: 'bg-loss-500/15 text-loss-400 hover:bg-loss-500/25 border border-loss-500/30',
}

const buttonSizes = {
  sm: 'h-8 px-3 text-xs',
  md: 'h-10 px-4 text-sm',
  lg: 'h-12 px-6 text-base',
}

export function Button({
  variant = 'secondary',
  size = 'md',
  className,
  ...props
}: ButtonProps) {
  return (
    <button
      className={cn(
        'inline-flex items-center justify-center gap-2 rounded-lg transition-colors',
        'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-rebel-400',
        'disabled:pointer-events-none disabled:opacity-50',
        buttonVariants[variant],
        buttonSizes[size],
        className,
      )}
      {...props}
    />
  )
}

export function Card({ className, ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={cn('rounded-xl border border-ink-800 bg-ink-900', className)}
      {...props}
    />
  )
}

export function CardHeader({ className, ...props }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cn('border-b border-ink-800 px-5 py-4', className)} {...props} />
}

export function CardBody({ className, ...props }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cn('px-5 py-4', className)} {...props} />
}

type BadgeTone = 'neutral' | 'rebel' | 'gain' | 'loss' | 'warn'

const badgeTones: Record<BadgeTone, string> = {
  neutral: 'bg-ink-800 text-ink-300 border-ink-700',
  rebel: 'bg-rebel-500/15 text-rebel-400 border-rebel-500/30',
  gain: 'bg-gain-500/15 text-gain-400 border-gain-500/30',
  loss: 'bg-loss-500/15 text-loss-400 border-loss-500/30',
  warn: 'bg-amber-500/15 text-amber-400 border-amber-500/30',
}

export function Badge({
  tone = 'neutral',
  className,
  children,
}: {
  tone?: BadgeTone
  className?: string
  children: ReactNode
}) {
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-md border px-2 py-0.5 text-xs font-medium',
        badgeTones[tone],
        className,
      )}
    >
      {children}
    </span>
  )
}

/** Label/value pair used across the trade-plan grids. */
export function Stat({
  label,
  value,
  tone,
  hint,
}: {
  label: string
  value: ReactNode
  tone?: string
  hint?: string
}) {
  return (
    <div>
      <div className="text-[11px] font-medium uppercase tracking-wide text-ink-500">{label}</div>
      <div className={cn('tabular mt-0.5 text-sm font-semibold text-ink-100', tone)}>{value}</div>
      {hint && <div className="tabular mt-0.5 text-[11px] text-ink-500">{hint}</div>}
    </div>
  )
}

export function Spinner({ className }: { className?: string }) {
  return (
    <svg
      className={cn('h-4 w-4 animate-spin', className)}
      viewBox="0 0 24 24"
      fill="none"
      aria-hidden="true"
    >
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="3" />
      <path
        className="opacity-90"
        fill="currentColor"
        d="M4 12a8 8 0 0 1 8-8v3a5 5 0 0 0-5 5H4z"
      />
    </svg>
  )
}

export function EmptyState({
  title,
  description,
  action,
}: {
  title: string
  description: string
  action?: ReactNode
}) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 px-6 py-16 text-center">
      <h3 className="text-base font-semibold text-ink-100">{title}</h3>
      <p className="max-w-md text-sm text-ink-500">{description}</p>
      {action}
    </div>
  )
}

export function ErrorNotice({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return (
    <div className="rounded-lg border border-loss-500/30 bg-loss-500/10 px-4 py-3 text-sm text-loss-400">
      <div className="flex items-center justify-between gap-4">
        <span>{message}</span>
        {onRetry && (
          <Button size="sm" variant="ghost" onClick={onRetry}>
            Retry
          </Button>
        )}
      </div>
    </div>
  )
}

export function Skeleton({ className }: { className?: string }) {
  return <div className={cn('animate-pulse rounded-md bg-ink-850', className)} />
}
