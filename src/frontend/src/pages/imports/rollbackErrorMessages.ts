import { ApiError } from '../../api/client'

const RECEIPT_APPROVED_PATTERN = /batch receipts are approved/i
const INVOICE_ALLOCATED_PATTERN = /batch invoices are allocated/i
const ADVANCE_ALLOCATED_PATTERN = /batch advances are allocated/i
const LOCKED_PERIOD_PATTERN = /period is locked for rollback:\s*(.+?)\.?$/i

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
