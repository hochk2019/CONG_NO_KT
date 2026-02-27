import { apiFetch } from './client'

export type ErpIntegrationStatus = {
  provider: string
  enabled: boolean
  configured: boolean
  hasApiKey: boolean
  baseUrl?: string | null
  companyCode?: string | null
  timeoutSeconds: number
  lastSyncAtUtc?: string | null
  lastSyncStatus?: string | null
  lastSyncMessage?: string | null
}

export type ErpIntegrationConfig = {
  enabled: boolean
  provider: string
  baseUrl?: string | null
  companyCode?: string | null
  timeoutSeconds: number
  hasApiKey: boolean
  updatedAtUtc?: string | null
  updatedBy?: string | null
}

export type ErpIntegrationConfigUpdateRequest = {
  enabled: boolean
  provider?: string | null
  baseUrl?: string | null
  companyCode?: string | null
  timeoutSeconds: number
  apiKey?: string | null
  clearApiKey: boolean
}

export type ErpSyncSummaryRequest = {
  from?: string
  to?: string
  asOfDate?: string
  dueSoonDays?: number
  dryRun?: boolean
}

export type ErpSyncSummaryPayload = {
  from?: string | null
  to?: string | null
  asOfDate?: string | null
  dueSoonDays: number
  totalOutstanding: number
  outstandingInvoice: number
  outstandingAdvance: number
  unallocatedReceiptsAmount: number
  unallocatedReceiptsCount: number
  overdueAmount: number
  overdueCustomers: number
  dueSoonAmount: number
  dueSoonCustomers: number
  onTimeCustomers: number
}

export type ErpSyncSummaryResult = {
  success: boolean
  status: string
  message: string
  executedAtUtc: string
  provider: string
  requestId?: string | null
  payload: ErpSyncSummaryPayload
}

export const fetchErpIntegrationStatus = async (token: string) => {
  return apiFetch<ErpIntegrationStatus>('/admin/erp-integration/status', { token })
}

export const fetchErpIntegrationConfig = async (token: string) => {
  return apiFetch<ErpIntegrationConfig>('/admin/erp-integration/config', { token })
}

export const updateErpIntegrationConfig = async (token: string, payload: ErpIntegrationConfigUpdateRequest) => {
  return apiFetch<ErpIntegrationConfig>('/admin/erp-integration/config', {
    token,
    method: 'PUT',
    body: payload,
  })
}

export const syncErpSummary = async (token: string, payload: ErpSyncSummaryRequest) => {
  return apiFetch<ErpSyncSummaryResult>('/admin/erp-integration/sync-summary', {
    token,
    method: 'POST',
    body: payload,
  })
}
