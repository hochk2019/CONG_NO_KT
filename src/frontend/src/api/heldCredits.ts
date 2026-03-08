import { apiFetch } from './client'

export type HeldCreditApplyResult = {
  heldCreditId: string
  version: number
  status: string
  invoiceId: string
  appliedHeldAmount: number
  appliedGeneralCreditAmount: number
  remainingHeldAmount: number
  invoiceOutstandingAmount: number
}

export type HeldCreditReleaseResult = {
  heldCreditId: string
  version: number
  status: string
  receiptId: string
  releasedAmount: number
  remainingHeldAmount: number
  receiptUnallocatedAmount: number
}

export const applyHeldCredit = async (
  token: string,
  heldCreditId: string,
  payload: {
    invoiceId: string
    useGeneralCreditTopUp?: boolean
    version?: number | null
  },
) => {
  return apiFetch<HeldCreditApplyResult>(`/held-credits/${heldCreditId}/apply`, {
    method: 'POST',
    token,
    body: {
      invoiceId: payload.invoiceId,
      use_general_credit_top_up: payload.useGeneralCreditTopUp ?? false,
      version: payload.version ?? null,
    },
  })
}

export const releaseHeldCredit = async (
  token: string,
  heldCreditId: string,
  payload: {
    version?: number | null
  },
) => {
  return apiFetch<HeldCreditReleaseResult>(`/held-credits/${heldCreditId}/release`, {
    method: 'POST',
    token,
    body: {
      version: payload.version ?? null,
    },
  })
}
