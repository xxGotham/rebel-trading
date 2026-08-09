import React, { useState } from 'react'
import {
  Alert,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native'
import { router } from 'expo-router'
import { useQuery } from '@tanstack/react-query'
import { SafeAreaView } from 'react-native-safe-area-context'
import { useCoachingSession } from '@/hooks/useCoachingSession'
import { useAuthStore } from '@/store/authStore'
import { playbooksApi, sessionsApi } from '@/lib/api'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { colors, spacing } from '@/constants/theme'
import type { Playbook } from '@/lib/types'

export default function HomeScreen() {
  const user = useAuthStore((s) => s.user)
  const { startSession } = useCoachingSession()
  const [selectedPlaybookId, setSelectedPlaybookId] = useState<string | null>(null)
  const [instrument, setInstrument] = useState('MNQ')
  const [starting, setStarting] = useState(false)

  const { data: playbooks = [], isLoading } = useQuery({
    queryKey: ['playbooks'],
    queryFn: playbooksApi.list,
  })

  const activePlaybook = playbooks.find((p: Playbook) => p.isActive)
  const displayPlaybook = selectedPlaybookId
    ? playbooks.find((p: Playbook) => p.id === selectedPlaybookId)
    : activePlaybook

  const handleStart = async () => {
    if (!displayPlaybook) {
      Alert.alert('No Playbook', 'Select a Playbook before starting a session.')
      return
    }
    if (!instrument.trim()) {
      Alert.alert('No Instrument', 'Enter an instrument (e.g. MNQ, ES, AAPL).')
      return
    }

    setStarting(true)
    try {
      const check = await sessionsApi.preflight(displayPlaybook.id, instrument.trim())

      const blockers: string[] = []
      if (!check.hasActiveSubscription) blockers.push('No active subscription')
      if (!check.hasLiveCoachingEntitlement) blockers.push('No Live Coaching entitlement')
      if (!check.isMarketDataConnected) blockers.push('Market data not connected')
      if (!check.isAIConnected) blockers.push('AI provider not connected')

      if (blockers.length > 0) {
        Alert.alert(
          'Cannot start session',
          blockers.join('\n'),
          [{ text: 'OK' }],
        )
        return
      }

      await startSession(displayPlaybook.id, instrument.trim())
      router.push('/(app)/session')
    } catch (err: any) {
      Alert.alert('Error', err?.response?.data?.message ?? 'Failed to start session.')
    } finally {
      setStarting(false)
    }
  }

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <ScrollView contentContainerStyle={styles.container}>
        <View style={styles.header}>
          <Text style={styles.greeting}>
            Good {getTimeOfDay()}, {user?.username ?? 'Trader'}
          </Text>
          <Text style={styles.tagline}>Ready to follow your strategy?</Text>
        </View>

        {/* Playbook selection */}
        <Card style={styles.section}>
          <Text style={styles.sectionTitle}>PLAYBOOK</Text>
          {isLoading ? (
            <Text style={styles.muted}>Loading playbooks…</Text>
          ) : playbooks.length === 0 ? (
            <View style={styles.emptyPlaybook}>
              <Text style={styles.muted}>No playbooks yet.</Text>
              <Button
                label="Create Playbook"
                variant="ghost"
                size="sm"
                onPress={() => router.push('/(app)/playbooks')}
              />
            </View>
          ) : (
            <View style={styles.playbookList}>
              {playbooks.map((p: Playbook) => (
                <PlaybookRow
                  key={p.id}
                  playbook={p}
                  selected={
                    selectedPlaybookId
                      ? p.id === selectedPlaybookId
                      : p.isActive
                  }
                  onSelect={() => setSelectedPlaybookId(p.id)}
                />
              ))}
            </View>
          )}
        </Card>

        {/* Instrument */}
        <Card style={styles.section}>
          <Text style={styles.sectionTitle}>INSTRUMENT</Text>
          <TextInput
            style={styles.instrumentInput}
            value={instrument}
            onChangeText={(t) => setInstrument(t.toUpperCase())}
            placeholder="MNQ, ES, AAPL…"
            placeholderTextColor={colors.textMuted}
            autoCapitalize="characters"
          />
          <Text style={styles.instrumentHint}>
            Enter the symbol you'll be trading
          </Text>
        </Card>

        {/* Start */}
        <Button
          label="Start Coaching"
          size="lg"
          loading={starting}
          onPress={handleStart}
          style={styles.startBtn}
        />

        <Text style={styles.disclaimer}>
          Rebel coaches. You execute. Always use your own judgment.
        </Text>
      </ScrollView>
    </SafeAreaView>
  )
}

function PlaybookRow({
  playbook,
  selected,
  onSelect,
}: {
  playbook: Playbook
  selected: boolean
  onSelect: () => void
}) {
  const { Pressable } = require('react-native')
  return (
    <Pressable
      onPress={onSelect}
      style={[pbStyles.row, selected && pbStyles.rowSelected]}
    >
      <View style={pbStyles.left}>
        <Text style={pbStyles.name}>{playbook.name}</Text>
        <Text style={pbStyles.meta}>
          {playbook.assetClass} · v{playbook.version}
        </Text>
      </View>
      {selected && <Text style={pbStyles.check}>✓</Text>}
    </Pressable>
  )
}

function getTimeOfDay() {
  const h = new Date().getHours()
  if (h < 12) return 'morning'
  if (h < 17) return 'afternoon'
  return 'evening'
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.bg },
  container: { padding: spacing.md, gap: spacing.md, paddingBottom: spacing.xxl },
  header: { paddingVertical: spacing.sm },
  greeting: { fontSize: 24, fontWeight: '700', color: colors.textPrimary },
  tagline: { fontSize: 14, color: colors.textSecondary, marginTop: 2 },
  section: { gap: spacing.sm },
  sectionTitle: {
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 1.5,
    color: colors.textMuted,
    marginBottom: 2,
  },
  muted: { color: colors.textMuted, fontSize: 14 },
  emptyPlaybook: { gap: spacing.xs },
  playbookList: { gap: spacing.xs },
  instrumentInput: {
    backgroundColor: colors.surfaceAlt,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: colors.border,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    fontSize: 20,
    fontWeight: '700',
    color: colors.textPrimary,
    letterSpacing: 1,
  },
  instrumentHint: { fontSize: 12, color: colors.textMuted },
  startBtn: { marginTop: spacing.sm },
  disclaimer: {
    fontSize: 12,
    color: colors.textMuted,
    textAlign: 'center',
    lineHeight: 18,
  },
})

const pbStyles = StyleSheet.create({
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: spacing.sm,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: colors.border,
    backgroundColor: colors.surfaceAlt,
  },
  rowSelected: {
    borderColor: colors.primary,
    backgroundColor: `${colors.primary}15`,
  },
  left: { flex: 1 },
  name: { fontSize: 15, fontWeight: '600', color: colors.textPrimary },
  meta: { fontSize: 12, color: colors.textSecondary, marginTop: 1 },
  check: { fontSize: 18, color: colors.primary },
})
