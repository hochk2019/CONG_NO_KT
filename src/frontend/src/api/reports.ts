import { apiFetch, apiFetchBlob } from './client'
import type { PagedResult } from './types'

export type ReportSummaryRow = {
  groupKey: string
  groupName?: string | null
  invoicedTotal: number
  advancedTotal: number
  receiptedTotal: number
  outstandingInvoice: number
  outstandingAdvance: number
  currentBalance: number
}

export type ReportKpi = {
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

export type ReportChartPoint = {
  date: string
  value: number
}

export type ReportAgingDistribution = {
  bucket0To30: number
  bucket31To60: number
  bucket61To90: number
  bucket91To180: number
  bucketOver180: number
}

export type ReportAllocationStatus = {
  status: string
  amount: number
}

export type ReportCharts = {
  cashFlow: ReportChartPoint[]
  agingDistribution: ReportAgingDistribution
  allocationStatuses: ReportAllocationStatus[]
}

export type ReportTopCustomer = {
  customerTaxCode: string
  customerName: string
  amount: number
  daysPastDue?: number | null
  ratio?: number | null
}

export type ReportOverdueGroup = {
  groupKey: string
  groupName: string
  totalOutstanding: number
  overdueAmount: number
  overdueRatio: number
  overdueCustomers: number
}

export type ReportInsights = {
  topOutstanding: ReportTopCustomer[]
  topOnTime: ReportTopCustomer[]
  overdueByOwner: ReportOverdueGroup[]
}

export type ReportPreferences = {
  kpiOrder: string[]
  dueSoonDays: number
}

export type ReportStatementLine = {
  documentDate: string
  appliedPeriodStart?: string | null
  type: string
  sellerTaxCode: string
  customerTaxCode: string
  customerName: string
  documentNo?: string | null
  description?: string | null
  revenue: number
  vat: number
  increase: number
  decrease: number
  runningBalance: number
  createdBy?: string | null
  approvedBy?: string | null
  batch?: string | null
}

export type ReportStatementResult = {
  openingBalance: number
  closingBalance: number
  lines: ReportStatementLine[]
}

export type ReportStatementPagedResult = {
  openingBalance: number
  closingBalance: number
  lines: ReportStatementLine[]
  page: number
  pageSize: number
  total: number
}

export type ReportAgingRow = {
  customerTaxCode: string
  customerName: string
  sellerTaxCode: string
  bucket0To30: number
  bucket31To60: number
  bucket61To90: number
  bucket91To180: number
  bucketOver180: number
  total: number
  overdue: number
}

export type ReportFilterParams = {
  from?: string
  to?: string
  asOfDate?: string
  sellerTaxCode?: string
  customerTaxCode?: string
  ownerId?: string
  groupBy?: string
  filterText?: string
  dueSoonDays?: number
  top?: number
}

export type ReportPagedParams = ReportFilterParams & {
  page: number
  pageSize: number
  sortKey?: string
  sortDirection?: string
}

export type ReportExportKind = 'Full' | 'Overview' | 'Summary' | 'Statement' | 'Aging'

const buildQuery = (params: ReportFilterParams) => {
  const query = new URLSearchParams()
  if (params.from) query.append('from', params.from)
  if (params.to) query.append('to', params.to)
  if (params.asOfDate) query.append('asOfDate', params.asOfDate)
  if (params.sellerTaxCode) query.append('sellerTaxCode', params.sellerTaxCode)
  if (params.customerTaxCode) query.append('customerTaxCode', params.customerTaxCode)
  if (params.ownerId) query.append('ownerId', params.ownerId)
  if (params.groupBy) query.append('groupBy', params.groupBy)
  if (params.filterText) query.append('filterText', params.filterText)
  if (params.dueSoonDays) query.append('dueSoonDays', String(params.dueSoonDays))
  if (params.top) query.append('top', String(params.top))
  return query
}

const buildPagedQuery = (params: ReportPagedParams) => {
  const query = buildQuery(params)
  query.append('page', String(params.page))
  query.append('pageSize', String(params.pageSize))
  if (params.sortKey) query.append('sortKey', params.sortKey)
  if (params.sortDirection) query.append('sortDirection', params.sortDirection)
  return query
}

export const fetchReportSummary = async (token: string, params: ReportFilterParams) => {
  const query = buildQuery(params)
  return apiFetch<ReportSummaryRow[]>(`/reports/summary?${query.toString()}`, { token })
}

export const fetchReportSummaryPaged = async (token: string, params: ReportPagedParams) => {
  const query = buildPagedQuery(params)
  return apiFetch<PagedResult<ReportSummaryRow>>(`/reports/summary/paged?${query.toString()}`, {
    token,
  })
}

export const fetchReportStatement = async (token: string, params: ReportFilterParams) => {
  const query = buildQuery(params)
  return apiFetch<ReportStatementResult>(`/reports/statement?${query.toString()}`, { token })
}

export const fetchReportStatementPaged = async (token: string, params: ReportPagedParams) => {
  const query = buildPagedQuery(params)
  return apiFetch<ReportStatementPagedResult>(
    `/reports/statement/paged?${query.toString()}`,
    { token },
  )
}

export const fetchReportAging = async (token: string, params: ReportFilterParams) => {
  const query = buildQuery(params)
  return apiFetch<ReportAgingRow[]>(`/reports/aging?${query.toString()}`, { token })
}

export const fetchReportAgingPaged = async (token: string, params: ReportPagedParams) => {
  const query = buildPagedQuery(params)
  return apiFetch<PagedResult<ReportAgingRow>>(`/reports/aging/paged?${query.toString()}`, {
    token,
  })
}

export const fetchReportKpis = async (token: string, params: ReportFilterParams) => {
  const query = buildQuery(params)
  return apiFetch<ReportKpi>(`/reports/kpis?${query.toString()}`, { token })
}

export const fetchReportCharts = async (token: string, params: ReportFilterParams) => {
  const query = buildQuery(params)
  return apiFetch<ReportCharts>(`/reports/charts?${query.toString()}`, { token })
}

export const fetchReportInsights = async (token: string, params: ReportFilterParams) => {
  const query = buildQuery(params)
  return apiFetch<ReportInsights>(`/reports/insights?${query.toString()}`, { token })
}

export const fetchReportPreferences = async (token: string) => {
  return apiFetch<ReportPreferences>('/reports/preferences', { token })
}

export const updateReportPreferences = async (
  token: string,
  payload: {
    kpiOrder?: string[]
    dueSoonDays?: number
  },
) => {
  return apiFetch<ReportPreferences>('/reports/preferences', {
    token,
    method: 'PUT',
    body: payload,
  })
}

export const exportReport = async (
  token: string,
  params: ReportFilterParams,
  kind: ReportExportKind = 'Full',
) => {
  const query = buildQuery(params)
  if (kind) query.append('kind', kind)
  const { blob, headers } = await apiFetchBlob(`/reports/export?${query.toString()}`, {
    token,
  })
  const contentDisposition = headers.get('content-disposition') ?? ''
  const match = /filename="?([^"]+)"?/i.exec(contentDisposition)
  const fileName = match?.[1] || 'congno_report_export.xlsx'

  return { blob, fileName }
}

