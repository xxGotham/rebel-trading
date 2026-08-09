import axios from 'axios'
import * as SecureStore from 'expo-secure-store'
import type {
  AuthTokens,
  CoachingSession,
  Playbook,
  PreflightCheck,
  ProviderConnection,
  SessionSummary,
  User,
} from './types'

const BASE_URL = process.env.EXPO_PUBLIC_API_URL ?? 'http://localhost:5000'

export const http = axios.create({ baseURL: BASE_URL })

http.interceptors.request.use(async (config) => {
  const token = await SecureStore.getItemAsync('access_token')
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

http.interceptors.response.use(
  (r) => r,
  async (error) => {
    if (error.response?.status === 401) {
      try {
        const refreshToken = await SecureStore.getItemAsync('refresh_token')
        const { data } = await axios.post<AuthTokens>(`${BASE_URL}/auth/refresh`, { refreshToken })
        await SecureStore.setItemAsync('access_token', data.accessToken)
        await SecureStore.setItemAsync('refresh_token', data.refreshToken)
        error.config.headers.Authorization = `Bearer ${data.accessToken}`
        return http.request(error.config)
      } catch {
        await SecureStore.deleteItemAsync('access_token')
        await SecureStore.deleteItemAsync('refresh_token')
      }
    }
    return Promise.reject(error)
  },
)

// ─── Auth ───────────────────────────────────────────────────────────────────

export const authApi = {
  login: async (email: string, password: string) => {
    const { data } = await http.post<AuthTokens & { user: User }>('/auth/login', { email, password })
    return data
  },
  register: async (email: string, username: string, password: string) => {
    const { data } = await http.post<AuthTokens & { user: User }>('/auth/register', {
      email,
      username,
      password,
    })
    return data
  },
  me: async () => {
    const { data } = await http.get<User>('/auth/me')
    return data
  },
  logout: async () => {
    await http.post('/auth/logout')
  },
}

// ─── Playbooks ───────────────────────────────────────────────────────────────

export const playbooksApi = {
  list: async () => {
    const { data } = await http.get<Playbook[]>('/playbooks')
    return data
  },
  get: async (id: string) => {
    const { data } = await http.get<Playbook>(`/playbooks/${id}`)
    return data
  },
  create: async (payload: Omit<Playbook, 'id' | 'version' | 'createdAt' | 'updatedAt'>) => {
    const { data } = await http.post<Playbook>('/playbooks', payload)
    return data
  },
  update: async (id: string, payload: Partial<Playbook>) => {
    const { data } = await http.put<Playbook>(`/playbooks/${id}`, payload)
    return data
  },
  delete: async (id: string) => {
    await http.delete(`/playbooks/${id}`)
  },
  activate: async (id: string) => {
    const { data } = await http.post<Playbook>(`/playbooks/${id}/activate`)
    return data
  },
}

// ─── Providers ───────────────────────────────────────────────────────────────

export const providersApi = {
  list: async () => {
    const { data } = await http.get<ProviderConnection[]>('/providers')
    return data
  },
  connect: async (
    providerType: string,
    providerId: string,
    credentials: Record<string, string>,
    keySource: 'REBEL_MANAGED' | 'USER_MANAGED_BYOK',
  ) => {
    const { data } = await http.post<ProviderConnection>('/providers/connect', {
      providerType,
      providerId,
      credentials,
      keySource,
    })
    return data
  },
  disconnect: async (providerId: string) => {
    await http.delete(`/providers/${providerId}`)
  },
}

// ─── Sessions ────────────────────────────────────────────────────────────────

export const sessionsApi = {
  preflight: async (playbookId: string, instrument: string) => {
    const { data } = await http.post<PreflightCheck>('/sessions/preflight', {
      playbookId,
      instrument,
    })
    return data
  },
  start: async (playbookId: string, instrument: string) => {
    const { data } = await http.post<CoachingSession>('/sessions/start', {
      playbookId,
      instrument,
    })
    return data
  },
  end: async (sessionId: string) => {
    const { data } = await http.post<CoachingSession>(`/sessions/${sessionId}/end`)
    return data
  },
  get: async (sessionId: string) => {
    const { data } = await http.get<CoachingSession>(`/sessions/${sessionId}`)
    return data
  },
  history: async () => {
    const { data } = await http.get<SessionSummary[]>('/sessions/history')
    return data
  },
  ask: async (sessionId: string, question: string) => {
    const { data } = await http.post<{ messageId: string }>(`/sessions/${sessionId}/ask`, {
      question,
    })
    return data
  },
}
