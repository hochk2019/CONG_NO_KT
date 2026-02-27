import { describe, expect, it } from 'vitest'

import { formatDate } from '../format'

describe('formatDate', () => {
  it('normalizes mm/dd/yyyy values to dd/mm/yyyy', () => {
    expect(formatDate('02/27/2026')).toBe('27/02/2026')
  })

  it('keeps dd/mm/yyyy values in dd/mm/yyyy format', () => {
    expect(formatDate('27/02/2026')).toBe('27/02/2026')
  })

  it('normalizes yyyy/mm/dd values to dd/mm/yyyy', () => {
    expect(formatDate('2026/02/27')).toBe('27/02/2026')
  })
})
