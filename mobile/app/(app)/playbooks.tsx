import React, { useState } from 'react'
import {
  Alert,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { SafeAreaView } from 'react-native-safe-area-context'
import { playbooksApi } from '@/lib/api'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { colors, radius, spacing } from '@/constants/theme'
import type { AssetClass, Playbook } from '@/lib/types'

type Screen = 'list' | 'create' | 'view'

export default function PlaybooksScreen() {
  const [screen, setScreen] = useState<Screen>('list')
  const [selected, setSelected] = useState<Playbook | null>(null)
  const qc = useQueryClient()

  const { data: playbooks = [], isLoading } = useQuery({
    queryKey: ['playbooks'],
    queryFn: playbooksApi.list,
  })

  const activateMutation = useMutation({
    mutationFn: (id: string) => playbooksApi.activate(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['playbooks'] }),
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => playbooksApi.delete(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['playbooks'] }),
  })

  if (screen === 'create') {
    return (
      <CreatePlaybookForm
        onSave={() => {
          qc.invalidateQueries({ queryKey: ['playbooks'] })
          setScreen('list')
        }}
        onCancel={() => setScreen('list')}
      />
    )
  }

  if (screen === 'view' && selected) {
    return (
      <PlaybookDetail
        playbook={selected}
        onBack={() => setScreen('list')}
        onActivate={() => {
          activateMutation.mutate(selected.id)
          setScreen('list')
        }}
        onDelete={() => {
          Alert.alert('Delete Playbook', `Delete "${selected.name}"? This cannot be undone.`, [
            { text: 'Cancel', style: 'cancel' },
            {
              text: 'Delete',
              style: 'destructive',
              onPress: () => {
                deleteMutation.mutate(selected.id)
                setScreen('list')
              },
            },
          ])
        }}
      />
    )
  }

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.header}>
        <Text style={styles.title}>Playbooks</Text>
        <Button label="+ New" size="sm" onPress={() => setScreen('create')} />
      </View>

      {isLoading ? (
        <View style={styles.center}>
          <Text style={styles.muted}>Loading…</Text>
        </View>
      ) : playbooks.length === 0 ? (
        <View style={styles.center}>
          <Text style={styles.emptyIcon}>📋</Text>
          <Text style={styles.emptyTitle}>No Playbooks Yet</Text>
          <Text style={styles.emptyBody}>
            A Playbook defines your trading strategy. Rebel uses it to coach you in real time.
          </Text>
          <Button label="Create First Playbook" onPress={() => setScreen('create')} style={styles.emptyBtn} />
        </View>
      ) : (
        <ScrollView contentContainerStyle={styles.list}>
          {playbooks.map((p: Playbook) => (
            <Pressable
              key={p.id}
              onPress={() => { setSelected(p); setScreen('view') }}
            >
              <Card style={[styles.card, p.isActive && styles.cardActive]}>
                <View style={styles.cardHeader}>
                  <View style={styles.cardLeft}>
                    <Text style={styles.cardName}>{p.name}</Text>
                    <Text style={styles.cardMeta}>
                      {p.assetClass} · {p.instruments.join(', ')} · v{p.version}
                    </Text>
                  </View>
                  {p.isActive && (
                    <View style={styles.activeBadge}>
                      <Text style={styles.activeBadgeText}>ACTIVE</Text>
                    </View>
                  )}
                </View>
                {p.description ? (
                  <Text style={styles.cardDesc} numberOfLines={2}>{p.description}</Text>
                ) : null}
              </Card>
            </Pressable>
          ))}
        </ScrollView>
      )}
    </SafeAreaView>
  )
}

