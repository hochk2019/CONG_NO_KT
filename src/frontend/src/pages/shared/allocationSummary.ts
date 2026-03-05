export type AllocationStatusKey = 'ALLOCATED' | 'PARTIAL' | 'UNALLOCATED'

export type AllocationSummaryItem = {
  key: AllocationStatusKey
  label: string
  amount: number
  percent: number
}

export type AllocationSummary = {
  total: number
  items: AllocationSummaryItem[]
}

type AllocationStatusRow = {
  status: string
  amount: number
}

const allocationLabelByKey: Record<AllocationStatusKey, string> = {
  ALLOCATED: 'Đã phân bổ',
  PARTIAL: 'Phân bổ một phần',
  UNALLOCATED: 'Chưa phân bổ',
}

const allocationItemOrder: AllocationStatusKey[] = ['ALLOCATED', 'PARTIAL', 'UNALLOCATED']

const resolveAllocationKey = (status: string): AllocationStatusKey => {
  const key = status.toUpperCase()
  if (key === 'ALLOCATED') return 'ALLOCATED'
  if (key === 'PARTIAL') return 'PARTIAL'
  return 'UNALLOCATED'
}

export const buildAllocationSummary = (rows: AllocationStatusRow[]): AllocationSummary => {
  const bucket: Record<AllocationStatusKey, number> = {
    ALLOCATED: 0,
    PARTIAL: 0,
    UNALLOCATED: 0,
  }

  rows.forEach((row) => {
    bucket[resolveAllocationKey(row.status)] += row.amount
  })

  const total = bucket.ALLOCATED + bucket.PARTIAL + bucket.UNALLOCATED

  return {
    total,
    items: allocationItemOrder.map((key) => {
      const amount = bucket[key]
      return {
        key,
        label: allocationLabelByKey[key],
        amount,
        percent: total > 0 ? Math.round((amount / total) * 1000) / 10 : 0,
      }
    }),
  }
}
