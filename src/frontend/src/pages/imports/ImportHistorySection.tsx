import { useCallback, useEffect, useMemo, useState } from 'react'
import { ApiError } from '../../api/client'
import { cancelImport, listImportBatches, rollbackImport } from '../../api/imports'
import DataTable from '../../components/DataTable'
import ActionConfirmModal, { type ActionConfirmPayload } from '../../components/modals/ActionConfirmModal'
import { formatDate, formatDateTime } from '../../utils/format'
import { formatRollbackErrorMessage } from './rollbackErrorMessages'

type ImportHistorySectionProps = {
  token: string
  canStage: boolean
  canCommit: boolean
  importTypeLabels: Record<string, string>
  historyStatusLabels: Record<string, string>
  refreshKey: number
  fixedType?: 'INVOICE' | 'ADVANCE' | 'RECEIPT'
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

type HistoryConfirmState =
  | { action: 'rollback'; batchId: string }
  | { action: 'cancel'; batchId: string }

export default function ImportHistorySection({
  token,
  canStage,
  canCommit,
  importTypeLabels,
  historyStatusLabels,
  refreshKey,
  fixedType,
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
  const [confirmState, setConfirmState] = useState<HistoryConfirmState | null>(null)
  const [confirmLoading, setConfirmLoading] = useState(false)
  const [confirmError, setConfirmError] = useState<string | null>(null)
  const [selectedBatchIds, setSelectedBatchIds] = useState<string[]>([])
  const [bulkConfirmAction, setBulkConfirmAction] = useState<'rollback' | 'cancel' | null>(null)
  const [bulkConfirmLoading, setBulkConfirmLoading] = useState(false)
  const [bulkConfirmError, setBulkConfirmError] = useState<string | null>(null)
  const [actionMessage, setActionMessage] = useState<string | null>(null)

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
          type: (fixedType ?? type) || undefined,
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
  }, [token, page, pageSize, type, status, searchApplied, refreshKey, reloadTick, fixedType])

  useEffect(() => {
    setSelectedBatchIds((prev) => {
      if (prev.length === 0) return prev
      const rowIds = new Set(rows.map((row) => row.batchId))
      return prev.filter((id) => rowIds.has(id))
    })
  }, [rows])

  const handleRollback = useCallback(
    (batchId: string) => {
      if (!token || !canCommit) return
      setActionMessage(null)
      setConfirmError(null)
      setConfirmState({ action: 'rollback', batchId })
    },
    [token, canCommit],
  )

  const handleCancel = useCallback(
    (batchId: string) => {
      if (!token || !canStage) return
      setActionMessage(null)
      setConfirmError(null)
      setConfirmState({ action: 'cancel', batchId })
    },
    [token, canStage],
  )

  const handleConfirmAction = useCallback(
    async (payload: ActionConfirmPayload) => {
      if (!token || !confirmState) return
      setConfirmLoading(true)
      setConfirmError(null)
      setError(null)
      setActionMessage(null)
      setLoading(true)
      try {
        if (confirmState.action === 'rollback') {
          await rollbackImport({ token, batchId: confirmState.batchId })
        } else {
          await cancelImport({ token, batchId: confirmState.batchId, reason: payload.reason })
        }
        setSelectedBatchIds((prev) => prev.filter((id) => id !== confirmState.batchId))
        setConfirmState(null)
        setPage(1)
        setReloadTick((value) => value + 1)
        setActionMessage(
          confirmState.action === 'rollback'
            ? `Đã hoàn tác lô ${confirmState.batchId}.`
            : `Đã hủy lô ${confirmState.batchId}.`,
        )
      } catch (err) {
        if (confirmState.action === 'rollback') {
          setConfirmError(formatRollbackErrorMessage(err))
        } else if (err instanceof ApiError) {
          setConfirmError(err.message)
        } else {
          setConfirmError('Hủy lô thất bại.')
        }
      } finally {
        setLoading(false)
        setConfirmLoading(false)
      }
    },
    [token, confirmState],
  )

  const isRollbackEligible = useCallback(
    (row: { status: string }) => row.status.toUpperCase() === 'COMMITTED' && canCommit,
    [canCommit],
  )
  const isCancelEligible = useCallback(
    (row: { status: string }) => row.status.toUpperCase() === 'STAGING' && canStage,
    [canStage],
  )
  const isActionable = useCallback(
    (row: { status: string }) => isRollbackEligible(row) || isCancelEligible(row),
    [isCancelEligible, isRollbackEligible],
  )

  const selectedRows = rows.filter((row) => selectedBatchIds.includes(row.batchId))
  const selectedRollbackCount = selectedRows.filter((row) => isRollbackEligible(row)).length
  const selectedCancelCount = selectedRows.filter((row) => isCancelEligible(row)).length
  const selectableBatchIds = rows.filter((row) => isActionable(row)).map((row) => row.batchId)

  const handleOpenBulkConfirm = (action: 'rollback' | 'cancel') => {
    if (action === 'rollback' && selectedRollbackCount === 0) return
    if (action === 'cancel' && selectedCancelCount === 0) return
    setBulkConfirmError(null)
    setBulkConfirmAction(action)
    setActionMessage(null)
  }

  const handleBulkConfirm = useCallback(
    async (payload: ActionConfirmPayload) => {
      if (!token || !bulkConfirmAction) return
      const targetRows = selectedRows.filter((row) =>
        bulkConfirmAction === 'rollback' ? isRollbackEligible(row) : isCancelEligible(row),
      )
      if (targetRows.length === 0) return

      setBulkConfirmLoading(true)
      setBulkConfirmError(null)
      setError(null)
      setActionMessage(null)

      let successCount = 0
      const failedIds: string[] = []
      const failedMessages: string[] = []

      for (const row of targetRows) {
        try {
          if (bulkConfirmAction === 'rollback') {
            await rollbackImport({
              token,
              batchId: row.batchId,
              overridePeriodLock: payload.overridePeriodLock,
              overrideReason: payload.overrideReason,
            })
          } else {
            await cancelImport({
              token,
              batchId: row.batchId,
              reason: payload.reason,
            })
          }
          successCount += 1
        } catch (err) {
          failedIds.push(row.batchId)
          if (bulkConfirmAction === 'rollback') {
            failedMessages.push(`${row.batchId}: ${formatRollbackErrorMessage(err)}`)
          } else if (err instanceof ApiError) {
            failedMessages.push(`${row.batchId}: ${err.message}`)
          } else {
            failedMessages.push(`${row.batchId}: Hủy lô thất bại.`)
          }
        }
      }

      if (successCount > 0) {
        const actionLabel = bulkConfirmAction === 'rollback' ? 'hoàn tác' : 'hủy'
        setActionMessage(`Đã ${actionLabel} ${successCount}/${targetRows.length} lô đã chọn.`)
        setPage(1)
        setReloadTick((value) => value + 1)
      }

      if (failedIds.length > 0) {
        setBulkConfirmError(
          `Thất bại ${failedIds.length} lô: ${failedMessages.slice(0, 2).join('; ')}`,
        )
        setSelectedBatchIds(failedIds)
      } else {
        setSelectedBatchIds([])
        setBulkConfirmAction(null)
      }

      setBulkConfirmLoading(false)
    },
    [bulkConfirmAction, isCancelEligible, isRollbackEligible, selectedRows, token],
  )

  const columns = useMemo(
    () => [
      {
        key: 'select',
        label: 'Chọn',
        align: 'center' as const,
        width: '72px',
        render: (row: { batchId: string; status: string }) => {
          if (!isActionable(row)) return <span className="muted">-</span>
          return (
            <input
              type="checkbox"
              checked={selectedBatchIds.includes(row.batchId)}
              onChange={(event) => {
                setSelectedBatchIds((prev) => {
                  if (event.target.checked) {
                    if (prev.includes(row.batchId)) return prev
                    return [...prev, row.batchId]
                  }
                  return prev.filter((id) => id !== row.batchId)
                })
              }}
              aria-label={`Chọn lô ${row.batchId}`}
            />
          )
        },
      },
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
    [
      canCommit,
      canStage,
      handleCancel,
      handleRollback,
      historyStatusLabels,
      importTypeLabels,
      isActionable,
      loading,
      onResumeBatch,
      selectedBatchIds,
    ],
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
        {fixedType ? (
          <label className="field">
            <span>Loại dữ liệu</span>
            <div className="readonly-field">{importTypeLabels[fixedType] ?? fixedType}</div>
          </label>
        ) : (
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
        )}
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
      {actionMessage && (
        <div className="alert alert--success" role="alert" aria-live="polite">
          {actionMessage}
        </div>
      )}
      <div className="filters-actions">
        <span className="muted">
          Đã chọn {selectedBatchIds.length}/{selectableBatchIds.length} lô khả dụng.
        </span>
        <button
          className="btn btn-ghost"
          type="button"
          onClick={() => setSelectedBatchIds(selectableBatchIds)}
          disabled={selectableBatchIds.length === 0 || bulkConfirmLoading}
        >
          Chọn tất cả
        </button>
        <button
          className="btn btn-ghost"
          type="button"
          onClick={() => setSelectedBatchIds([])}
          disabled={selectedBatchIds.length === 0 || bulkConfirmLoading}
        >
          Bỏ chọn
        </button>
        <button
          className="btn btn-outline-danger"
          type="button"
          onClick={() => handleOpenBulkConfirm('rollback')}
          disabled={selectedRollbackCount === 0 || bulkConfirmLoading}
        >
          Hoàn tác đã chọn ({selectedRollbackCount})
        </button>
        <button
          className="btn btn-outline-danger"
          type="button"
          onClick={() => handleOpenBulkConfirm('cancel')}
          disabled={selectedCancelCount === 0 || bulkConfirmLoading}
        >
          Hủy đã chọn ({selectedCancelCount})
        </button>
      </div>
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

      <ActionConfirmModal
        isOpen={Boolean(confirmState)}
        title={confirmState ? `${confirmState.action === 'rollback' ? 'Hoàn tác' : 'Hủy'} lô ${confirmState.batchId}` : ''}
        description={
          confirmState?.action === 'rollback'
            ? `Xác nhận hoàn tác dữ liệu của lô ${confirmState.batchId}.`
            : confirmState
              ? `Nhập lý do hủy lô ${confirmState.batchId}.`
              : undefined
        }
        confirmLabel={confirmState?.action === 'rollback' ? 'Xác nhận hoàn tác' : 'Xác nhận hủy'}
        reasonRequired={confirmState?.action === 'cancel'}
        reasonLabel="Lý do hủy lô"
        reasonPlaceholder="Nhập lý do hủy lô nhập liệu"
        loading={confirmLoading}
        error={confirmError}
        tone="danger"
        onClose={() => {
          if (confirmLoading) return
          setConfirmState(null)
          setConfirmError(null)
        }}
        onConfirm={handleConfirmAction}
      />

      <ActionConfirmModal
        isOpen={Boolean(bulkConfirmAction)}
        title={
          bulkConfirmAction === 'rollback'
            ? 'Hoàn tác các lô đã chọn'
            : 'Hủy các lô đã chọn'
        }
        description={
          bulkConfirmAction === 'rollback'
            ? `Xác nhận hoàn tác ${selectedRollbackCount} lô đã chọn.`
            : `Nhập lý do hủy cho ${selectedCancelCount} lô đã chọn.`
        }
        confirmLabel={
          bulkConfirmAction === 'rollback' ? 'Xác nhận hoàn tác' : 'Xác nhận hủy'
        }
        reasonRequired={bulkConfirmAction === 'cancel'}
        reasonLabel="Lý do hủy lô"
        reasonPlaceholder="Nhập lý do hủy lô nhập liệu"
        showOverrideOption={bulkConfirmAction === 'rollback'}
        overrideReasonPlaceholder="Nhập lý do vượt khóa kỳ"
        loading={bulkConfirmLoading}
        error={bulkConfirmError}
        tone="danger"
        onClose={() => {
          if (bulkConfirmLoading) return
          setBulkConfirmAction(null)
          setBulkConfirmError(null)
        }}
        onConfirm={handleBulkConfirm}
      />
    </section>
  )
}