function PlaybookDetail({
  playbook,
  onBack,
  onActivate,
  onDelete,
}: {
  playbook: Playbook
  onBack: () => void
  onActivate: () => void
  onDelete: () => void
}) {
  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.header}>
        <Pressable onPress={onBack}>
          <Text style={styles.backBtn}>← Back</Text>
        </Pressable>
        {!playbook.isActive && (
          <Button label="Set Active" size="sm" onPress={onActivate} />
        )}
      </View>

      <ScrollView contentContainerStyle={{ padding: spacing.md, gap: spacing.md }}>
        <Text style={styles.title}>{playbook.name}</Text>
        {playbook.description ? (
          <Text style={{ color: colors.textSecondary }}>{playbook.description}</Text>
        ) : null}

        <RuleSection title="Asset Class" items={[playbook.assetClass]} />
        <RuleSection title="Instruments" items={playbook.instruments} />
        {playbook.rules.entryConditions.length > 0 && (
          <RuleSection title="Entry Conditions" items={playbook.rules.entryConditions} />
        )}
        {playbook.rules.exitConditions.length > 0 && (
          <RuleSection title="Exit Conditions" items={playbook.rules.exitConditions} />
        )}
        {playbook.rules.stopLossRules.length > 0 && (
          <RuleSection title="Stop Loss Rules" items={playbook.rules.stopLossRules} />
        )}
        {playbook.rules.stopManagementRules.length > 0 && (
          <RuleSection title="Stop Management" items={playbook.rules.stopManagementRules} />
        )}
        {playbook.rules.profitTargets.length > 0 && (
          <RuleSection title="Profit Targets" items={playbook.rules.profitTargets} />
        )}
        {playbook.rules.riskPerTrade !== undefined && (
          <RuleSection title="Risk Per Trade" items={[`${playbook.rules.riskPerTrade}%`]} />
        )}
        {playbook.rules.maxDailyRisk !== undefined && (
          <RuleSection title="Max Daily Risk" items={[`${playbook.rules.maxDailyRisk}%`]} />
        )}

        <Button label="Delete Playbook" variant="danger" onPress={onDelete} style={{ marginTop: spacing.md }} />
      </ScrollView>
    </SafeAreaView>
  )
}

function RuleSection({ title, items }: { title: string; items: string[] }) {
  return (
    <View style={ruleStyles.section}>
      <Text style={ruleStyles.title}>{title.toUpperCase()}</Text>
      {items.map((item, i) => (
        <Text key={i} style={ruleStyles.item}>· {item}</Text>
      ))}
    </View>
  )
}

