import { describe, expect, it } from 'vitest'
import { toDateInput } from '../dateInput'

describe('toDateInput', () => {
  it('formats date to yyyy-mm-dd in local time', () => {
    expect(toDateInput(new Date(2026, 2, 5))).toBe('2026-03-05')
  })
})
