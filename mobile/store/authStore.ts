import { create } from 'zustand'
import * as SecureStore from 'expo-secure-store'
import { authApi } from '@/lib/api'
import type { User } from '@/lib/types'

interface AuthState {
  user: User | null
  isLoading: boolean
  isAuthenticated: boolean
  login: (email: string, password: string) => Promise<void>
  register: (email: string, username: string, password: string) => Promise<void>
  logout: () => Promise<void>
  loadUser: () => Promise<void>
}

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  isLoading: true,
  isAuthenticated: false,

  login: async (email, password) => {
    const { accessToken, refreshToken, user } = await authApi.login(email, password)
    await SecureStore.setItemAsync('access_token', accessToken)
    await SecureStore.setItemAsync('refresh_token', refreshToken)
    set({ user, isAuthenticated: true })
  },

  register: async (email, username, password) => {
    const { accessToken, refreshToken, user } = await authApi.register(email, username, password)
    await SecureStore.setItemAsync('access_token', accessToken)
    await SecureStore.setItemAsync('refresh_token', refreshToken)
    set({ user, isAuthenticated: true })
  },

  logout: async () => {
    try { await authApi.logout() } catch { /* ignore */ }
    await SecureStore.deleteItemAsync('access_token')
    await SecureStore.deleteItemAsync('refresh_token')
    set({ user: null, isAuthenticated: false })
  },

  loadUser: async () => {
    try {
      const token = await SecureStore.getItemAsync('access_token')
      if (!token) { set({ isLoading: false }); return }
      const user = await authApi.me()
      set({ user, isAuthenticated: true, isLoading: false })
    } catch {
      set({ isLoading: false })
    }
  },
}))