function CreatePlaybookForm({ onSave, onCancel }: { onSave: () => void; onCancel: () => void }) {
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [assetClass, setAssetClass] = useState<AssetClass>('Futures')
  const [instruments, setInstruments] = useState('MNQ')
  const [entryConditions, setEntryConditions] = useState('')
  const [riskPerTrade, setRiskPerTrade] = useState('1')
  const [saving, setSaving] = useState(false)

  const handleSave = async () => {
    if (!name.trim()) {
      Alert.alert('Name required', 'Give your Playbook a name.')
      return
    }
    setSaving(true)
    try {
      await playbooksApi.create({
        name: name.trim(),
        description: description.trim(),
        assetClass,
        instruments: instruments.split(',').map((s) => s.trim()).filter(Boolean),
        isActive: false,
        rules: {
          entryConditions: entryConditions.split('\n').filter(Boolean),
          exitConditions: [],
          stopLossRules: [],
          stopManagementRules: [],
          profitTargets: [],
          trailingRules: [],
          positionSizingRules: [],
          additionalConditions: [],
          riskPerTrade: parseFloat(riskPerTrade) || 1,
        },
      })
      onSave()
    } catch (err: any) {
      Alert.alert('Error', err?.response?.data?.message ?? 'Failed to save playbook.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.header}>
        <Pressable onPress={onCancel}>
          <Text style={styles.backBtn}>Cancel</Text>
        </Pressable>
        <Text style={styles.title}>New Playbook</Text>
        <Button label="Save" size="sm" loading={saving} onPress={handleSave} />
      </View>

      <ScrollView contentContainerStyle={createStyles.form} keyboardShouldPersistTaps="handled">
        <Field label="Name">
          <TextInput
            style={createStyles.input}
            value={name}
            onChangeText={setName}
            placeholder="My MNQ Breakout Strategy"
            placeholderTextColor={colors.textMuted}
          />
        </Field>

        <Field label="Description (optional)">
          <TextInput
            style={[createStyles.input, createStyles.multiline]}
            value={description}
            onChangeText={setDescription}
            placeholder="Describe your strategy…"
            placeholderTextColor={colors.textMuted}
            multiline
            numberOfLines={3}
          />
        </Field>

        <Field label="Asset Class">
          <View style={createStyles.toggleRow}>
            {(['Futures', 'Stocks'] as AssetClass[]).map((ac) => (
              <Pressable
                key={ac}
                style={[createStyles.toggleBtn, assetClass === ac && createStyles.toggleBtnActive]}
                onPress={() => setAssetClass(ac)}
              >
                <Text style={[createStyles.toggleLabel, assetClass === ac && createStyles.toggleLabelActive]}>
                  {ac}
                </Text>
              </Pressable>
            ))}
          </View>
        </Field>

        <Field label="Instruments (comma-separated)">
          <TextInput
            style={createStyles.input}
            value={instruments}
            onChangeText={setInstruments}
            placeholder="MNQ, NQ"
            placeholderTextColor={colors.textMuted}
            autoCapitalize="characters"
          />
        </Field>

        <Field label="Entry Conditions (one per line)">
          <TextInput
            style={[createStyles.input, createStyles.multiline]}
            value={entryConditions}
            onChangeText={setEntryConditions}
            multiline
            numberOfLines={4}
            placeholder={"Price above VWAP\nMomentum confirmation\n..."}
            placeholderTextColor={colors.textMuted}
          />
        </Field>

        <Field label="Risk Per Trade (%)">
          <TextInput
            style={createStyles.input}
            value={riskPerTrade}
            onChangeText={setRiskPerTrade}
            keyboardType="decimal-pad"
            placeholder="1"
            placeholderTextColor={colors.textMuted}
          />
        </Field>

        <Text style={createStyles.hint}>
          You can add more rules after creating the playbook.
        </Text>
      </ScrollView>
    </SafeAreaView>
  )
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <View style={{ gap: 6 }}>
      <Text style={{ fontSize: 13, fontWeight: '600', color: colors.textSecondary }}>{label}</Text>
      {children}
    </View>
  )
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.bg },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
  title: { fontSize: 20, fontWeight: '700', color: colors.textPrimary },
  backBtn: { fontSize: 16, color: colors.primary },
  center: { flex: 1, justifyContent: 'center', alignItems: 'center', gap: spacing.md, padding: spacing.xl },
  muted: { color: colors.textMuted, fontSize: 15 },
  emptyIcon: { fontSize: 48 },
  emptyTitle: { fontSize: 20, fontWeight: '700', color: colors.textPrimary },
  emptyBody: { fontSize: 14, color: colors.textSecondary, textAlign: 'center', lineHeight: 20 },
  emptyBtn: { marginTop: spacing.sm },
  list: { padding: spacing.md, gap: spacing.sm },
  card: { gap: spacing.xs },
  cardActive: { borderColor: colors.primary },
  cardHeader: { flexDirection: 'row', alignItems: 'center' },
  cardLeft: { flex: 1 },
  cardName: { fontSize: 16, fontWeight: '700', color: colors.textPrimary },
  cardMeta: { fontSize: 12, color: colors.textSecondary, marginTop: 2 },
  cardDesc: { fontSize: 13, color: colors.textMuted },
  activeBadge: {
    backgroundColor: colors.primary + '20',
    borderRadius: 4,
    paddingHorizontal: 6,
    paddingVertical: 2,
  },
  activeBadgeText: { fontSize: 10, fontWeight: '700', color: colors.primary, letterSpacing: 1 },
})

const ruleStyles = StyleSheet.create({
  section: {
    backgroundColor: colors.surface,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    padding: spacing.md,
    gap: 6,
  },
  title: { fontSize: 11, fontWeight: '700', letterSpacing: 1.5, color: colors.textMuted },
  item: { fontSize: 14, color: colors.textPrimary, lineHeight: 20 },
})

const createStyles = StyleSheet.create({
  form: { padding: spacing.md, gap: spacing.md, paddingBottom: spacing.xxl },
  input: {
    backgroundColor: colors.surface,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm + 2,
    fontSize: 15,
    color: colors.textPrimary,
  },
  multiline: { minHeight: 80, textAlignVertical: 'top' },
  toggleRow: { flexDirection: 'row', gap: spacing.sm },
  toggleBtn: {
    flex: 1,
    paddingVertical: spacing.sm,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    alignItems: 'center',
    backgroundColor: colors.surfaceAlt,
  },
  toggleBtnActive: { borderColor: colors.primary, backgroundColor: colors.primary + '15' },
  toggleLabel: { fontSize: 15, fontWeight: '600', color: colors.textSecondary },
  toggleLabelActive: { color: colors.primary },
  hint: { fontSize: 12, color: colors.textMuted, textAlign: 'center' },
})
