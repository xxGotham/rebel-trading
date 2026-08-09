import * as signalR from '@microsoft/signalr'
import * as SecureStore from 'expo-secure-store'
import type { Quote, Position, Order, RebelMessage, SessionStatus } from './types'

const BASE_URL = process.env.EXPO_PUBLIC_API_URL ?? 'http://localhost:5000'

export type CoachingHubEvents = {
  QuoteUpdated: (quote: Quote) => void
  PositionUpdated: (position: Position | null) => void
  OrdersUpdated: (orders: Order[]) => void
  RebelSpeaking: (message: RebelMessage) => void
  SessionStatusChanged: (status: SessionStatus) => void
}

class CoachingHubClient {
  private connection: signalR.HubConnection | null = null

  async connect(sessionId: string): Promise<void> {
    const token = await SecureStore.getItemAsync('access_token')

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`${BASE_URL}/hubs/coaching?sessionId=${sessionId}`, {
        accessTokenFactory: () => token ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    await this.connection.start()
  }

  on<K extends keyof CoachingHubEvents>(event: K, handler: CoachingHubEvents[K]): void {
    this.connection?.on(event, handler as (...args: unknown[]) => void)
  }

  off<K extends keyof CoachingHubEvents>(event: K, handler: CoachingHubEvents[K]): void {
    this.connection?.off(event, handler as (...args: unknown[]) => void)
  }

  async disconnect(): Promise<void> {
    await this.connection?.stop()
    this.connection = null
  }

  get state(): signalR.HubConnectionState {
    return this.connection?.state ?? signalR.HubConnectionState.Disconnected
  }
}

export const coachingHub = new CoachingHubClient()
