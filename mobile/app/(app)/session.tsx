import React, { useCallback } from 'react'
import { ScrollView, StyleSheet, Text, View } from 'react-native'
import { router } from 'expo-router'
import { SafeAreaView } from 'react-native-safe-area-context'
import { useCoachingSession } from '@/hooks/useCoachingSession'
import { useVoice } from '@/hooks/useVoice'
import { useSessionStore } from '@/store/sessionStore'
import { SessionHeader } from '@/components/coaching/SessionHeader'
import { PositionCard } from '@/components/coaching/PositionCard'
import { RebelMessage } from '@/components/coaching/RebelMessage'
import { AskRebelButton } from '@/components/coaching/AskRebelButton'
import { colors, spacing } from '@/constants/theme'

export default function SessionScreen() {
  const { session, endSession, askRebel } = useCoachingSession()
  const { messages, isMuted, isListening, toggleMute, setListening } = useSessionStore()
  const { startListening, stopListening } = useVoice()

  const handleEnd = async () => {
    await endSession()
    router.replace('/(app)')
  }

  const handlePressIn = useCallback(async () => {
    setListening(true)
    await startListening()
  }, [startListening, setListening])

  const handlePressOut = useCallback(async () => {
    const uri = await stopListening()
    setListening(false)
    if (uri) {
      // In production: send audio to backend STT, get transcription, then askRebel
      // For now: demo with a placeholder
      await askRebel('[voice question from audio]')
    }
  }, [stopListening, setListening, askRebel])

  if (!session) {
    return (
      <SafeAreaView style={styles.safe}>
        <View style={styles.noSession}>
          <Text style={styles.noSessionText}>No active session.</Text>
        </View>
      </SafeAreaView>
    )
  }

  const latestRebelMessage = messages.find((m) => !m.isUserQuestion) ?? null

  return (
    <SafeAreaView style={styles.safe} edges={['top', 'bottom']}>
      <SessionHeader
        instrument={session.instrument}
        quote={session.currentQuote}
        isMuted={isMuted}
        onMute={toggleMute}
        onEnd={handleEnd}
      />

      <ScrollView
        style={styles.scroll}
        contentContainerStyle={styles.content}
        showsVerticalScrollIndicator={false}
      >
        {/* P&L Summary */}
        <View style={styles.pnlRow}>
          <PnlChip label="Session P&L" value={session.sessionPnl} />
          <PnlChip label="Trades" value={session.tradeCount} isCount />
        </View>

        {/* Position */}
        {session.currentPosition ? (
          <PositionCard position={session.currentPosition} />
        ) : (
          <View style={styles.noPosition}>
            <Text style={styles.noPositionText}>No open position</Text>
            <Text style={styles.noPositionSub}>
              Rebel is watching for your entry conditions.
            </Text>
          </View>
        )}

        {/* Rebel Message */}
        <RebelMessage message={latestRebelMessage} />

        {/* Message History */}
        {messages.length > 1 && (
          <View style={styles.historySection}>
            <Text style={styles.historyTitle}>TRANSCRIPT</Text>
            {messages.slice(1, 8).map((msg) => (
              <View
                key={msg.id}
                style={[styles.historyItem, msg.isUserQuestion && styles.historyItemUser]}
              >
                <Text style={styles.historyAuthor}>
                  {msg.isUserQuestion ? 'YOU' : 'REBEL'}
                </Text>
                <Text style={styles.historyText}>{msg.text}</Text>
              </View>
            ))}
          </View>
        )}
      </ScrollView>

      {/* Ask Rebel */}
      <View style={styles.footer}>
        <AskRebelButton
          isListening={isListening}
          onPressIn={handlePressIn}
          onPressOut={handlePressOut}
        />
      </View>
    </SafeAreaView>
  )
}

function PnlChip({ label, value, isCount }: { label: string; value: number; isCount?: boolean }) {
  const isPositive = value >= 0
  const formatted = isCount
    ? String(value)
    : `${isPositive ? '+' : ''}$${Math.abs(value).toFixed(2)}`
  const color = isCount
    ? colors.textPrimary
    : isPositive
      ? colors.success
      : colors.danger

  return (
    <View style={chipStyles.chip}>
      <Text style={chipStyles.label}>{label}</Text>
      <Text style={[chipStyles.value, { color }]}>{formatted}</Text>
    </View>
  )
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.bg },
  scroll: { flex: 1 },
  content: { padding: spacing.md, gap: spacing.md, paddingBottom: spacing.xl },
  noSession: { flex: 1, justifyContent: 'center', alignItems: 'center' },
  noSessionText: { color: colors.textMuted, fontSize: 16 },
  pnlRow: { flexDirection: 'row', gap: spacing.sm },
  noPosition: {
    backgroundColor: colors.surface,
    borderRadius: 12,
    borderWidth: 1,
    borderColor: colors.border,
    padding: spacing.md,
    gap: 4,
  },
  noPositionText: { fontSize: 15, fontWeight: '600', color: colors.textSecondary },
  noPositionSub: { fontSize: 13, color: colors.textMuted },
  historySection: { gap: spacing.sm },
  historyTitle: {
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 1.5,
    color: colors.textMuted,
  },
  historyItem: {
    backgroundColor: colors.surface,
    borderRadius: 10,
    borderWidth: 1,
    borderColor: colors.border,
    padding: spacing.sm,
    gap: 2,
  },
  historyItemUser: {
    borderColor: colors.primary + '40',
    backgroundColor: colors.primary + '0A',
  },
  historyAuthor: {
    fontSize: 10,
    fontWeight: '700',
    letterSpacing: 1,
    color: colors.textMuted,
  },
  historyText: {
    fontSize: 14,
    color: colors.textPrimary,
    lineHeight: 20,
  },
  footer: {
    paddingVertical: spacing.lg,
    paddingHorizontal: spacing.md,
    alignItems: 'center',
    borderTopWidth: 1,
    borderTopColor: colors.border,
    backgroundColor: colors.bg,
  },
})

const chipStyles = StyleSheet.create({
  chip: {
    flex: 1,
    backgroundColor: colors.surface,
    borderRadius: 10,
    borderWidth: 1,
    borderColor: colors.border,
    padding: spacing.sm,
    gap: 2,
  },
  label: { fontSize: 11, color: colors.textMuted, fontWeight: '600', letterSpacing: 1 },
  value: { fontSize: 18, fontWeight: '700' },
})
