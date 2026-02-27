import { apiFetch } from './client'

export type AdminHealthTable = {
  name: string
  count: number
  lastCreatedAt?: string | null
  lastUpdatedAt?: string | null
}

export type AdminBalanceDriftItem = {
  taxCode: string
  currentBalance: number
  expectedBalance: number
  absoluteDrift: number
}

export type AdminBalanceDriftSummary = {
  checkedCustomers: number
  driftedCustomers: number
  totalAbsoluteDrift: number
  maxAbsoluteDrift: number
  topDrifts: AdminBalanceDriftItem[]
}

export type AdminHealthSummary = {
  serverTimeUtc: string
  tables: AdminHealthTable[]
  balanceDrift: AdminBalanceDriftSummary
}

export type AdminBalanceReconcileRequest = {
  applyChanges?: boolean
  maxItems?: number
  tolerance?: number
}

export type AdminBalanceReconcileResult = {
  executedAtUtc: string
  checkedCustomers: number
  driftedCustomers: number
  updatedCustomers: number
  totalAbsoluteDrift: number
  maxAbsoluteDrift: number
  topDrifts: AdminBalanceDriftItem[]
}

export const fetchAdminHealth = async (token: string) => {
  return apiFetch<AdminHealthSummary>('/admin/health', { token })
}

export const runAdminBalanceReconcile = async (
  token: string,
  payload: AdminBalanceReconcileRequest,
) => {
  return apiFetch<AdminBalanceReconcileResult>('/admin/health/reconcile-balances', {
    method: 'POST',
    token,
    body: payload,
  })
}
