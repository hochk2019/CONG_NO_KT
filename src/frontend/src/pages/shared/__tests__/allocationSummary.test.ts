import { describe, expect, it } from 'vitest'
import { buildAllocationSummary } from '../allocationSummary'

describe('buildAllocationSummary', () => {
  it('builds total and percentages for known statuses', () => {
    const summary = buildAllocationSummary([
      { status: 'ALLOCATED', amount: 600 },
      { status: 'PARTIAL', amount: 300 },
      { status: 'UNALLOCATED', amount: 100 },
    ])

    expect(summary.total).toBe(1000)
    expect(summary.items).toEqual([
      { key: 'ALLOCATED', label: 'Đã phân bổ', amount: 600, percent: 60 },
      { key: 'PARTIAL', label: 'Phân bổ một phần', amount: 300, percent: 30 },
      { key: 'UNALLOCATED', label: 'Chưa phân bổ', amount: 100, percent: 10 },
    ])
  })

  it('maps unknown status to UNALLOCATED bucket', () => {
    const summary = buildAllocationSummary([
      { status: 'unknown', amount: 50 },
      { status: 'ALLOCATED', amount: 50 },
    ])

    expect(summary.total).toBe(100)
    expect(summary.items[2]).toEqual({
      key: 'UNALLOCATED',
      label: 'Chưa phân bổ',
      amount: 50,
      percent: 50,
    })
  })
})
