import { apiFetch } from './client'
import type { PagedResult } from './types'

export type ReceiptPreviewLine = {
  targetId: string
  targetType: string
  amount: number
}

export type ReceiptTargetRef = {
  id: string
  targetType: string
}

export type ReceiptDto = {
  id: string
  status: string
  version: number
  amount: number
  unallocatedAmount: number
  receiptNo?: string | null
  receiptDate: string
  appliedPeriodStart?: string | null
  allocationMode: string
  allocationStatus: string
  allocationPriority: string
  allocationSource?: string | null
  allocationSuggestedAt?: string | null
  selectedTargets?: ReceiptTargetRef[] | null
  method: string
  sellerTaxCode: string
  customerTaxCode: string
}

export type ReceiptCreateRequest = {
  sellerTaxCode: string
  customerTaxCode: string
  receiptNo?: string | null
  receiptDate: string
  amount: number
  allocationMode: string
  appliedPeriodStart?: string | null
  allocationPriority?: string | null
  selectedTargets?: ReceiptTargetRef[] | null
  method?: string | null
  description?: string | null
}

export type ReceiptDraftUpdateRequest = {
  receiptNo?: string | null
  receiptDate: string
  amount: number
  allocationMode: string
  appliedPeriodStart?: string | null
  method?: string | null
  description?: string | null
  allocationPriority?: string | null
  selectedTargets?: ReceiptTargetRef[] | null
  version: number
}

export type ReceiptPreviewResult = {
  lines: ReceiptPreviewLine[]
  unallocatedAmount: number
}

export type ReceiptAllocationDetail = {
  targetType: string
  targetId: string
  targetNo?: string | null
  targetDate: string
  amount: number
}

export type ReceiptListItem = {
  id: string
  status: string
  version: number
  receiptNo?: string | null
  receiptDate: string
  amount: number
  unallocatedAmount: number
  allocationMode: string
  allocationStatus: string
  allocationPriority: string
  allocationSource?: string | null
  allocationSuggestedAt?: string | null
  lastReminderAt?: string | null
  reminderDisabledAt?: string | null
  method: string
  sellerTaxCode: string
  customerTaxCode: string
  customerName?: string | null
  ownerName?: string | null
  canManage: boolean
}

export type ReceiptPreviewRequest = {
  sellerTaxCode: string
  customerTaxCode: string
  amount: number
  allocationMode: string
  appliedPeriodStart?: string | null
  selectedTargets?: ReceiptTargetRef[]
}

export type ReceiptOpenItem = {
  targetType: string
  targetId: string
  documentNo?: string | null
  issueDate: string
  dueDate: string
  outstandingAmount: number
  sellerTaxCode: string
  customerTaxCode: string
}

export const previewReceipt = async (
  token: string,
  payload: ReceiptPreviewRequest,
) => {
  return apiFetch<ReceiptPreviewResult>('/receipts/preview', {
    method: 'POST',
    token,
    body: payload,
  })
}

export const createReceipt = async (token: string, payload: ReceiptCreateRequest) => {
  return apiFetch<ReceiptDto>('/receipts', {
    method: 'POST',
    token,
    body: payload,
  })
}

export const getReceipt = async (token: string, receiptId: string) => {
  return apiFetch<ReceiptDto>(`/receipts/${receiptId}`, { token })
}

