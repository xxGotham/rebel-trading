import { useCallback, useEffect, useRef, useState } from 'react'
import { Audio } from 'expo-av'
import * as Speech from 'expo-speech'
import { useSessionStore } from '@/store/sessionStore'

export function useVoice() {
  const { isMuted, messages } = useSessionStore()
  const lastSpokenId = useRef<string | null>(null)
  const [isRecording, setIsRecording] = useState(false)
  const recordingRef = useRef<Audio.Recording | null>(null)

  // Speak new Rebel messages as they arrive
  useEffect(() => {
    const latestMessage = messages[0]
    if (!latestMessage || latestMessage.isUserQuestion) return
    if (latestMessage.id === lastSpokenId.current) return
    if (isMuted) return

    lastSpokenId.current = latestMessage.id
    Speech.speak(latestMessage.text, {
      language: 'en-US',
      pitch: 1.0,
      rate: 0.95,
    })
  }, [messages, isMuted])

  const stopSpeaking = useCallback(() => {
    Speech.stop()
  }, [])

  const startListening = useCallback(async (): Promise<string | null> => {
    try {
      Speech.stop()
      const permission = await Audio.requestPermissionsAsync()
      if (!permission.granted) return null

      await Audio.setAudioModeAsync({
        allowsRecordingIOS: true,
        playsInSilentModeIOS: true,
      })

      const { recording } = await Audio.Recording.createAsync(
        Audio.RecordingOptionsPresets.HIGH_QUALITY,
      )
      recordingRef.current = recording
      setIsRecording(true)
      return null
    } catch (err) {
      console.error('Failed to start recording', err)
      return null
    }
  }, [])

  const stopListening = useCallback(async (): Promise<string | null> => {
    if (!recordingRef.current) return null
    try {
      setIsRecording(false)
      await recordingRef.current.stopAndUnloadAsync()
      const uri = recordingRef.current.getURI()
      recordingRef.current = null

      await Audio.setAudioModeAsync({ allowsRecordingIOS: false })

      // The URI would be sent to the backend speech-to-text service
      // For now return the URI; the caller decides what to do with it
      return uri ?? null
    } catch (err) {
      console.error('Failed to stop recording', err)
      return null
    }
  }, [])

  useEffect(() => {
    return () => {
      Speech.stop()
      recordingRef.current?.stopAndUnloadAsync()
    }
  }, [])

  return { isRecording, startListening, stopListening, stopSpeaking }
}
