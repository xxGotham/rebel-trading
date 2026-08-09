import React, { useState } from 'react'
import {
  Alert,
  KeyboardAvoidingView,
  Platform,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native'
import { router } from 'expo-router'
import { Button } from '@/components/ui/Button'
import { colors, radius, spacing } from '@/constants/theme'
import { http } from '@/lib/api'

export default function ForgotPasswordScreen() {
  const [email, setEmail] = useState('')
  const [loading, setLoading] = useState(false)
  const [sent, setSent] = useState(false)

  const handleSubmit = async () => {
    if (!email.trim()) {
      Alert.alert('Enter your email', 'Please enter the email address on your account.')
      return
    }
    setLoading(true)
    try {
      await http.post('/auth/forgot-password', { email: email.trim().toLowerCase() })
      setSent(true)
    } catch {
      // Always show success to avoid user enumeration
      setSent(true)
    } finally {
      setLoading(false)
    }
  }

  if (sent) {
    return (
      <View style={styles.container}>
        <Text style={styles.icon}>📬</Text>
        <Text style={styles.title}>Check your email</Text>
        <Text style={styles.body}>
          If an account exists for {email}, you'll receive a password reset link shortly.
        </Text>
        <Button label="Back to Sign In" onPress={() => router.replace('/(auth)/login')} style={styles.btn} />
      </View>
    )
  }

  return (
    <KeyboardAvoidingView
      style={styles.flex}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      <View style={styles.container}>
        <Text style={styles.title}>Reset Password</Text>
        <Text style={styles.body}>
          Enter your email and we'll send you a link to reset your password.
        </Text>
        <View style={styles.field}>
          <Text style={styles.label}>Email</Text>
          <TextInput
            style={styles.input}
            value={email}
            onChangeText={setEmail}
            placeholder="you@example.com"
            placeholderTextColor={colors.textMuted}
            autoCapitalize="none"
            keyboardType="email-address"
            textContentType="emailAddress"
            onSubmitEditing={handleSubmit}
          />
        </View>
        <Button label="Send Reset Link" loading={loading} onPress={handleSubmit} style={styles.btn} />
        <Button label="Back" variant="ghost" onPress={() => router.back()} />
      </View>
    </KeyboardAvoidingView>
  )
}

const styles = StyleSheet.create({
  flex: { flex: 1, backgroundColor: colors.bg },
  container: {
    flex: 1,
    padding: spacing.xl,
    justifyContent: 'center',
    gap: spacing.md,
    backgroundColor: colors.bg,
  },
  icon: { fontSize: 48, textAlign: 'center' },
  title: { fontSize: 26, fontWeight: '800', color: colors.textPrimary },
  body: { fontSize: 15, color: colors.textSecondary, lineHeight: 22 },
  field: { gap: spacing.xs },
  label: { fontSize: 13, fontWeight: '600', color: colors.textSecondary },
  input: {
    backgroundColor: colors.surface,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm + 2,
    fontSize: 16,
    color: colors.textPrimary,
  },
  btn: { marginTop: spacing.sm },
})
