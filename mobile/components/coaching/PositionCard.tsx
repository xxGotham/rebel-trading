import React from 'react'
import { StyleSheet, Text, View } from 'react-native'
import { Card } from '@/components/ui/Card'
import { colors, spacing } from '@/constants/theme'
import type { Position } from '@/lib/types'

interface Props {
  position: Position
}

function Row({ label, value, color }: { label: string; value: string; color?: string }) {
  return (
    <View style={styles.row}>
      <Text style={styles.label}>{label}</Text>
      <Text style={[styles.value, color ? { color } : undefined]}>{value}</Text>
    </View>
  )
}

function formatPnl(pnl: number, pts?: number): { text: string; color: string } {
  const positive = pnl >= 0
  const dollar = `${positive ? '+' : ''}$${Math.abs(pnl).toFixed(2)}`
  const ptsStr = pts !== undefined ? ` (${positive ? '+' : ''}${pts.toFixed(2)} pts)` : ''
  return {
    text: `${dollar}${ptsStr}`,
    color: positive ? colors.success : colors.danger,
  }
}

export function PositionCard({ position }: Props) {
  const pnl = formatPnl(position.unrealizedPnl, position.unrealizedPnlPts)
  const sideColor = position.side === 'Long' ? colors.success : colors.danger

  return (
    <Card>
      <View style={styles.header}>
        <Text style={styles.title}>POSITION</Text>
        <Text style={[styles.side, { color: sideColor }]}>
          {position.side.toUpperCase()} · {position.quantity}{' '}
          {position.quantity === 1 ? 'contract' : 'contracts'}
        </Text>
      </View>
      <View style={styles.rows}>
        <Row label="Entry" value={position.entryPrice.toFixed(2)} />
        {position.stopPrice !== undefined && (
          <Row label="Stop" value={position.stopPrice.toFixed(2)} color={colors.danger} />
        )}
        {position.targetPrice !== undefined && (
          <Row label="Target" value={position.targetPrice.toFixed(2)} color={colors.success} />
        )}
        <Row label="Unrealized P&L" value={pnl.text} color={pnl.color} />
      </View>
    </Card>
  )
}

const styles = StyleSheet.create({
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: spacing.sm,
  },
  title: {
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 1.5,
    color: colors.textMuted,
  },
  side: {
    fontSize: 13,
    fontWeight: '600',
  },
  rows: { gap: spacing.xs },
  row: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: 2,
  },
  label: {
    fontSize: 14,
    color: colors.textSecondary,
  },
  value: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.textPrimary,
  },
})
