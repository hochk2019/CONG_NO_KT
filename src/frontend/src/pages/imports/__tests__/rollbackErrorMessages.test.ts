import { describe, expect, it } from 'vitest'
import { ApiError } from '../../../api/client'
import { formatRollbackErrorMessage } from '../rollbackErrorMessages'

describe('formatRollbackErrorMessage', () => {
  it('maps approved receipt rollback error to friendly message', () => {
    const error = new ApiError(
      'Rollback blocked: batch receipts are approved. Void receipts first.',
      400,
    )

    expect(formatRollbackErrorMessage(error)).toContain('phiếu thu đã duyệt')
  })

  it('maps allocated invoice rollback error to friendly message', () => {
    const error = new ApiError(
      'Rollback blocked: batch invoices are allocated. Void receipts first.',
      400,
    )

    expect(formatRollbackErrorMessage(error)).toContain('hóa đơn đã được phân bổ')
  })

  it('maps allocated advance rollback error to friendly message', () => {
    const error = new ApiError(
      'Rollback blocked: batch advances are allocated. Void receipts first.',
      400,
    )

    expect(formatRollbackErrorMessage(error)).toContain('khoản trả hộ đã được phân bổ')
  })

  it('maps period lock rollback error to friendly message with period context', () => {
    const error = new ApiError('Period is locked for rollback: 2026-01, 2026-02.', 400)

    const message = formatRollbackErrorMessage(error)
    expect(message).toContain('kỳ đã khóa')
    expect(message).toContain('2026-01, 2026-02')
  })

  it('returns original api error message when no special mapping exists', () => {
    const error = new ApiError('Some other rollback error', 400)
    expect(formatRollbackErrorMessage(error)).toBe('Some other rollback error')
  })

  it('returns fallback for non-api error', () => {
    expect(formatRollbackErrorMessage(new Error('boom'))).toBe('Hoàn tác thất bại.')
  })
})
