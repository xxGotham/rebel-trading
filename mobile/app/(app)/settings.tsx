import React from 'react'
import {
  Alert,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native'
import { useQuery } from '@tanstack/react-query'
import { SafeAreaView } from 'react-native-safe-area-context'
import { router } from 'expo-router'
import { useAuthStore } from '@/store/authStore'
import { providersApi } from '@/lib/api'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { colors, spacing } from '@/constants/theme'
import type { ProviderConnection, ProviderStatus } from '@/lib/types'

export default function SettingsScreen() {
  const { user, logout } = useAuthStore()

  const { data: providers = [] } = useQuery({
    queryKey: ['providers'],
    queryFn: providersApi.list,
  })

  const handleLogout = () => {
    Alert.alert('Sign Out', 'Are you sure you want to sign out?', [
      { text: 'Cancel', style: 'cancel' },
      {
        text: 'Sign Out',
        style: 'destructive',
        onPress: async () => {
          await logout()
          router.replace('/(auth)/login')
        },
      },
    ])
  }

  const byType = (type: ProviderConnection['providerType']) =>
    providers.filter((p: ProviderConnection) => p.providerType === type)

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.header}>
        <Text style={styles.title}>Settings</Text>
      </View>

      <ScrollView contentContainerStyle={styles.content}>
        {/* Account */}
        <Section title="Account">
          <Card>
            <Text style={styles.label}>Email</Text>
            <Text style={styles.value}>{user?.email}</Text>
            <Text style={[styles.label, { marginTop: spacing.sm }]}>Username</Text>
            <Text style={styles.value}>{user?.username}</Text>
            <Text style={[styles.label, { marginTop: spacing.sm }]}>Plan</Text>
            <Text style={styles.value}>{user?.subscriptionPlan ?? '—'}</Text>
          </Card>
        </Section>

        {/* Providers */}
        <Section title="Broker">
          <ProviderList providers={byType('Broker')} type="Broker" />
        </Section>

        <Section title="Market Data">
          <ProviderList providers={byType('MarketData')} type="MarketData" />
        </Section>

        <Section title="AI Provider">
          <ProviderList providers={byType('AI')} type="AI" />
        </Section>

        <Section title="Voice Provider">
          <ProviderList providers={byType('Voice')} type="Voice" />
        </Section>

        {/* Danger zone */}
        <Section title="">
          <Button label="Sign Out" variant="danger" onPress={handleLogout} />
        </Section>

        <Text style={styles.version}>Rebel Trading · Mobile v1.0.0</Text>
      </ScrollView>
    </SafeAreaView>
  )
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <View style={sectionStyles.container}>
      {title ? <Text style={sectionStyles.title}>{title.toUpperCase()}</Text> : null}
      {children}
    </View>
  )
}

function ProviderList({
  providers,
  type,
}: {
  providers: ProviderConnection[]
  type: ProviderConnection['providerType']
}) {
  if (providers.length === 0) {
    return (
      <Card>
        <View style={provStyles.empty}>
          <Text style={provStyles.emptyText}>No {type} provider connected</Text>
          <Pressable>
            <Text style={provStyles.connectLink}>+ Connect</Text>
          </Pressable>
        </View>
      </Card>
    )
  }

  return (
    <View style={{ gap: spacing.xs }}>
      {providers.map((p) => (
        <Card key={p.providerId}>
          <View style={provStyles.row}>
            <View>
              <Text style={provStyles.name}>{p.providerName}</Text>
              <Text style={provStyles.byok}>
                {p.keySource === 'USER_MANAGED_BYOK' ? 'Your API key' : 'Rebel managed'}
              </Text>
            </View>
            <StatusDot status={p.status} />
          </View>
        </Card>
      ))}
    </View>
  )
}

function StatusDot({ status }: { status: ProviderStatus }) {
  const color = {
    Connected: colors.success,
    Connecting: colors.warning,
    Disconnected: colors.textMuted,
    Error: colors.danger,
  }[status]

  return (
    <View style={dotStyles.container}>
      <View style={[dotStyles.dot, { backgroundColor: color }]} />
      <Text style={[dotStyles.label, { color }]}>{status}</Text>
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
  content: { padding: spacing.md, gap: spacing.lg, paddingBottom: spacing.xxl },
  label: { fontSize: 12, fontWeight: '600', color: colors.textMuted },
  value: { fontSize: 15, color: colors.textPrimary, marginTop: 2 },
  version: { textAlign: 'center', fontSize: 12, color: colors.textMuted },
})

const sectionStyles = StyleSheet.create({
  container: { gap: spacing.sm },
  title: { fontSize: 11, fontWeight: '700', letterSpacing: 1.5, color: colors.textMuted },
})

const provStyles = StyleSheet.create({
  empty: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  emptyText: { fontSize: 14, color: colors.textMuted },
  connectLink: { fontSize: 14, color: colors.primary, fontWeight: '600' },
  row: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  name: { fontSize: 15, fontWeight: '600', color: colors.textPrimary },
  byok: { fontSize: 12, color: colors.textSecondary, marginTop: 2 },
})

const dotStyles = StyleSheet.create({
  container: { flexDirection: 'row', alignItems: 'center', gap: 5 },
  dot: { width: 8, height: 8, borderRadius: 4 },
  label: { fontSize: 12, fontWeight: '600' },
})
