import { useEffect, useState } from 'react'

type UseServerSyncedPreferencesOptions<TServer, TLocal> = {
  token: string
  pendingPreferences: TLocal
  fetchPreferences: (token: string) => Promise<TServer>
  updatePreferences: (token: string, payload: TLocal) => Promise<TServer>
  toLocal: (value: TServer) => TLocal
  applyLocal: (value: TLocal) => void
  isEqual: (first: TLocal | null, second: TLocal | null) => boolean
  onPersistStart?: () => void
  onPersistError?: (error: unknown) => void
}

export const useServerSyncedPreferences = <TServer, TLocal>({
  token,
  pendingPreferences,
  fetchPreferences,
  updatePreferences,
  toLocal,
  applyLocal,
  isEqual,
  onPersistStart,
  onPersistError,
}: UseServerSyncedPreferencesOptions<TServer, TLocal>) => {
  const [preferencesLoaded, setPreferencesLoaded] = useState(false)
  const [lastSavedPreferences, setLastSavedPreferences] = useState<TLocal | null>(null)

  useEffect(() => {
    if (!token) {
      setPreferencesLoaded(false)
      setLastSavedPreferences(null)
      return
    }

    let isActive = true

    const loadPreferences = async () => {
      try {
        const result = await fetchPreferences(token)
        if (!isActive) return
        const localValue = toLocal(result)
        applyLocal(localValue)
        setLastSavedPreferences(localValue)
      } catch {
        if (!isActive) return
      } finally {
        if (isActive) {
          setPreferencesLoaded(true)
        }
      }
    }

    void loadPreferences()

    return () => {
      isActive = false
    }
  }, [token, fetchPreferences, toLocal, applyLocal])

  useEffect(() => {
    if (!token || !preferencesLoaded) return
    if (isEqual(lastSavedPreferences, pendingPreferences)) return

    let isActive = true

    const persistPreferences = async () => {
      onPersistStart?.()
      try {
        const result = await updatePreferences(token, pendingPreferences)
        if (!isActive) return
        const localValue = toLocal(result)
        applyLocal(localValue)
        setLastSavedPreferences(localValue)
      } catch (error) {
        if (!isActive) return
        onPersistError?.(error)
      }
    }

    void persistPreferences()

    return () => {
      isActive = false
    }
  }, [
    token,
    preferencesLoaded,
    pendingPreferences,
    lastSavedPreferences,
    updatePreferences,
    toLocal,
    applyLocal,
    isEqual,
    onPersistStart,
    onPersistError,
  ])

  return { preferencesLoaded }
}
