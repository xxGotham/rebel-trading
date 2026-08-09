import React from 'react'
import { StyleSheet, Text, View } from 'react-native'
import { colors, radius, spacing } from '@/constants/theme'
import type { RebelMessage as RebelMessageType } from '@/lib/types'

interface Props {
  message: RebelMessageType | null
}

export function RebelMessage({ message }: Props) {
  return (
    <View style={styles.container}>
      <Text style={styles.eyebrow}>REBEL SAYS</Text>
      <View style={styles.bubble}>
        {message ? (
          <Text style={styles.text}>{message.text}</Text>
        ) : (
          <Text style={styles.placeholder}>Listening to the market…</Text>
        )}
      </View>
    </View>
  )
}

const styles = StyleSheet.create({
  container: { gap: spacing.xs },
  eyebrow: {
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 1.5,
    color: colors.textMuted,
  },
  bubble: {
    backgroundColor: colors.surfaceAlt,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border,
    padding: spacing.md,
    minHeight: 80,
    justifyContent: 'center',
  },
  text: {
    fontSize: 16,
    lineHeight: 24,
    color: colors.textPrimary,
  },
  placeholder: {
    fontSize: 15,
    color: colors.textMuted,
    fontStyle: 'italic',
  },
})
