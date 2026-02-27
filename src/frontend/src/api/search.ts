import { apiFetch } from './client'

export type GlobalSearchCustomerItem = {
  taxCode: string
  name: string
}

export type GlobalSearchInvoiceItem = {
  id: string
  invoiceNo: string
  customerTaxCode: string
  customerName: string
  issueDate: string
  outstandingAmount: number
  status: string
}

export type GlobalSearchReceiptItem = {
  id: string
  receiptNo?: string | null
  customerTaxCode: string
  customerName: string
  receiptDate: string
  amount: number
  status: string
}

export type GlobalSearchResult = {
  query: string
  total: number
  customers: GlobalSearchCustomerItem[]
  invoices: GlobalSearchInvoiceItem[]
  receipts: GlobalSearchReceiptItem[]
}

export const fetchGlobalSearch = async (params: {
  token: string
  query: string
  top?: number
  signal?: AbortSignal
}) => {
  const query = params.query.trim()
  const search = new URLSearchParams({
    q: query,
    top: String(params.top ?? 6),
  })

  return apiFetch<GlobalSearchResult>(`/search/global?${search.toString()}`, {
    token: params.token,
    signal: params.signal,
  })
}
