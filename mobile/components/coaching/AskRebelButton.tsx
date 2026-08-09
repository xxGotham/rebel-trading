import React from 'react'
import { Pressable, StyleSheet, Text, View } from 'react-native'
import { colors, radius, spacing } from '@/constants/theme'

interface Props {
  isListening: boolean
  onPressIn: () => void
  onPressOut: () => void
}

export function AskRebelButton({ isListening, onPressIn, onPressOut }: Props) {
  return (
    <View style={styles.container}>
      <Pressable
        onPressIn={onPressIn}
        onPressOut={onPressOut}
        style={({ pressed }) => [
          styles.button,
          isListening && styles.buttonListening,
          pressed && styles.buttonPressed,
        ]}
      >
        <Text style={styles.icon}>{isListening ? '🔴' : '🎤'}</Text>
        <Text style={styles.label}>{isListening ? 'Listening…' : 'Ask Rebel'}</Text>
      </Pressable>
      <Text style={styles.hint}>Hold to speak</Text>
    </View>
  )
}

const styles = StyleSheet.create({
  container: { alignItems: 'center', gap: spacing.xs },
  button: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    backgroundColor: colors.primary,
    paddingHorizontal: spacing.xl,
    paddingVertical: spacing.md,
    borderRadius: radius.full,
    minWidth: 180,
    justifyContent: 'center',
  },
  buttonListening: {
    backgroundColor: colors.danger,
  },
  buttonPressed: {
    transform: [{ scale: 0.97 }],
    opacity: 0.9,
  },
  icon: { fontSize: 20 },
  label: {
    fontSize: 17,
    fontWeight: '700',
    color: '#fff',
  },
  hint: {
    fontSize: 12,
    color: colors.textMuted,
  },
})
