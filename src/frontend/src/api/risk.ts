import { apiFetch } from './client'
import type { PagedResult } from './types'

export type RiskOverviewItem = {
  level: string
  customers: number
  totalOutstanding: number
  overdueAmount: number
}

export type RiskOverview = {
  asOfDate: string
  items: RiskOverviewItem[]
  totalCustomers: number
  totalOutstanding: number
  totalOverdue: number
}

export type RiskCustomerItem = {
  customerTaxCode: string
  customerName: string
  ownerId?: string | null
  ownerName?: string | null
  totalOutstanding: number
  overdueAmount: number
  overdueRatio: number
  maxDaysPastDue: number
  lateCount: number
  riskLevel: string
}

export type RiskRule = {
  level: string
  minOverdueDays: number
  minOverdueRatio: number
  minLateCount: number
  isActive: boolean
}

export const fetchRiskOverview = async (params: { token: string; asOfDate?: string }) => {
  const query = new URLSearchParams()
  if (params.asOfDate) query.append('asOfDate', params.asOfDate)
  const suffix = query.toString()
  return apiFetch<RiskOverview>(`/risk/overview${suffix ? `?${suffix}` : ''}`, {
    token: params.token,
  })
}

export const fetchRiskCustomers = async (params: {
  token: string
  search?: string
  ownerId?: string
  level?: string
  asOfDate?: string
  page: number
  pageSize: number
  sort?: string
  order?: string
}) => {
  const query = new URLSearchParams({
    page: String(params.page),
    pageSize: String(params.pageSize),
  })
  if (params.search) query.append('search', params.search)
  if (params.ownerId) query.append('ownerId', params.ownerId)
  if (params.level) query.append('level', params.level)
  if (params.asOfDate) query.append('asOfDate', params.asOfDate)
  if (params.sort) query.append('sort', params.sort)
  if (params.order) query.append('order', params.order)

  return apiFetch<PagedResult<RiskCustomerItem>>(`/risk/customers?${query.toString()}`, {
    token: params.token,
  })
}

export const fetchRiskRules = async (token: string) => {
  return apiFetch<RiskRule[]>('/risk/rules', { token })
}

export const updateRiskRules = async (token: string, rules: RiskRule[]) => {
  return apiFetch<void>('/risk/rules', {
    method: 'PUT',
    token,
    body: { rules },
  })
}
