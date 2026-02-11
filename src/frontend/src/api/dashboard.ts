import { apiFetch } from './client'

export type DashboardKpis = {
  totalOutstanding: number
  outstandingInvoice: number
  outstandingAdvance: number
  overdueTotal: number
  overdueCustomers: number
  onTimeCustomers: number
  unallocatedReceiptsAmount: number
  unallocatedReceiptsCount: number
  pendingReceiptsCount: number
  pendingReceiptsAmount: number
  pendingAdvancesCount: number
  pendingAdvancesAmount: number
  pendingImportBatches: number
  lockedPeriodsCount: number
}

export type DashboardTrendPoint = {
  period: string
  invoicedTotal: number
  advancedTotal: number
  receiptedTotal: number
}

export type DashboardTopItem = {
  customerTaxCode: string
  customerName: string
  amount: number
  daysPastDue?: number | null
}

export type DashboardAgingBucket = {
  bucket: string
  amount: number
}

export type DashboardAllocationStatus = {
  status: string
  amount: number
}

export type DashboardOverview = {
  trendFrom: string
  trendTo: string
  kpis: DashboardKpis
  trend: DashboardTrendPoint[]
  topOutstanding: DashboardTopItem[]
  topOnTime: DashboardTopItem[]
  topOverdueDays: DashboardTopItem[]
  agingBuckets: DashboardAgingBucket[]
  allocationStatuses: DashboardAllocationStatus[]
  lastUpdatedAt: string
}

export type DashboardOverdueGroupItem = {
  groupKey: string
  groupName: string
  totalOutstanding: number
  overdueAmount: number
  overdueRatio: number
  overdueCustomers: number
}

export const fetchDashboardOverview = async (params: {
  token: string
  from?: string
  to?: string
  months?: number
  top?: number
  trendGranularity?: string
  trendPeriods?: number
}) => {
  const query = new URLSearchParams()
  if (params.from) query.append('from', params.from)
  if (params.to) query.append('to', params.to)
  if (params.months) query.append('months', String(params.months))
  if (params.top) query.append('top', String(params.top))
  if (params.trendGranularity) query.append('trendGranularity', params.trendGranularity)
  if (params.trendPeriods) query.append('trendPeriods', String(params.trendPeriods))

  const suffix = query.toString()
  return apiFetch<DashboardOverview>(`/dashboard/overview${suffix ? `?${suffix}` : ''}`, {
    token: params.token,
  })
}

export const fetchDashboardOverdueGroups = async (params: {
  token: string
  asOf?: string
  top?: number
  groupBy?: string
}) => {
  const query = new URLSearchParams()
  if (params.asOf) query.append('asOf', params.asOf)
  if (params.top) query.append('top', String(params.top))
  if (params.groupBy) query.append('groupBy', params.groupBy)

  const suffix = query.toString()
  return apiFetch<DashboardOverdueGroupItem[]>(
    `/dashboard/overdue-groups${suffix ? `?${suffix}` : ''}`,
    { token: params.token },
  )
}
