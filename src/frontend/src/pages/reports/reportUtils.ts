import type { LookupOption } from '../../api/lookups'

export type ReportPresetConfig = {
  id: string
  label: string
  from: Date
  to: Date
}

export const groupByLabels: Record<string, string> = {
  customer: 'Khách hàng',
  seller: 'Bên bán',
  owner: 'Phụ trách',
  period: 'Kỳ',
}

export const defaultKpiOrder = [
  'totalOutstanding',
  'outstandingInvoice',
  'outstandingAdvance',
  'unallocatedReceipts',
  'overdueAmount',
  'dueSoonAmount',
  'onTimeCustomers',
]

export const quickActionsStorageKey = 'reports.quickActions.open'

export const toDateInput = (value: Date) => {
  const year = value.getFullYear()
  const month = String(value.getMonth() + 1).padStart(2, '0')
  const day = String(value.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

export const buildPresetList = (): ReportPresetConfig[] => {
  const today = new Date()
  return [
    {
      id: 'month',
      label: 'Tháng này',
      from: new Date(today.getFullYear(), today.getMonth(), 1),
      to: today,
    },
    {
      id: 'quarter',
      label: 'Quý này',
      from: new Date(today.getFullYear(), Math.floor(today.getMonth() / 3) * 3, 1),
      to: today,
    },
    {
      id: 'six-months',
      label: '6 tháng',
      from: new Date(today.getFullYear(), today.getMonth() - 5, 1),
      to: today,
    },
    {
      id: 'year',
      label: 'Năm nay',
      from: new Date(today.getFullYear(), 0, 1),
      to: today,
    },
  ]
}

export const resolveOptionLabel = (options: LookupOption[], value: string) => {
  if (!value) return ''
  return options.find((option) => option.value === value)?.label ?? value
}
