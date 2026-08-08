import { clsx, type ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export function money(value: number | null | undefined, digits = 2): string {
  if (value === null || value === undefined) return '—'
  return value.toLocaleString('en-US', {
    style: 'currency',
    currency: 'USD',
    minimumFractionDigits: digits,
    maximumFractionDigits: digits,
  })
}

export function percent(value: number | null | undefined, digits = 2): string {
  if (value === null || value === undefined) return '—'
  return `${value >= 0 ? '+' : ''}${value.toFixed(digits)}%`
}

/** Unsigned percentage — for distances and rates, where a leading + reads as a gain. */
export function rate(value: number | null | undefined, digits = 1): string {
  if (value === null || value === undefined) return '—'
  return `${value.toFixed(digits)}%`
}

export function number(value: number | null | undefined, digits = 2): string {
  if (value === null || value === undefined) return '—'
  return value.toFixed(digits)
}

export function compact(value: number | null | undefined): string {
  if (value === null || value === undefined) return '—'
  return Intl.NumberFormat('en-US', { notation: 'compact', maximumFractionDigits: 1 }).format(value)
}

export function shortDate(value: string | null | undefined): string {
  if (!value) return '—'
  const date = new Date(value.length <= 10 ? `${value}T00:00:00` : value)
  return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
}

export function dateTime(value: string | null | undefined): string {
  if (!value) return '—'
  return new Date(value).toLocaleString('en-US', {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  })
}

/** Green for gains, red for losses, neutral for flat — never green for "good". */
export function toneFor(value: number | null | undefined): string {
  if (value === null || value === undefined || value === 0) return 'text-ink-300'
  return value > 0 ? 'text-gain-400' : 'text-loss-400'
}
