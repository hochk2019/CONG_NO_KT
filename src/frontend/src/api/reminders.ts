import { apiFetch } from './client'
import type { PagedResult } from './types'

export type ReminderSettings = {
  enabled: boolean
  frequencyDays: number
  upcomingDueDays: number
  channels: string[]
  targetLevels: string[]
  lastRunAt?: string | null
  nextRunAt?: string | null
}

export type ReminderRunResult = {
  runAt: string
  totalCandidates: number
  sentCount: number
  failedCount: number
  skippedCount: number
}

export type ReminderLogItem = {
  id: string
  customerTaxCode: string
  customerName: string
  ownerUserId?: string | null
  ownerName?: string | null
  riskLevel: string
  channel: string
  status: string
  message?: string | null
  errorDetail?: string | null
  sentAt?: string | null
  createdAt: string
}

export const fetchReminderSettings = async (token: string) => {
  return apiFetch<ReminderSettings>('/reminders/settings', { token })
}

export const updateReminderSettings = async (
  token: string,
  payload: Pick<
    ReminderSettings,
    'enabled' | 'frequencyDays' | 'upcomingDueDays' | 'channels' | 'targetLevels'
  >,
) => {
  return apiFetch<void>('/reminders/settings', {
    method: 'PUT',
    token,
    body: payload,
  })
}

export const runReminders = async (token: string) => {
  return apiFetch<ReminderRunResult>('/reminders/run', { method: 'POST', token })
}

export const fetchReminderLogs = async (params: {
  token: string
  channel?: string
  status?: string
  ownerId?: string
  page: number
  pageSize: number
}) => {
  const query = new URLSearchParams({
    page: String(params.page),
    pageSize: String(params.pageSize),
  })
  if (params.channel) query.append('channel', params.channel)
  if (params.status) query.append('status', params.status)
  if (params.ownerId) query.append('ownerId', params.ownerId)

  return apiFetch<PagedResult<ReminderLogItem>>(`/reminders/logs?${query.toString()}`, {
    token: params.token,
  })
}
