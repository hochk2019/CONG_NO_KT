import { DEFAULT_PAGE_SIZE, PAGE_SIZE_STORAGE_KEY } from './constants'

export const formatInputDate = (date: Date) => {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

export const resolveQuickRange = (value: string) => {
  if (!value) return null
  const today = new Date()
  const year = today.getFullYear()
  const month = today.getMonth()

  if (value === 'this_month') {
    const start = new Date(year, month, 1)
    const end = new Date(year, month + 1, 0)
    return { from: formatInputDate(start), to: formatInputDate(end) }
  }

  if (value === 'last_month') {
    const start = new Date(year, month - 1, 1)
    const end = new Date(year, month, 0)
    return { from: formatInputDate(start), to: formatInputDate(end) }
  }

  const quarterStartMonth = Math.floor(month / 3) * 3
  if (value === 'this_quarter') {
    const start = new Date(year, quarterStartMonth, 1)
    const end = new Date(year, quarterStartMonth + 3, 0)
    return { from: formatInputDate(start), to: formatInputDate(end) }
  }

  if (value === 'last_quarter') {
    const lastQuarterMonth = quarterStartMonth - 3
    const start = new Date(year, lastQuarterMonth, 1)
    const end = new Date(year, lastQuarterMonth + 3, 0)
    return { from: formatInputDate(start), to: formatInputDate(end) }
  }

  return null
}

export const applyQuickRange = (
  value: string,
  setFrom: (next: string) => void,
  setTo: (next: string) => void,
  setQuickRange: (next: string) => void,
) => {
  setQuickRange(value)
  const range = resolveQuickRange(value)
  if (!range) {
    setFrom('')
    setTo('')
    return
  }
  setFrom(range.from)
  setTo(range.to)
}

export const shortId = (value: string) => (value.length > 8 ? value.slice(0, 8) : value)

export const getStoredPageSize = () => {
  if (typeof window === 'undefined') return DEFAULT_PAGE_SIZE
  const raw = window.localStorage.getItem(PAGE_SIZE_STORAGE_KEY)
  const parsed = Number(raw)
  return Number.isFinite(parsed) && parsed > 0 ? parsed : DEFAULT_PAGE_SIZE
}

export const storePageSize = (value: number) => {
  if (typeof window === 'undefined') return
  window.localStorage.setItem(PAGE_SIZE_STORAGE_KEY, String(value))
}

export const getStoredFilter = (key: string) => {
  if (typeof window === 'undefined') return ''
  return window.localStorage.getItem(key) ?? ''
}

export const storeFilter = (key: string, value: string) => {
  if (typeof window === 'undefined') return
  if (!value) {
    window.localStorage.removeItem(key)
  } else {
    window.localStorage.setItem(key, value)
  }
}

export const renderSellerLabel = (taxCode: string, shortName?: string | null) => (
  <div className="stacked-text">
    <span>{taxCode}</span>
    {shortName ? <span className="muted">({shortName})</span> : null}
  </div>
)
