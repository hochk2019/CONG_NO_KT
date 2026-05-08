import type { CustomerInvoiceRef, CustomerReceiptRef } from './customers'
import type { PagedResult } from './types'
import { apiFetch } from './client'

export type InvoiceListItem = {
  id: string
  invoiceNo: string
  issueDate: string
  totalAmount: number
  outstandingAmount: number
  status: string
  version: number
  customerTaxCode: string
  customerName: string
  sellerTaxCode: string
  sellerShortName?: string | null
  receiptRefs: CustomerReceiptRef[]
  reductionInvoiceRefs: CustomerInvoiceRef[]
  reducedInvoiceRefs: CustomerInvoiceRef[]
}

export type InvoiceListRequest = {
  status?: string
  search?: string
  documentNo?: string
  receiptNo?: string
  from?: string
  to?: string
  page?: number
  pageSize?: number
}

export const listInvoices = async (token: string, params: InvoiceListRequest = {}) => {
  const query = new URLSearchParams()

  if (params.status) query.set('status', params.status)
  if (params.search) query.set('search', params.search)
  if (params.documentNo) query.set('documentNo', params.documentNo)
  if (params.receiptNo) query.set('receiptNo', params.receiptNo)
  if (params.from) query.set('from', params.from)
  if (params.to) query.set('to', params.to)
  if (params.page) query.set('page', String(params.page))
  if (params.pageSize) query.set('pageSize', String(params.pageSize))

  const suffix = query.toString()
  return apiFetch<PagedResult<InvoiceListItem>>(suffix ? `/invoices?${suffix}` : '/invoices', {
    token,
  })
}

export type InvoiceVoidResult = {
  id: string
  status: string
  version: number
  outstandingAmount: number
  replacementInvoiceId?: string | null
  heldCreditAmount: number
  heldCreditCount: number
  restoredHeldCreditAmount: number
  restoredHeldCreditCount: number
}

export const voidInvoice = async (
  token: string,
  invoiceId: string,
  payload: {
    reason: string
    force?: boolean
    version: number
  },
) => {
  return apiFetch<InvoiceVoidResult>(`/invoices/${invoiceId}/void`, {
    method: 'POST',
    token,
    body: {
      reason: payload.reason,
      force: payload.force ?? false,
      version: payload.version,
    },
  })
}