export const listReceipts = async (params: {
  token: string
  sellerTaxCode?: string
  customerTaxCode?: string
  status?: string
  allocationStatus?: string
  documentNo?: string
  from?: string
  to?: string
  amountMin?: number
  amountMax?: number
  method?: string
  allocationPriority?: string
  reminderEnabled?: boolean
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
  if (params.allocationStatus) query.append('allocationStatus', params.allocationStatus)
  if (params.documentNo) query.append('documentNo', params.documentNo)
  if (params.from) query.append('from', params.from)
  if (params.to) query.append('to', params.to)
  if (typeof params.amountMin === 'number') query.append('amountMin', String(params.amountMin))
  if (typeof params.amountMax === 'number') query.append('amountMax', String(params.amountMax))
  if (params.method) query.append('method', params.method)
  if (params.allocationPriority) query.append('allocationPriority', params.allocationPriority)
  if (typeof params.reminderEnabled === 'boolean') {
    query.append('reminderEnabled', String(params.reminderEnabled))
  }

  return apiFetch<PagedResult<ReceiptListItem>>(`/receipts?${query.toString()}`, {
    token: params.token,
  })
}

export type ReceiptVoidResult = {
  reversedAmount: number
  reversedAllocations: number
}

export type ReceiptBulkApproveItem = {
  receiptId: string
  version: number
  selectedTargets?: ReceiptTargetRef[] | null
  overridePeriodLock?: boolean
  overrideReason?: string | null
}

export type ReceiptBulkApproveItemResult = {
  receiptId: string
  result: string
  preview?: ReceiptPreviewResult | null
  errorCode?: string | null
  errorMessage?: string | null
}

export type ReceiptBulkApproveResult = {
  total: number
  approved: number
  failed: number
  items: ReceiptBulkApproveItemResult[]
}

export const approveReceipt = async (
  token: string,
  receiptId: string,
  payload: {
    selectedTargets?: ReceiptTargetRef[]
    version: number
    overridePeriodLock?: boolean
    overrideReason?: string
  },
) => {
  return apiFetch<ReceiptPreviewResult>(`/receipts/${receiptId}/approve`, {
    method: 'POST',
    token,
    body: {
      selectedTargets: payload.selectedTargets ?? null,
      version: payload.version,
      override_period_lock: payload.overridePeriodLock ?? false,
      override_reason: payload.overrideReason ?? null,
    },
  })
}

export const updateReceiptDraft = async (
  token: string,
  receiptId: string,
  payload: ReceiptDraftUpdateRequest,
) => {
  return apiFetch<ReceiptDto>(`/receipts/${receiptId}/draft`, {
    method: 'PUT',
    token,
    body: {
      receiptNo: payload.receiptNo ?? null,
      receiptDate: payload.receiptDate,
      amount: payload.amount,
      allocationMode: payload.allocationMode,
      appliedPeriodStart: payload.appliedPeriodStart ?? null,
      method: payload.method ?? null,
      description: payload.description ?? null,
      allocationPriority: payload.allocationPriority ?? null,
      selectedTargets: payload.selectedTargets ?? null,
      version: payload.version,
    },
  })
}

export const approveReceiptsBulk = async (
  token: string,
  payload: {
    items: ReceiptBulkApproveItem[]
    continueOnError?: boolean
  },
) => {
  return apiFetch<ReceiptBulkApproveResult>('/receipts/approve-bulk', {
    method: 'POST',
    token,
    body: {
      items: payload.items.map((item) => ({
        receipt_id: item.receiptId,
        version: item.version,
        selected_targets: item.selectedTargets ?? null,
        override_period_lock: item.overridePeriodLock ?? false,
        override_reason: item.overrideReason ?? null,
      })),
      continue_on_error: payload.continueOnError ?? true,
    },
  })
}

export const voidReceipt = async (
  token: string,
  receiptId: string,
  payload: {
    reason: string
    version: number
    overridePeriodLock?: boolean
    overrideReason?: string
  },
) => {
  return apiFetch<ReceiptVoidResult>(`/receipts/${receiptId}/void`, {
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

export const unvoidReceipt = async (
  token: string,
  receiptId: string,
  payload: {
    version: number
    overridePeriodLock?: boolean
    overrideReason?: string
  },
) => {
  return apiFetch<ReceiptDto>(`/receipts/${receiptId}/unvoid`, {
    method: 'POST',
    token,
    body: {
      version: payload.version,
      override_period_lock: payload.overridePeriodLock ?? false,
      override_reason: payload.overrideReason ?? null,
    },
  })
}

export const fetchReceiptAllocations = async (token: string, receiptId: string) => {
  return apiFetch<ReceiptAllocationDetail[]>(`/receipts/${receiptId}/allocations`, { token })
}

export const fetchReceiptOpenItems = async (params: {
  token: string
  sellerTaxCode: string
  customerTaxCode: string
}) => {
  const query = new URLSearchParams({
    sellerTaxCode: params.sellerTaxCode,
    customerTaxCode: params.customerTaxCode,
  })
  return apiFetch<ReceiptOpenItem[]>(`/receipts/open-items?${query.toString()}`, {
    token: params.token,
  })
}

export const updateReceiptReminder = async (
  token: string,
  receiptId: string,
  disabled: boolean,
) => {
  return apiFetch<void>(`/receipts/${receiptId}/reminder`, {
    method: 'POST',
    token,
    body: { disabled },
  })
}
