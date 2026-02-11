import { apiFetch } from './client'

export type AdminHealthTable = {
  name: string
  count: number
  lastCreatedAt?: string | null
  lastUpdatedAt?: string | null
}

export type AdminHealthSummary = {
  serverTimeUtc: string
  tables: AdminHealthTable[]
}

export const fetchAdminHealth = async (token: string) => {
  return apiFetch<AdminHealthSummary>('/admin/health', { token })
}
