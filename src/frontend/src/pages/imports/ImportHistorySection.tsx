import { useCallback, useEffect, useMemo, useState } from 'react'
import { ApiError } from '../../api/client'
import { cancelImport, listImportBatches, rollbackImport } from '../../api/imports'
import DataTable from '../../components/DataTable'
import { formatDate, formatDateTime } from '../../utils/format'

type ImportHistorySectionProps = {
  token: string
  canStage: boolean
  canCommit: boolean
  importTypeLabels: Record<string, string>
  historyStatusLabels: Record<string, string>
  refreshKey: number
  onResumeBatch: (row: {
    batchId: string
    type: string
    periodFrom?: string | null
    periodTo?: string | null
  }) => void
}

const DEFAULT_PAGE_SIZE = 10
const PAGE_SIZE_STORAGE_KEY = 'pref.table.pageSize'
const IMPORTS_HISTORY_STATUS_KEY = 'pref.imports.historyStatus'
const IMPORTS_HISTORY_SEARCH_KEY = 'pref.imports.historySearch'

const getStoredPageSize = () => {
  if (typeof window === 'undefined') return DEFAULT_PAGE_SIZE
  const raw = window.localStorage.getItem(PAGE_SIZE_STORAGE_KEY)
  const parsed = Number(raw)
  return Number.isFinite(parsed) && parsed > 0 ? parsed : DEFAULT_PAGE_SIZE
}

const storePageSize = (value: number) => {
  if (typeof window === 'undefined') return
  window.localStorage.setItem(PAGE_SIZE_STORAGE_KEY, String(value))
}

const getStoredFilter = (key: string) => {
  if (typeof window === 'undefined') return ''
  return window.localStorage.getItem(key) ?? ''
}

const storeFilter = (key: string, value: string) => {
  if (typeof window === 'undefined') return
  if (!value) {
    window.localStorage.removeItem(key)
  } else {
    window.localStorage.setItem(key, value)
  }
}

