import { apiFetch } from './client'

export type InvoiceVoidResult = {
  id: string
  status: string
  version: number
  outstandingAmount: number
  replacementInvoiceId?: string | null
}

export const voidInvoice = async (
  token: string,
  invoiceId: string,
  payload: {
    reason: string
    replacementInvoiceId?: string | null
    force?: boolean
    version: number
  },
) => {
  return apiFetch<InvoiceVoidResult>(`/invoices/${invoiceId}/void`, {
    method: 'POST',
    token,
    body: {
      reason: payload.reason,
      replacementInvoiceId: payload.replacementInvoiceId ?? null,
      force: payload.force ?? false,
      version: payload.version,
    },
  })
}
