import { useEffect, useRef } from 'react'
import { coachingHub } from '@/lib/signalr'
import { sessionsApi } from '@/lib/api'
import { useSessionStore } from '@/store/sessionStore'
import type { CoachingSession } from '@/lib/types'

export function useCoachingSession() {
  const {
    session,
    setSession,
    updateStatus,
    updateQuote,
    updatePosition,
    updateOrders,
    addMessage,
    clearSession,
  } = useSessionStore()

  const hubConnected = useRef(false)

  useEffect(() => {
    if (!session?.id || hubConnected.current) return

    const connect = async () => {
      await coachingHub.connect(session.id)
      hubConnected.current = true

      coachingHub.on('QuoteUpdated', updateQuote)
      coachingHub.on('PositionUpdated', updatePosition)
      coachingHub.on('OrdersUpdated', updateOrders)
      coachingHub.on('RebelSpeaking', addMessage)
      coachingHub.on('SessionStatusChanged', updateStatus)
    }

    connect().catch(console.error)

    return () => {
      coachingHub.off('QuoteUpdated', updateQuote)
      coachingHub.off('PositionUpdated', updatePosition)
      coachingHub.off('OrdersUpdated', updateOrders)
      coachingHub.off('RebelSpeaking', addMessage)
      coachingHub.off('SessionStatusChanged', updateStatus)
      coachingHub.disconnect()
      hubConnected.current = false
    }
  }, [session?.id])

  const startSession = async (playbookId: string, instrument: string): Promise<CoachingSession> => {
    const newSession = await sessionsApi.start(playbookId, instrument)
    setSession(newSession)
    return newSession
  }

  const endSession = async () => {
    if (!session?.id) return
    await sessionsApi.end(session.id)
    await coachingHub.disconnect()
    hubConnected.current = false
    clearSession()
  }

  const askRebel = async (question: string) => {
    if (!session?.id) return
    addMessage({
      id: `user-${Date.now()}`,
      text: question,
      timestamp: new Date().toISOString(),
      isUserQuestion: true,
    })
    await sessionsApi.ask(session.id, question)
  }

  return { session, startSession, endSession, askRebel }
}
