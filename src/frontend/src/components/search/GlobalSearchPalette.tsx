import { useEffect, useMemo, useState } from 'react'
import type {
  GlobalSearchCustomerItem,
  GlobalSearchInvoiceItem,
  GlobalSearchReceiptItem,
  GlobalSearchResult,
} from '../../api/search'
import { fetchGlobalSearch } from '../../api/search'
import { ApiError } from '../../api/client'
import { formatMoney } from '../../utils/format'

type GlobalSearchPaletteProps = {
  open: boolean
  token: string
  onClose: () => void
  onNavigate: (to: string) => void
}

type PaletteEntry = {
  key: string
  label: string
  meta: string
  to: string
  kind: 'customer' | 'invoice' | 'receipt'
}

const MIN_QUERY_LENGTH = 2

const normalizeQuery = (value: string) => value.trim()

const buildCustomerEntry = (item: GlobalSearchCustomerItem): PaletteEntry => ({
  key: `customer-${item.taxCode}`,
  label: item.name,
  meta: `MST ${item.taxCode}`,
  to: `/customers?taxCode=${encodeURIComponent(item.taxCode)}`,
  kind: 'customer',
})

const buildInvoiceEntry = (item: GlobalSearchInvoiceItem): PaletteEntry => ({
  key: `invoice-${item.id}`,
  label: item.invoiceNo,
  meta: `${item.customerName} • Còn nợ ${formatMoney(item.outstandingAmount)}`,
  to: `/customers?taxCode=${encodeURIComponent(item.customerTaxCode)}&tab=invoices&doc=${encodeURIComponent(
    item.invoiceNo,
  )}`,
  kind: 'invoice',
})

const buildReceiptEntry = (item: GlobalSearchReceiptItem): PaletteEntry => ({
  key: `receipt-${item.id}`,
  label: item.receiptNo?.trim() || item.id,
  meta: `${item.customerName} • Số tiền ${formatMoney(item.amount)}`,
  to: `/customers?taxCode=${encodeURIComponent(item.customerTaxCode)}&tab=receipts&doc=${encodeURIComponent(
    item.receiptNo?.trim() || item.id,
  )}`,
  kind: 'receipt',
})

export default function GlobalSearchPalette({ open, token, onClose, onNavigate }: GlobalSearchPaletteProps) {
  const [query, setQuery] = useState('')
  const [result, setResult] = useState<GlobalSearchResult | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [activeIndex, setActiveIndex] = useState(-1)

  const entries = useMemo(() => {
    if (!result) return []
    return [
      ...result.customers.map(buildCustomerEntry),
      ...result.invoices.map(buildInvoiceEntry),
      ...result.receipts.map(buildReceiptEntry),
    ]
  }, [result])

  useEffect(() => {
    if (!open) return
    setQuery('')
    setResult(null)
    setError(null)
    setLoading(false)
    setActiveIndex(-1)
  }, [open])

  useEffect(() => {
    if (!open) return

    const normalized = normalizeQuery(query)
    if (normalized.length < MIN_QUERY_LENGTH) {
      setResult(null)
      setError(null)
      setLoading(false)
      setActiveIndex(-1)
      return
    }

    const controller = new AbortController()
    const timeoutId = window.setTimeout(async () => {
      try {
        setLoading(true)
        setError(null)
        const searchResult = await fetchGlobalSearch({
          token,
          query: normalized,
          top: 6,
          signal: controller.signal,
        })
        setResult(searchResult)
      } catch (err) {
        if (controller.signal.aborted) return
        if (err instanceof ApiError) {
          setError(err.message)
        } else {
          setError('Không thể tải kết quả tìm kiếm.')
        }
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      }
    }, 250)

    return () => {
      controller.abort()
      window.clearTimeout(timeoutId)
    }
  }, [open, query, token])

  useEffect(() => {
    if (!open) return
    if (entries.length === 0) {
      setActiveIndex(-1)
      return
    }
    setActiveIndex(0)
  }, [entries, open])

  useEffect(() => {
    if (!open) return

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault()
        onClose()
        return
      }

      if (entries.length === 0) return

      if (event.key === 'ArrowDown') {
        event.preventDefault()
        setActiveIndex((prev) => (prev + 1) % entries.length)
        return
      }

      if (event.key === 'ArrowUp') {
        event.preventDefault()
        setActiveIndex((prev) => (prev <= 0 ? entries.length - 1 : prev - 1))
        return
      }

      if (event.key === 'Enter' && activeIndex >= 0 && activeIndex < entries.length) {
        event.preventDefault()
        const item = entries[activeIndex]
        onNavigate(item.to)
        onClose()
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [activeIndex, entries, onClose, onNavigate, open])

  if (!open) return null

  return (
    <div className="modal-backdrop global-search-backdrop">
      <button
        className="modal-scrim"
        type="button"
        aria-label="Đóng tìm kiếm nhanh"
        onClick={onClose}
      />
      <div className="modal global-search-modal" role="dialog" aria-modal="true" aria-label="Tìm kiếm nhanh">
        <div className="global-search-header">
          <h3>Tìm kiếm nhanh</h3>
          <p className="muted">Nhập từ khóa để tìm khách hàng, hóa đơn hoặc phiếu thu.</p>
        </div>

        <label className="field global-search-field">
          <span>Từ khóa</span>
          <input
            autoFocus
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="VD: MST, số hóa đơn, số phiếu thu..."
          />
          <span className="muted">Phím tắt: Ctrl/Cmd + K, điều hướng bằng ↑ ↓, Enter để mở.</span>
        </label>

        <div className="global-search-results" role="listbox" aria-label="Kết quả tìm kiếm nhanh">
          {normalizeQuery(query).length < MIN_QUERY_LENGTH && (
            <div className="global-search-empty">Nhập ít nhất 2 ký tự để bắt đầu tìm kiếm.</div>
          )}
          {loading && <div className="global-search-empty">Đang tìm...</div>}
          {error && <div className="alert alert--error">{error}</div>}
          {!loading && !error && normalizeQuery(query).length >= MIN_QUERY_LENGTH && entries.length === 0 && (
            <div className="global-search-empty">Không có kết quả phù hợp.</div>
          )}
          {!loading &&
            !error &&
            entries.map((item, index) => (
              <button
                key={item.key}
                type="button"
                role="option"
                aria-selected={index === activeIndex}
                className={`global-search-item${index === activeIndex ? ' global-search-item--active' : ''}`}
                onMouseEnter={() => setActiveIndex(index)}
                onClick={() => {
                  onNavigate(item.to)
                  onClose()
                }}
              >
                <span className="global-search-item__type">{item.kind}</span>
                <span className="global-search-item__main">
                  <strong>{item.label}</strong>
                  <span className="muted">{item.meta}</span>
                </span>
              </button>
            ))}
        </div>
      </div>
    </div>
  )
}
