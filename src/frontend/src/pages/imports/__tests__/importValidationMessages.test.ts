import { describe, expect, it } from 'vitest'
import { formatValidationMessages } from '../importValidationMessages'

describe('formatValidationMessages', () => {
  it('maps replaced invoice informational code to user-facing preview text', () => {
    expect(formatValidationMessages(['INFO_REPLACED_INVOICE'])).toBe(
      'Hóa đơn thông tin đã bị thay thế, không ghi nhận công nợ',
    )
  })

  it('falls back to raw code for unknown messages', () => {
    expect(formatValidationMessages(['SOME_UNKNOWN_CODE'])).toBe('SOME_UNKNOWN_CODE')
  })
})
