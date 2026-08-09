import React from 'react'
import { StyleSheet, View, type StyleProp, type ViewStyle } from 'react-native'
import { colors, radius, spacing } from '@/constants/theme'

interface Props {
  children: React.ReactNode
  style?: StyleProp<ViewStyle>
}

export function Card({ children, style }: Props) {
  return <View style={[styles.card, style]}>{children}</View>
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: colors.surface,
    borderRadius: radius.lg,
    padding: spacing.md,
    borderWidth: 1,
    borderColor: colors.border,
  },
})
