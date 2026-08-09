import { create } from 'zustand'
import type { CoachingSession, Order, Position, Quote, RebelMessage, SessionStatus } from '@/lib/types'

interface SessionState {
  session: CoachingSession | null
  messages: RebelMessage[]
  isMuted: boolean
  isListening: boolean

  setSession: (session: CoachingSession | null) => void
  updateStatus: (status: SessionStatus) => void
  updateQuote: (quote: Quote) => void
  updatePosition: (position: Position | null) => void
  updateOrders: (orders: Order[]) => void
  addMessage: (message: RebelMessage) => void
  toggleMute: () => void
  setListening: (val: boolean) => void
  clearSession: () => void
}

export const useSessionStore = create<SessionState>((set) => ({
  session: null,
  messages: [],
  isMuted: false,
  isListening: false,

  setSession: (session) => set({ session, messages: [] }),

  updateStatus: (status) =>
    set((s) => s.session ? { session: { ...s.session, status } } : {}),

  updateQuote: (quote) =>
    set((s) => s.session ? { session: { ...s.session, currentQuote: quote } } : {}),

  updatePosition: (position) =>
    set((s) => s.session ? { session: { ...s.session, currentPosition: position ?? undefined } } : {}),

  updateOrders: (orders) =>
    set((s) => s.session ? { session: { ...s.session, openOrders: orders } } : {}),

  addMessage: (message) =>
    set((s) => ({ messages: [message, ...s.messages].slice(0, 100) })),

  toggleMute: () => set((s) => ({ isMuted: !s.isMuted })),
  setListening: (val) => set({ isListening: val }),

  clearSession: () => set({ session: null, messages: [], isMuted: false, isListening: false }),
}))
