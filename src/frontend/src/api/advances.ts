import { apiFetch } from './client'
import type { PagedResult } from './types'

export type AdvanceDto = {
  id: string
  status: string
  version: number
  outstandingAmount: number
  advanceNo?: string | null
  advanceDate: string
  amount: number
  sellerTaxCode: string
  customerTaxCode: string
}

export type AdvanceListItem = {
  id: string
  status: string
  version: number
  advanceNo?: string | null
  advanceDate: string
  amount: number
  outstandingAmount: number
  sellerTaxCode: string
  customerTaxCode: string
  description?: string | null
  customerName?: string | null
  ownerName?: string | null
  sourceType?: string | null
  canManage: boolean
  sourceBatchId?: string | null
}

export type AdvanceCreateRequest = {
  sellerTaxCode: string
  customerTaxCode: string
  advanceNo?: string | null
  advanceDate: string
  amount: number
  description?: string
}

export const createAdvance = async (token: string, payload: AdvanceCreateRequest) => {
  return apiFetch<AdvanceDto>('/advances', {
    method: 'POST',
    token,
    body: payload,
  })
}

export const listAdvances = async (params: {
  token: string
  sellerTaxCode?: string
  customerTaxCode?: string
  status?: string
  advanceNo?: string
  from?: string
  to?: string
  amountMin?: number
  amountMax?: number
  source?: string
  page: number
  pageSize: number
}) => {
  const query = new URLSearchParams({
    page: String(params.page),
    pageSize: String(params.pageSize),
  })
  if (params.sellerTaxCode) query.append('sellerTaxCode', params.sellerTaxCode)
  if (params.customerTaxCode) query.append('customerTaxCode', params.customerTaxCode)
  if (params.status) query.append('status', params.status)
  if (params.advanceNo) query.append('advanceNo', params.advanceNo)
  if (params.from) query.append('from', params.from)
  if (params.to) query.append('to', params.to)
  if (typeof params.amountMin === 'number') query.append('amountMin', String(params.amountMin))
  if (typeof params.amountMax === 'number') query.append('amountMax', String(params.amountMax))
  if (params.source) query.append('source', params.source)

  return apiFetch<PagedResult<AdvanceListItem>>(`/advances?${query.toString()}`, {
    token: params.token,
  })
}

export const approveAdvance = async (
  token: string,
  advanceId: string,
  payload: { version: number; overridePeriodLock?: boolean; overrideReason?: string },
) => {
  return apiFetch<AdvanceDto>(`/advances/${advanceId}/approve`, {
    method: 'POST',
    token,
    body: {
      version: payload.version,
      override_period_lock: payload.overridePeriodLock ?? false,
      override_reason: payload.overrideReason ?? null,
    },
  })
}

export const voidAdvance = async (
  token: string,
  advanceId: string,
  payload: {
    reason: string
    version: number
    overridePeriodLock?: boolean
    overrideReason?: string
  },
) => {
  return apiFetch<AdvanceDto>(`/advances/${advanceId}/void`, {
    method: 'POST',
    token,
    body: {
      reason: payload.reason,
      version: payload.version,
      override_period_lock: payload.overridePeriodLock ?? false,
      override_reason: payload.overrideReason ?? null,
    },
  })
}

export const unvoidAdvance = async (
  token: string,
  advanceId: string,
  payload: {
    version: number
    overridePeriodLock?: boolean
    overrideReason?: string
  },
) => {
  return apiFetch<AdvanceDto>(`/advances/${advanceId}/unvoid`, {
    method: 'POST',
    token,
    body: {
      version: payload.version,
      override_period_lock: payload.overridePeriodLock ?? false,
      override_reason: payload.overrideReason ?? null,
    },
  })
}

export const updateAdvance = async (
  token: string,
  advanceId: string,
  payload: { description?: string | null; version: number },
) => {
  return apiFetch<{ id: string; version: number; description?: string | null }>(
    `/advances/${advanceId}`,
    {
      method: 'PUT',
      token,
      body: {
        description: payload.description ?? null,
        version: payload.version,
      },
    },
  )
}
