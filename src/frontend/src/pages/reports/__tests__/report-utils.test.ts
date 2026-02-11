import { describe, expect, it } from 'vitest'
import { buildPresetList, toDateInput } from '../reportUtils'

describe('report utils', () => {
  it('builds preset list', () => {
    const presets = buildPresetList()
    expect(presets.length).toBe(4)
  })

  it('formats date input in local time', () => {
    const value = new Date(2025, 0, 5)
    expect(toDateInput(value)).toBe('2025-01-05')
  })
})
