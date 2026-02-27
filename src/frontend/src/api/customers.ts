import { apiFetch } from './client'
import type { PagedResult } from './types'

export type CustomerListItem = {
  taxCode: string
  name: string
  ownerName?: string | null
  currentBalance: number
  status: string
}

export type CustomerDetail = {
  taxCode: string
  name: string
  address?: string | null
  email?: string | null
  phone?: string | null
  status: string
  currentBalance: number
  paymentTermsDays: number
  creditLimit?: number | null
  ownerId?: string | null
  ownerName?: string | null
  managerId?: string | null
  managerName?: string | null
  createdAt: string
  updatedAt: string
}

export type Customer360Summary = {
  totalOutstanding: number
  overdueAmount: number
  overdueRatio: number
  maxDaysPastDue: number
  openInvoiceCount: number
  nextDueDate?: string | null
}

export type Customer360RiskSnapshot = {
  score?: number | null
  signal?: string | null
  asOfDate?: string | null
  modelVersion?: string | null
  createdAt?: string | null
}

export type Customer360ReminderLog = {
  id: string
  channel: string
  status: string
  riskLevel: string
  escalationLevel: number
  escalationReason?: string | null
  message?: string | null
  sentAt?: string | null
  createdAt: string
}

export type Customer360ResponseState = {
  channel: string
  responseStatus: string
  latestResponseAt?: string | null
  escalationLocked: boolean
  attemptCount: number
  currentEscalationLevel: number
  lastSentAt?: string | null
  updatedAt: string
}

export type Customer360 = {
  taxCode: string
  name: string
  status: string
  currentBalance: number
  paymentTermsDays: number
  creditLimit?: number | null
  ownerName?: string | null
  managerName?: string | null
  summary: Customer360Summary
  riskSnapshot: Customer360RiskSnapshot
  reminderTimeline: Customer360ReminderLog[]
  responseStates: Customer360ResponseState[]
}

export type CustomerUpdateRequest = {
  name: string
  address?: string | null
  email?: string | null
  phone?: string | null
  status: string
  paymentTermsDays: number
  creditLimit?: number | null
  ownerId?: string | null
  managerId?: string | null
}

export type CustomerInvoice = {
  id: string
  invoiceNo: string
  issueDate: string
  totalAmount: number
  outstandingAmount: number
  status: string
  version: number
  sellerTaxCode: string
  sellerShortName?: string | null
  receiptRefs: CustomerReceiptRef[]
}

export type CustomerAdvance = {
  id: string
  advanceNo?: string | null
  advanceDate: string
  amount: number
  outstandingAmount: number
  status: string
  version: number
  sellerTaxCode: string
  sellerShortName?: string | null
  receiptRefs: CustomerReceiptRef[]
}

export type CustomerReceipt = {
  id: string
  receiptNo?: string | null
  receiptDate: string
  appliedPeriodStart?: string | null
  amount: number
  unallocatedAmount: number
  status: string
  sellerTaxCode: string
  sellerShortName?: string | null
}

export type CustomerReceiptRef = {
  id: string
  receiptNo?: string | null
  receiptDate: string
  amount: number
}

export const fetchCustomers = async (params: {
  token: string
  search?: string
  ownerId?: string
  status?: string
  page: number
  pageSize: number
}) => {
  const query = new URLSearchParams({
    page: String(params.page),
    pageSize: String(params.pageSize),
  })
  if (params.search) query.append('search', params.search)
  if (params.ownerId) query.append('ownerId', params.ownerId)
  if (params.status) query.append('status', params.status)

  return apiFetch<PagedResult<CustomerListItem>>(`/customers?${query.toString()}`, {
    token: params.token,
  })
}

export const fetchCustomerDetail = async (token: string, taxCode: string) => {
  return apiFetch<CustomerDetail>(`/customers/${taxCode}`, { token })
}

export const fetchCustomer360 = async (token: string, taxCode: string) => {
  return apiFetch<Customer360>(`/customers/${taxCode}/360`, { token })
}

export const updateCustomer = async (
  token: string,
  taxCode: string,
  payload: CustomerUpdateRequest,
) => {
  return apiFetch<void>(`/customers/${taxCode}`, {
    method: 'PUT',
    token,
    body: payload,
  })
}

export const fetchCustomerInvoices = async (params: {
  token: string
  taxCode: string
  status?: string
  search?: string
  from?: string
  to?: string
  page: number
  pageSize: number
}) => {
  const query = new URLSearchParams({
    page: String(params.page),
    pageSize: String(params.pageSize),
  })
  if (params.status) query.append('status', params.status)
  if (params.search) query.append('search', params.search)
  if (params.from) query.append('from', params.from)
  if (params.to) query.append('to', params.to)

  return apiFetch<PagedResult<CustomerInvoice>>(
    `/customers/${params.taxCode}/invoices?${query.toString()}`,
    { token: params.token },
  )
}

export const fetchCustomerAdvances = async (params: {
  token: string
  taxCode: string
  status?: string
  search?: string
  from?: string
  to?: string
  page: number
  pageSize: number
}) => {
  const query = new URLSearchParams({
    page: String(params.page),
    pageSize: String(params.pageSize),
  })
  if (params.status) query.append('status', params.status)
  if (params.search) query.append('search', params.search)
  if (params.from) query.append('from', params.from)
  if (params.to) query.append('to', params.to)

  return apiFetch<PagedResult<CustomerAdvance>>(
    `/customers/${params.taxCode}/advances?${query.toString()}`,
    { token: params.token },
  )
}

export const fetchCustomerReceipts = async (params: {
  token: string
  taxCode: string
  status?: string
  search?: string
  from?: string
  to?: string
  page: number
  pageSize: number
}) => {
  const query = new URLSearchParams({
    page: String(params.page),
    pageSize: String(params.pageSize),
  })
  if (params.status) query.append('status', params.status)
  if (params.search) query.append('search', params.search)
  if (params.from) query.append('from', params.from)
  if (params.to) query.append('to', params.to)

  return apiFetch<PagedResult<CustomerReceipt>>(
    `/customers/${params.taxCode}/receipts?${query.toString()}`,
    { token: params.token },
  )
}
