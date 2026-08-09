import React, { useState } from 'react'
import {
  Alert,
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native'
import { Link, router } from 'expo-router'
import { useAuthStore } from '@/store/authStore'
import { Button } from '@/components/ui/Button'
import { colors, radius, spacing } from '@/constants/theme'

export default function RegisterScreen() {
  const register = useAuthStore((s) => s.register)
  const [email, setEmail] = useState('')
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [loading, setLoading] = useState(false)

  const handleRegister = async () => {
    if (!email.trim() || !username.trim() || !password) {
      Alert.alert('Missing fields', 'All fields are required.')
      return
    }
    if (password !== confirm) {
      Alert.alert('Password mismatch', 'Passwords do not match.')
      return
    }
    if (password.length < 8) {
      Alert.alert('Weak password', 'Password must be at least 8 characters.')
      return
    }

    setLoading(true)
    try {
      await register(email.trim().toLowerCase(), username.trim(), password)
      router.replace('/(app)')
    } catch (err: any) {
      Alert.alert('Registration failed', err?.response?.data?.message ?? 'Please try again.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <KeyboardAvoidingView
      style={styles.flex}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      <ScrollView contentContainerStyle={styles.container} keyboardShouldPersistTaps="handled">
        <View style={styles.header}>
          <Text style={styles.title}>Create Account</Text>
          <Text style={styles.subtitle}>Join the Rebel Trading founder beta.</Text>
        </View>

        <View style={styles.form}>
          <View>
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
            />
          </View>

          <View>
            <Text style={styles.label}>Username</Text>
            <TextInput
              style={styles.input}
              value={username}
              onChangeText={setUsername}
              placeholder="tradingpro"
              placeholderTextColor={colors.textMuted}
              autoCapitalize="none"
              textContentType="username"
            />
          </View>

          <View>
            <Text style={styles.label}>Password</Text>
            <TextInput
              style={styles.input}
              value={password}
              onChangeText={setPassword}
              placeholder="At least 8 characters"
              placeholderTextColor={colors.textMuted}
              secureTextEntry
              textContentType="newPassword"
            />
          </View>

          <View>
            <Text style={styles.label}>Confirm Password</Text>
            <TextInput
              style={styles.input}
              value={confirm}
              onChangeText={setConfirm}
              placeholder="Re-enter your password"
              placeholderTextColor={colors.textMuted}
              secureTextEntry
              textContentType="newPassword"
              onSubmitEditing={handleRegister}
            />
          </View>

          <Button label="Create Account" loading={loading} onPress={handleRegister} size="lg" style={styles.submitBtn} />
        </View>

        <View style={styles.footer}>
          <Text style={styles.footerText}>Already have an account? </Text>
          <Link href="/(auth)/login" asChild>
            <Pressable>
              <Text style={styles.footerLink}>Sign in</Text>
            </Pressable>
          </Link>
        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  )
}

const styles = StyleSheet.create({
  flex: { flex: 1, backgroundColor: colors.bg },
  container: {
    flexGrow: 1,
    padding: spacing.xl,
    justifyContent: 'center',
    gap: spacing.xl,
  },
  header: { gap: spacing.xs },
  title: { fontSize: 28, fontWeight: '800', color: colors.textPrimary },
  subtitle: { fontSize: 15, color: colors.textSecondary },
  form: { gap: spacing.md },
  label: {
    fontSize: 13,
    fontWeight: '600',
    color: colors.textSecondary,
    marginBottom: spacing.xs,
  },
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
  submitBtn: { marginTop: spacing.sm },
  footer: { flexDirection: 'row', justifyContent: 'center' },
  footerText: { color: colors.textSecondary, fontSize: 14 },
  footerLink: { color: colors.primary, fontWeight: '600', fontSize: 14 },
})
