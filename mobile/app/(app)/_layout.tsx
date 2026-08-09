import { Tabs } from 'expo-router'
import { colors } from '@/constants/theme'

export default function AppLayout() {
  return (
    <Tabs
      screenOptions={{
        headerShown: false,
        tabBarStyle: {
          backgroundColor: colors.surface,
          borderTopColor: colors.border,
          borderTopWidth: 1,
        },
        tabBarActiveTintColor: colors.primary,
        tabBarInactiveTintColor: colors.textMuted,
        tabBarLabelStyle: { fontSize: 11, fontWeight: '600' },
      }}
    >
      <Tabs.Screen
        name="index"
        options={{
          title: 'Coach',
          tabBarIcon: ({ color }) => <TabIcon label="⚡" color={color} />,
        }}
      />
      <Tabs.Screen
        name="playbooks"
        options={{
          title: 'Playbooks',
          tabBarIcon: ({ color }) => <TabIcon label="📋" color={color} />,
        }}
      />
      <Tabs.Screen
        name="history"
        options={{
          title: 'History',
          tabBarIcon: ({ color }) => <TabIcon label="📅" color={color} />,
        }}
      />
      <Tabs.Screen
        name="settings"
        options={{
          title: 'Settings',
          tabBarIcon: ({ color }) => <TabIcon label="⚙️" color={color} />,
        }}
      />
      {/* Session screen — hidden from tab bar */}
      <Tabs.Screen
        name="session"
        options={{ href: null }}
      />
    </Tabs>
  )
}

function TabIcon({ label, color }: { label: string; color: string }) {
  const { Text } = require('react-native')
  return <Text style={{ fontSize: 20, opacity: color === colors.primary ? 1 : 0.5 }}>{label}</Text>
}
