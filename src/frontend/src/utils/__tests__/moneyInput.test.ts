import { describe, expect, it } from 'vitest'
import { formatMoneyInput, normalizeMoneyInput } from '../moneyInput'

describe('moneyInput utils', () => {
  it('normalizes grouped integer input into raw digits', () => {
    expect(normalizeMoneyInput('1.000.000')).toBe('1000000')
  })

  it('normalizes vi-VN decimal input into raw decimal value', () => {
    expect(normalizeMoneyInput('1.234,50')).toBe('1234.50')
  })

  it('keeps a trailing decimal separator so the next digit can be typed', () => {
    expect(normalizeMoneyInput('1.234,')).toBe('1234.')
  })

  it('formats raw digits with thousand separators', () => {
    expect(formatMoneyInput('1000000')).toBe('1.000.000')
  })

  it('formats raw decimals with comma separator', () => {
    expect(formatMoneyInput('1234.5')).toBe('1.234,5')
  })
})
