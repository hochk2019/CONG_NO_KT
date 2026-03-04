import { ApiError } from '../../api/client'

const RECEIPT_APPROVED_PATTERN = /batch receipts are approved/i
const INVOICE_ALLOCATED_PATTERN = /batch invoices are allocated/i
const ADVANCE_ALLOCATED_PATTERN = /batch advances are allocated/i
const LOCKED_PERIOD_PATTERN = /period is locked for rollback:\s*(.+?)\.?$/i
const STRUCTURED_ROLLBACK_CODE = 'IMPORT_ROLLBACK_BLOCKED'

type RollbackErrorPayload = {
  code?: string
  reason?: string
  data?: {
    approvedReceiptCount?: number
    approvedReceiptIds?: string[]
    receiptAllocationCount?: number
    invoiceAllocationCount?: number
    advanceAllocationCount?: number
    receiptIds?: string[]
    invoiceIds?: string[]
    advanceIds?: string[]
    lockedPeriods?: string[]
    action?: string
  }
  extensions?: {
    code?: string
    reason?: string
    data?: RollbackErrorPayload['data']
  }
}

const extractIds = (ids?: string[]) => {
  if (!ids || ids.length === 0) return ''
  const top = ids.slice(0, 5)
  return top.join(', ')
}

const resolveStructuredRollbackMessage = (payload?: RollbackErrorPayload | null) => {
  if (!payload) return null
  const code = payload.code ?? payload.extensions?.code
  if (code !== STRUCTURED_ROLLBACK_CODE) return null
  const reason = payload.reason ?? payload.extensions?.reason
  const data = payload.data ?? payload.extensions?.data
  const action = data?.action ?? 'Hãy xử lý các chứng từ liên quan trước rồi hoàn tác lại.'

  if (reason === 'RECEIPTS_APPROVED') {
    const ids = extractIds(data?.approvedReceiptIds)
    const count = data?.approvedReceiptCount ?? data?.approvedReceiptIds?.length ?? 0
    const suffix = ids ? ` (${count} phiếu, ví dụ: ${ids})` : ''
    return `Không thể hoàn tác: lô có phiếu thu đã duyệt${suffix}. ${action}`
  }

  if (reason === 'RECEIPTS_ALLOCATED') {
    const receiptIds = extractIds(data?.receiptIds)
    const invoiceIds = extractIds(data?.invoiceIds)
    const advanceIds = extractIds(data?.advanceIds)
    const related = [
      receiptIds ? `phiếu thu: ${receiptIds}` : '',
      invoiceIds ? `hóa đơn: ${invoiceIds}` : '',
      advanceIds ? `trả hộ: ${advanceIds}` : '',
    ]
      .filter(Boolean)
      .join(' | ')
    return `Không thể hoàn tác: lô có phiếu thu đã phân bổ${related ? ` (${related})` : ''}. ${action}`
  }

  if (reason === 'INVOICES_ALLOCATED') {
    const invoiceIds = extractIds(data?.invoiceIds)
    const receiptIds = extractIds(data?.receiptIds)
    const related = [invoiceIds ? `hóa đơn: ${invoiceIds}` : '', receiptIds ? `phiếu thu: ${receiptIds}` : '']
      .filter(Boolean)
      .join(' | ')
    return `Không thể hoàn tác: lô có hóa đơn đã được phân bổ${related ? ` (${related})` : ''}. ${action}`
  }

  if (reason === 'ADVANCES_ALLOCATED') {
    const advanceIds = extractIds(data?.advanceIds)
    const receiptIds = extractIds(data?.receiptIds)
    const related = [advanceIds ? `trả hộ: ${advanceIds}` : '', receiptIds ? `phiếu thu: ${receiptIds}` : '']
      .filter(Boolean)
      .join(' | ')
    return `Không thể hoàn tác: lô có khoản trả hộ đã được phân bổ${related ? ` (${related})` : ''}. ${action}`
  }

  if (reason === 'PERIOD_LOCKED') {
    const periods = data?.lockedPeriods?.join(', ')
    return `Không thể hoàn tác vì kỳ đã khóa${periods ? ` (${periods})` : ''}. Bật "Vượt khóa kỳ" và nhập lý do nếu cần.`
  }

  return null
}

export const formatRollbackErrorMessage = (
  error: unknown,
  fallback = 'Hoàn tác thất bại.',
) => {
  if (!(error instanceof ApiError)) {
    return fallback
  }

  const message = (error.message ?? '').trim()
  if (!message) {
    return fallback
  }

  const structuredMessage = resolveStructuredRollbackMessage(error.payload as RollbackErrorPayload | undefined)
  if (structuredMessage) {
    return structuredMessage
  }

  if (RECEIPT_APPROVED_PATTERN.test(message)) {
    return 'Không thể hoàn tác: lô có phiếu thu đã duyệt. Hãy hủy duyệt các phiếu thu liên quan trước.'
  }

  if (INVOICE_ALLOCATED_PATTERN.test(message)) {
    return 'Không thể hoàn tác: lô có hóa đơn đã được phân bổ. Hãy hủy các phân bổ/phiếu thu liên quan trước.'
  }

  if (ADVANCE_ALLOCATED_PATTERN.test(message)) {
    return 'Không thể hoàn tác: lô có khoản trả hộ đã được phân bổ. Hãy hủy các phân bổ/phiếu thu liên quan trước.'
  }

  const lockedPeriod = message.match(LOCKED_PERIOD_PATTERN)
  if (lockedPeriod) {
    const periodText = lockedPeriod[1].trim()
    return `Không thể hoàn tác vì kỳ đã khóa (${periodText}). Bật "Vượt khóa kỳ" và nhập lý do nếu cần.`
  }

  return message
}
