import React from 'react'
import { Alert, Pressable, StyleSheet, Text, View } from 'react-native'
import { colors, spacing } from '@/constants/theme'
import type { Quote } from '@/lib/types'

interface Props {
  instrument: string
  quote: Quote | undefined
  isMuted: boolean
  onMute: () => void
  onEnd: () => void
}

export function SessionHeader({ instrument, quote, isMuted, onMute, onEnd }: Props) {
  const handleEnd = () => {
    Alert.alert(
      'End Session',
      'End this coaching session? Rebel will generate a journal summary.',
      [
        { text: 'Cancel', style: 'cancel' },
        { text: 'End Session', style: 'destructive', onPress: onEnd },
      ],
    )
  }

  const priceColor = quote
    ? quote.change >= 0
      ? colors.success
      : colors.danger
    : colors.textSecondary

  return (
    <View style={styles.container}>
      <View style={styles.left}>
        <View style={styles.liveRow}>
          <View style={styles.liveDot} />
          <Text style={styles.liveText}>LIVE</Text>
        </View>
        <Text style={styles.instrument}>{instrument}</Text>
      </View>

      <View style={styles.price}>
        {quote && (
          <>
            <Text style={styles.priceValue}>{quote.price.toFixed(2)}</Text>
            <Text style={[styles.priceChange, { color: priceColor }]}>
              {quote.change >= 0 ? '+' : ''}{quote.change.toFixed(2)}
            </Text>
          </>
        )}
      </View>

      <View style={styles.controls}>
        <Pressable onPress={onMute} style={styles.iconBtn}>
          <Text style={styles.iconBtnText}>{isMuted ? '🔇' : '🔊'}</Text>
        </Pressable>
        <Pressable onPress={handleEnd} style={[styles.iconBtn, styles.endBtn]}>
          <Text style={styles.endBtnText}>End</Text>
        </Pressable>
      </View>
    </View>
  )
}

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
    backgroundColor: colors.surface,
  },
  left: { flex: 1 },
  liveRow: { flexDirection: 'row', alignItems: 'center', gap: 5 },
  liveDot: {
    width: 7,
    height: 7,
    borderRadius: 999,
    backgroundColor: colors.live,
  },
  liveText: {
    fontSize: 10,
    fontWeight: '700',
    letterSpacing: 1.5,
    color: colors.live,
  },
  instrument: {
    fontSize: 20,
    fontWeight: '700',
    color: colors.textPrimary,
  },
  price: { flex: 1, alignItems: 'center' },
  priceValue: {
    fontSize: 18,
    fontWeight: '700',
    color: colors.textPrimary,
    fontVariant: ['tabular-nums'],
  },
  priceChange: {
    fontSize: 12,
    fontWeight: '600',
  },
  controls: { flex: 1, flexDirection: 'row', justifyContent: 'flex-end', gap: spacing.sm },
  iconBtn: {
    padding: spacing.xs,
  },
  iconBtnText: { fontSize: 22 },
  endBtn: {
    backgroundColor: colors.danger,
    paddingHorizontal: spacing.sm,
    paddingVertical: 4,
    borderRadius: 6,
  },
  endBtnText: {
    fontSize: 13,
    fontWeight: '700',
    color: '#fff',
  },
})
