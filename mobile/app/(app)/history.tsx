import React from 'react'
import { ScrollView, StyleSheet, Text, View } from 'react-native'
import { useQuery } from '@tanstack/react-query'
import { SafeAreaView } from 'react-native-safe-area-context'
import { sessionsApi } from '@/lib/api'
import { Card } from '@/components/ui/Card'
import { colors, spacing } from '@/constants/theme'
import type { SessionSummary } from '@/lib/types'

export default function HistoryScreen() {
  const { data: sessions = [], isLoading } = useQuery({
    queryKey: ['session-history'],
    queryFn: sessionsApi.history,
  })

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.header}>
        <Text style={styles.title}>Session History</Text>
      </View>

      {isLoading ? (
        <View style={styles.center}>
          <Text style={styles.muted}>Loading…</Text>
        </View>
      ) : sessions.length === 0 ? (
        <View style={styles.center}>
          <Text style={styles.emptyIcon}>📅</Text>
          <Text style={styles.emptyTitle}>No Sessions Yet</Text>
          <Text style={styles.emptyBody}>
            Your coaching sessions will appear here with automatic journals.
          </Text>
        </View>
      ) : (
        <ScrollView contentContainerStyle={styles.list}>
          {sessions.map((s: SessionSummary) => (
            <SessionCard key={s.id} session={s} />
          ))}
        </ScrollView>
      )}
    </SafeAreaView>
  )
}

function SessionCard({ session }: { session: SessionSummary }) {
  const pnlPositive = session.realizedPnl >= 0
  const pnlColor = pnlPositive ? colors.success : colors.danger
  const pnlText = `${pnlPositive ? '+' : ''}$${Math.abs(session.realizedPnl).toFixed(2)}`

  const date = new Date(session.startedAt)
  const dateStr = date.toLocaleDateString('en-US', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
  })
  const timeStr = date.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' })

  return (
    <Card>
      <View style={cardStyles.top}>
        <View>
          <Text style={cardStyles.instrument}>{session.instrument}</Text>
          <Text style={cardStyles.playbook}>{session.playbookName}</Text>
        </View>
        <Text style={[cardStyles.pnl, { color: pnlColor }]}>{pnlText}</Text>
      </View>

      <View style={cardStyles.row}>
        <Stat label="Date" value={`${dateStr} ${timeStr}`} />
        <Stat label="Duration" value={`${session.durationMinutes}m`} />
        <Stat label="Trades" value={String(session.tradeCount)} />
        {session.ruleViolations > 0 && (
          <Stat label="Violations" value={String(session.ruleViolations)} color={colors.warning} />
        )}
      </View>

      {session.apiCost > 0 && (
        <Text style={cardStyles.cost}>API cost: ${session.apiCost.toFixed(4)}</Text>
      )}
    </Card>
  )
}

function Stat({ label, value, color }: { label: string; value: string; color?: string }) {
  return (
    <View>
      <Text style={statStyles.label}>{label}</Text>
      <Text style={[statStyles.value, color ? { color } : undefined]}>{value}</Text>
    </View>
  )
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.bg },
  header: {
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
  title: { fontSize: 20, fontWeight: '700', color: colors.textPrimary },
  center: { flex: 1, justifyContent: 'center', alignItems: 'center', gap: spacing.md, padding: spacing.xl },
  muted: { color: colors.textMuted, fontSize: 15 },
  emptyIcon: { fontSize: 48 },
  emptyTitle: { fontSize: 20, fontWeight: '700', color: colors.textPrimary },
  emptyBody: { fontSize: 14, color: colors.textSecondary, textAlign: 'center', lineHeight: 20 },
  list: { padding: spacing.md, gap: spacing.sm },
})

const cardStyles = StyleSheet.create({
  top: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: spacing.sm },
  instrument: { fontSize: 18, fontWeight: '700', color: colors.textPrimary },
  playbook: { fontSize: 12, color: colors.textSecondary },
  pnl: { fontSize: 20, fontWeight: '700' },
  row: { flexDirection: 'row', gap: spacing.md, flexWrap: 'wrap' },
  cost: { fontSize: 11, color: colors.textMuted, marginTop: spacing.xs },
})

const statStyles = StyleSheet.create({
  label: { fontSize: 10, fontWeight: '700', letterSpacing: 1, color: colors.textMuted },
  value: { fontSize: 14, fontWeight: '600', color: colors.textPrimary },
})
