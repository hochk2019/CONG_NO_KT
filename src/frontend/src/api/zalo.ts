import { apiFetch } from './client'

export type ZaloLinkStatus = {
  linked: boolean
  zaloUserId?: string | null
  linkedAt?: string | null
}

export type ZaloLinkCode = {
  code: string
  expiresAt: string
}

export const fetchZaloLinkStatus = async (token: string) => {
  return apiFetch<ZaloLinkStatus>('/zalo/link/status', { token })
}

export const requestZaloLinkCode = async (token: string) => {
  return apiFetch<ZaloLinkCode>('/zalo/link/request', { method: 'POST', token })
}
