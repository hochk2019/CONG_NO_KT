import { apiFetch } from './client'

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
