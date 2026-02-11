import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useAuth } from './AuthStore'
import {
  fetchNotifications,
  fetchNotificationPreferences,
  markAllNotificationsRead,
  markNotificationRead,
  updateNotificationPreferences,
  type NotificationItem,
  type NotificationPreferences,
} from '../api/notifications'
import { NotificationCenterContext } from './notification-center-context'

const DEFAULT_PREFERENCES: NotificationPreferences = {
  receiveNotifications: true,
  popupEnabled: true,
  popupSeverities: ['WARN', 'ALERT'],
  popupSources: ['RISK', 'RECEIPT', 'IMPORT', 'SYSTEM'],
}

const normalizeToken = (value: string) => value.trim().toUpperCase()
const normalizeSeverity = (value: string) => {
  const token = normalizeToken(value)
  return token === 'CRITICAL' ? 'ALERT' : token
}

const dedupe = (values: string[]) => Array.from(new Set(values))

const normalizePreferences = (prefs: NotificationPreferences): NotificationPreferences => ({
  ...prefs,
  popupSeverities: dedupe(prefs.popupSeverities.map(normalizeSeverity)),
  popupSources: dedupe(prefs.popupSources.map(normalizeToken)),
})

export function NotificationCenterProvider({ children }: { children: React.ReactNode }) {
  const { state } = useAuth()
  const token = state.accessToken
  const seenKey = useMemo(() => `notify_seen_${state.username ?? 'user'}`, [state.username])

  const [unreadCount, setUnreadCount] = useState(0)
  const [unreadItems, setUnreadItems] = useState<NotificationItem[]>([])
  const [preferences, setPreferences] = useState<NotificationPreferences | null>(null)
  const [preferencesLoading, setPreferencesLoading] = useState(false)
  const [toasts, setToasts] = useState<NotificationItem[]>([])
  const [criticalModal, setCriticalModal] = useState<NotificationItem | null>(null)

  const seenIdsRef = useRef<Set<string>>(new Set())
  const criticalShownRef = useRef<Set<string>>(new Set())
  const pollingRef = useRef<number | null>(null)

  useEffect(() => {
    const raw = localStorage.getItem(seenKey)
    if (!raw) {
      seenIdsRef.current = new Set()
      return
    }
    try {
      const parsed = JSON.parse(raw) as string[]
      seenIdsRef.current = new Set(parsed)
    } catch {
      seenIdsRef.current = new Set()
    }
  }, [seenKey])

  const persistSeenIds = useCallback(() => {
    const values = Array.from(seenIdsRef.current).slice(-200)
    localStorage.setItem(seenKey, JSON.stringify(values))
  }, [seenKey])

  const enqueueToast = useCallback((item: NotificationItem) => {
    setToasts((prev) => {
      if (prev.some((toast) => toast.id === item.id)) return prev
      return [...prev, item].slice(-4)
    })
  }, [])

  const handlePopup = useCallback(
    (items: NotificationItem[]) => {
      const prefs = preferences ?? DEFAULT_PREFERENCES
      if (!prefs.popupEnabled || !prefs.receiveNotifications) {
        return
      }

      const allowedSeverities = new Set(prefs.popupSeverities.map(normalizeSeverity))
      const allowedSources = new Set(prefs.popupSources.map(normalizeToken))

      items.forEach((item) => {
        if (seenIdsRef.current.has(item.id)) return
        seenIdsRef.current.add(item.id)

        const severity = normalizeSeverity(item.severity)
        const source = normalizeToken(item.source)
        if (!allowedSeverities.has(severity) || !allowedSources.has(source)) {
          return
        }

        if (severity === 'ALERT') {
          if (criticalShownRef.current.has(item.id)) return
          criticalShownRef.current.add(item.id)
          setCriticalModal(item)
          return
        }

        enqueueToast(item)
      })

      persistSeenIds()
    },
    [enqueueToast, persistSeenIds, preferences],
  )

  const refreshUnread = useCallback(async () => {
    if (!token) return
    try {
      const result = await fetchNotifications({
        token,
        unreadOnly: true,
        page: 1,
        pageSize: 10,
      })
      setUnreadItems(result.items)
      setUnreadCount(result.total)
      handlePopup(result.items)
    } catch {
      setUnreadItems([])
      setUnreadCount(0)
    }
  }, [handlePopup, token])

  const loadPreferences = useCallback(async () => {
    if (!token) return
    setPreferencesLoading(true)
    try {
      const result = await fetchNotificationPreferences(token)
      setPreferences(normalizePreferences(result))
    } catch {
      setPreferences(DEFAULT_PREFERENCES)
    } finally {
      setPreferencesLoading(false)
    }
  }, [token])

  useEffect(() => {
    if (!token) return
    loadPreferences()
  }, [loadPreferences, token])

  useEffect(() => {
    if (!token) return
    refreshUnread()
    if (pollingRef.current) {
      window.clearInterval(pollingRef.current)
    }
    pollingRef.current = window.setInterval(() => {
      refreshUnread()
    }, 60000)
    return () => {
      if (pollingRef.current) {
        window.clearInterval(pollingRef.current)
      }
    }
  }, [refreshUnread, token])

  const markRead = useCallback(
    async (id: string) => {
      if (!token) return
      await markNotificationRead(token, id)
      await refreshUnread()
    },
    [refreshUnread, token],
  )

  const markAllRead = useCallback(async () => {
    if (!token) return
    await markAllNotificationsRead(token)
    await refreshUnread()
  }, [refreshUnread, token])

  const handleUpdatePreferences = useCallback(
    async (next: NotificationPreferences) => {
      if (!token) return
      const normalized = normalizePreferences(next)
      const result = await updateNotificationPreferences(token, normalized)
      setPreferences(normalizePreferences(result))
    },
    [token],
  )

  const dismissToast = useCallback((id: string) => {
    setToasts((prev) => prev.filter((toast) => toast.id !== id))
  }, [])

  const dismissCritical = useCallback(() => {
    setCriticalModal(null)
  }, [])

  const value = useMemo(
    () => ({
      unreadCount,
      unreadItems,
      preferences,
      preferencesLoading,
      toasts,
      criticalModal,
      refreshUnread,
      markRead,
      markAllRead,
      updatePreferences: handleUpdatePreferences,
      dismissToast,
      dismissCritical,
    }),
    [
      unreadCount,
      unreadItems,
      preferences,
      preferencesLoading,
      toasts,
      criticalModal,
      refreshUnread,
      markRead,
      markAllRead,
      handleUpdatePreferences,
      dismissToast,
      dismissCritical,
    ],
  )

  return <NotificationCenterContext.Provider value={value}>{children}</NotificationCenterContext.Provider>
}
