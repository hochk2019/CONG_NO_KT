import { createContext } from 'react'
import type { NotificationItem, NotificationPreferences } from '../api/notifications'

export type NotificationCenterContextValue = {
  unreadCount: number
  unreadItems: NotificationItem[]
  preferences: NotificationPreferences | null
  preferencesLoading: boolean
  toasts: NotificationItem[]
  criticalModal: NotificationItem | null
  refreshUnread: () => Promise<void>
  markRead: (id: string) => Promise<void>
  markAllRead: () => Promise<void>
  updatePreferences: (next: NotificationPreferences) => Promise<void>
  dismissToast: (id: string) => void
  dismissCritical: () => void
}

export const NotificationCenterContext = createContext<NotificationCenterContextValue | undefined>(
  undefined,
)
