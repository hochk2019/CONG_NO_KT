import { apiFetch } from './client'
import type { PagedResult } from './types'

export type NotificationItem = {
  id: string
  title: string
  body?: string | null
  severity: string
  source: string
  createdAt: string
  readAt?: string | null
}

export type NotificationPreferences = {
  receiveNotifications: boolean
  popupEnabled: boolean
  popupSeverities: string[]
  popupSources: string[]
}

export type NotificationUnreadCount = {
  count: number
}

export const fetchNotifications = async (params: {
  token: string
  unreadOnly?: boolean
  source?: string
  severity?: string
  query?: string
  page: number
  pageSize: number
}) => {
  const query = new URLSearchParams({
    page: String(params.page),
    pageSize: String(params.pageSize),
  })
  if (params.unreadOnly !== undefined) query.append('unreadOnly', String(params.unreadOnly))
  if (params.source) query.append('source', params.source)
  if (params.severity) query.append('severity', params.severity)
  if (params.query) query.append('q', params.query)

  return apiFetch<PagedResult<NotificationItem>>(`/notifications?${query.toString()}`, {
    token: params.token,
  })
}

export const markNotificationRead = async (token: string, id: string) => {
  return apiFetch<void>(`/notifications/${id}/read`, {
    method: 'POST',
    token,
  })
}

export const fetchUnreadCount = async (token: string) => {
  return apiFetch<NotificationUnreadCount>('/notifications/unread-count', { token })
}

export const markAllNotificationsRead = async (token: string) => {
  return apiFetch<void>('/notifications/read-all', {
    method: 'POST',
    token,
  })
}

export const fetchNotificationPreferences = async (token: string) => {
  return apiFetch<NotificationPreferences>('/notifications/preferences', { token })
}

export const updateNotificationPreferences = async (token: string, payload: NotificationPreferences) => {
  return apiFetch<NotificationPreferences>('/notifications/preferences', {
    method: 'PUT',
    token,
    body: payload,
  })
}
