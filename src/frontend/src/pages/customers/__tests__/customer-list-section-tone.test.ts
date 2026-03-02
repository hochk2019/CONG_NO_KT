import { describe, expect, it } from 'vitest'
import {
  CUSTOMER_DEBT_DANGER_THRESHOLD,
  CUSTOMER_DEBT_WARNING_THRESHOLD,
  getDebtToneClass,
} from '../customerDebtTone'

describe('customer debt tone thresholds', () => {
  it('returns clear when balance is zero or negative', () => {
    expect(getDebtToneClass(0)).toBe('debt-value--clear')
    expect(getDebtToneClass(-1)).toBe('debt-value--clear')
  })

  it('returns normal when balance is positive but below warning threshold', () => {
    expect(getDebtToneClass(1)).toBe('debt-value--normal')
    expect(getDebtToneClass(CUSTOMER_DEBT_WARNING_THRESHOLD - 1)).toBe('debt-value--normal')
  })

  it('returns medium when balance is at warning threshold and below danger threshold', () => {
    expect(getDebtToneClass(CUSTOMER_DEBT_WARNING_THRESHOLD)).toBe('debt-value--medium')
    expect(getDebtToneClass(CUSTOMER_DEBT_DANGER_THRESHOLD - 1)).toBe('debt-value--medium')
  })

  it('returns high when balance is at or above danger threshold', () => {
    expect(getDebtToneClass(CUSTOMER_DEBT_DANGER_THRESHOLD)).toBe('debt-value--high')
    expect(getDebtToneClass(CUSTOMER_DEBT_DANGER_THRESHOLD + 1)).toBe('debt-value--high')
  })
})
