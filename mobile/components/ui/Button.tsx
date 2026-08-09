import React from 'react'
import {
  ActivityIndicator,
  Pressable,
  StyleSheet,
  Text,
  type PressableProps,
  type StyleProp,
  type ViewStyle,
} from 'react-native'
import { colors, radius, spacing } from '@/constants/theme'

type Variant = 'primary' | 'secondary' | 'danger' | 'ghost'
type Size = 'sm' | 'md' | 'lg'

interface Props extends PressableProps {
  label: string
  variant?: Variant
  size?: Size
  loading?: boolean
  style?: StyleProp<ViewStyle>
}

export function Button({ label, variant = 'primary', size = 'md', loading, style, disabled, ...rest }: Props) {
  return (
    <Pressable
      style={({ pressed }) => [
        styles.base,
        styles[variant],
        styles[size],
        pressed && styles.pressed,
        (disabled || loading) && styles.disabled,
        style,
      ]}
      disabled={disabled || loading}
      {...rest}
    >
      {loading ? (
        <ActivityIndicator color={variant === 'primary' ? '#fff' : colors.primary} size="small" />
      ) : (
        <Text style={[styles.label, styles[`label_${variant}`], styles[`label_${size}`]]}>
          {label}
        </Text>
      )}
    </Pressable>
  )
}

const styles = StyleSheet.create({
  base: {
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: radius.md,
  },
  pressed: { opacity: 0.75 },
  disabled: { opacity: 0.4 },

  primary: { backgroundColor: colors.primary },
  secondary: { backgroundColor: colors.surfaceAlt, borderWidth: 1, borderColor: colors.border },
  danger: { backgroundColor: colors.danger },
  ghost: { backgroundColor: 'transparent' },

  sm: { paddingHorizontal: spacing.md, paddingVertical: spacing.xs },
  md: { paddingHorizontal: spacing.lg, paddingVertical: spacing.sm + 2 },
  lg: { paddingHorizontal: spacing.xl, paddingVertical: spacing.md },

  label: { fontWeight: '600' },
  label_primary: { color: '#fff' },
  label_secondary: { color: colors.textPrimary },
  label_danger: { color: '#fff' },
  label_ghost: { color: colors.primary },

  label_sm: { fontSize: 13 },
  label_md: { fontSize: 15 },
  label_lg: { fontSize: 17 },
})