export default function ImportHistorySection({
  token,
  canStage,
  canCommit,
  importTypeLabels,
  historyStatusLabels,
  refreshKey,
  onResumeBatch,
}: ImportHistorySectionProps) {
  const [rows, setRows] = useState<
    {
      batchId: string
      type: string
      status: string
      fileName?: string | null
      periodFrom?: string | null
      periodTo?: string | null
      createdAt: string
      createdBy?: string | null
      committedAt?: string | null
      cancelledAt?: string | null
      cancelledBy?: string | null
      cancelReason?: string | null
      summary: {
        insertedInvoices: number
        insertedAdvances: number
        insertedReceipts: number
      }
    }[]
  >([])
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(() => getStoredPageSize())
  const [total, setTotal] = useState(0)
  const [type, setType] = useState('')
  const [status, setStatus] = useState(() => getStoredFilter(IMPORTS_HISTORY_STATUS_KEY))
  const [search, setSearch] = useState(() => getStoredFilter(IMPORTS_HISTORY_SEARCH_KEY))
  const [searchApplied, setSearchApplied] = useState(() => getStoredFilter(IMPORTS_HISTORY_SEARCH_KEY))
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [reloadTick, setReloadTick] = useState(0)

  useEffect(() => {
    const trimmed = search.trim()
    const timer = window.setTimeout(() => {
      setSearchApplied(trimmed)
      storeFilter(IMPORTS_HISTORY_SEARCH_KEY, trimmed)
      setPage(1)
    }, 400)
    return () => window.clearTimeout(timer)
  }, [search])

  useEffect(() => {
    if (!token) return
    let isActive = true

    const load = async () => {
      setLoading(true)
      setError(null)
      try {
        const result = await listImportBatches({
          token,
          type: type || undefined,
          status: status || undefined,
          search: searchApplied || undefined,
          page,
          pageSize,
        })
        if (!isActive) return
        setRows(result.items)
        setTotal(result.total)
      } catch (err) {
        if (!isActive) return
        if (err instanceof ApiError) {
          setError(err.message)
        } else {
          setError('Không tải được lịch sử nhập liệu.')
        }
      } finally {
        if (isActive) {
          setLoading(false)
        }
      }
    }

    load()
    return () => {
      isActive = false
    }
  }, [token, page, pageSize, type, status, searchApplied, refreshKey, reloadTick])

  const handleRollback = useCallback(
    async (batchId: string) => {
      if (!token || !canCommit) return
      if (!window.confirm(`Hoàn tác lô nhập ${batchId}?`)) return
      setLoading(true)
      setError(null)
      try {
        await rollbackImport({ token, batchId })
        setPage(1)
        setReloadTick((value) => value + 1)
      } catch (err) {
        if (err instanceof ApiError) {
          setError(err.message)
        } else {
          setError('Hoàn tác thất bại.')
        }
      } finally {
        setLoading(false)
      }
    },
    [token, canCommit],
  )

  const handleCancel = useCallback(
    async (batchId: string) => {
      if (!token || !canStage) return
      const reason = window.prompt('Nhập lý do hủy lô nhập liệu:', '')
      if (reason === null) return
      if (!reason.trim()) {
        setError('Vui lòng nhập lý do hủy lô.')
        return
      }
      setLoading(true)
      setError(null)
      try {
        await cancelImport({ token, batchId, reason: reason.trim() })
        setPage(1)
        setReloadTick((value) => value + 1)
      } catch (err) {
        if (err instanceof ApiError) {
          setError(err.message)
        } else {
          setError('Hủy lô thất bại.')
        }
      } finally {
        setLoading(false)
      }
    },
    [token, canStage],
  )

  const columns = useMemo(
    () => [
      {
        key: 'type',
        label: 'Loại',
        render: (row: { type: string }) => importTypeLabels[row.type] ?? row.type,
      },
      {
        key: 'status',
        label: 'Trạng thái',
        render: (row: { status: string }) => {
          const normalized = row.status.toUpperCase()
          const className =
            normalized === 'COMMITTED'
              ? 'pill pill-ok'
              : normalized === 'STAGING'
                ? 'pill pill-warn'
                : 'pill pill-info'
          return <span className={className}>{historyStatusLabels[normalized] ?? row.status}</span>
        },
      },
      {
        key: 'period',
        label: 'Kỳ',
        render: (row: { periodFrom?: string | null; periodTo?: string | null }) => {
          if (!row.periodFrom && !row.periodTo) return '-'
          const from = row.periodFrom ? formatDate(row.periodFrom) : '-'
          const to = row.periodTo ? formatDate(row.periodTo) : '-'
          return `${from} - ${to}`
        },
      },
      {
        key: 'fileName',
        label: 'File',
        render: (row: { fileName?: string | null }) => row.fileName ?? '-',
      },
      {
        key: 'createdAt',
        label: 'Tạo lúc',
        render: (row: { createdAt: string }) => formatDateTime(row.createdAt),
      },
      {
        key: 'createdBy',
        label: 'Người nhập',
        render: (row: { createdBy?: string | null }) => row.createdBy ?? '-',
      },
      {
        key: 'cancelInfo',
        label: 'Hủy',
        render: (row: {
          status: string
          cancelledAt?: string | null
          cancelledBy?: string | null
          cancelReason?: string | null
        }) => {
          if (row.status.toUpperCase() !== 'CANCELLED') {
            return <span className="muted">-</span>
          }
          const reason = row.cancelReason?.trim() || 'Không có lý do'
          return (
            <div>
              <div>{reason}</div>
              <span className="muted">
                {row.cancelledBy ?? '-'}
                {row.cancelledAt ? ` • ${formatDateTime(row.cancelledAt)}` : ''}
              </span>
            </div>
          )
        },
      },
      {
        key: 'summary',
        label: 'Tổng hợp',
        render: (row: {
          summary: { insertedInvoices: number; insertedAdvances: number; insertedReceipts: number }
        }) =>
          `I:${row.summary.insertedInvoices} A:${row.summary.insertedAdvances} R:${row.summary.insertedReceipts}`,
      },
      {
        key: 'actions',
        label: 'Thao tác',
        render: (row: {
          batchId: string
          status: string
          type: string
          periodFrom?: string | null
          periodTo?: string | null
        }) => {
          const normalized = row.status.toUpperCase()
          if (normalized === 'STAGING' && canStage) {
            return (
              <div className="inline-actions">
                <button
                  className="btn btn-ghost"
                  type="button"
                  disabled={loading}
                  onClick={() => onResumeBatch(row)}
                >
                  Tiếp tục
                </button>
                <button
                  className="btn btn-outline-danger"
                  type="button"
                  disabled={loading}
                  onClick={() => handleCancel(row.batchId)}
                >
                  Hủy lô
                </button>
              </div>
            )
          }

          if (normalized === 'COMMITTED' && canCommit) {
            return (
              <button
                className="btn btn-outline-danger"
                type="button"
                disabled={loading}
                onClick={() => handleRollback(row.batchId)}
              >
                Hoàn tác
              </button>
            )
          }

          return <span className="muted">-</span>
        },
      },
    ],
    [canCommit, canStage, handleCancel, handleRollback, historyStatusLabels, importTypeLabels, loading, onResumeBatch],
  )

  return (
    <section className="card" id="history">
      <div className="card-row">
        <div>
          <h3>Lịch sử nhập liệu</h3>
          <p className="muted">Xem lại các lô đã tải, ghi dữ liệu hoặc đã hủy.</p>
        </div>
        {loading && <span className="muted">Đang tải...</span>}
      </div>

      <div className="filters-grid filters-grid--compact">
        <label className="field field-span-full">
          <span>Tìm kiếm</span>
          <input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Mã lô / Tên file / Người nhập / Lý do hủy"
            disabled={loading}
          />
        </label>
        <label className="field">
          <span>Loại dữ liệu</span>
          <select
            value={type}
            onChange={(event) => {
              setType(event.target.value)
              setPage(1)
            }}
            disabled={loading}
          >
            <option value="">Tất cả</option>
            <option value="INVOICE">Hóa đơn</option>
            <option value="ADVANCE">Khoản trả hộ KH</option>
            <option value="RECEIPT">Phiếu thu</option>
          </select>
        </label>
        <label className="field">
          <span>Trạng thái</span>
          <select
            value={status}
            onChange={(event) => {
              const next = event.target.value
              setStatus(next)
              storeFilter(IMPORTS_HISTORY_STATUS_KEY, next)
              setPage(1)
            }}
            disabled={loading}
          >
            <option value="">Tất cả</option>
            <option value="STAGING">Đang chờ</option>
            <option value="COMMITTED">Đã ghi</option>
            <option value="ROLLED_BACK">Đã hoàn tác</option>
            <option value="CANCELLED">Đã hủy</option>
          </select>
        </label>
      </div>

      {error && (
        <div className="alert alert--error" role="alert" aria-live="assertive">
          {error}
        </div>
      )}
      <DataTable
        columns={columns}
        rows={rows}
        getRowKey={(row) => row.batchId}
        emptyMessage={loading ? 'Đang tải...' : 'Chưa có lô nào.'}
        pagination={{
          page,
          pageSize,
          total,
        }}
        onPageChange={setPage}
        onPageSizeChange={(size) => {
          storePageSize(size)
          setPageSize(size)
          setPage(1)
        }}
      />
    </section>
  )
}
