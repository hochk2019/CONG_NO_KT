import { describe, expect, it } from 'vitest'
import { validatePasswordPolicy } from '../passwordPolicy'

describe('validatePasswordPolicy', () => {
  it('returns null for valid password', () => {
    expect(validatePasswordPolicy('StrongPass123')).toBeNull()
  })

  it('returns validation message for weak password', () => {
    expect(validatePasswordPolicy('weak')).toContain('ít nhất 8 ký tự')
    expect(validatePasswordPolicy('lowercase123')).toContain('1 chữ hoa')
    expect(validatePasswordPolicy('UPPERCASE123')).toContain('1 chữ thường')
    expect(validatePasswordPolicy('NoNumberPass')).toContain('1 chữ số')
  })
})
