import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  approveReceipt,
  approveReceiptsBulk,
  fetchReceiptAllocations,
  fetchReceiptOpenItems,
  getReceipt,
  listReceipts,
  unvoidReceipt,
  updateReceiptReminder,
  voidReceipt,
  type ReceiptAllocationDetail,
  type ReceiptListItem,
  type ReceiptOpenItem,
  type ReceiptTargetRef,
} from '../../api/receipts'
import {
  fetchCustomerLookup,
  fetchSellerLookup,
  mapTaxCodeOptions,
  type LookupOption,
} from '../../api/lookups'
import DataTable from '../../components/DataTable'
import LookupInput from '../../components/LookupInput'
import MoneyInput from '../../components/MoneyInput'
import ActionConfirmModal, { type ActionConfirmPayload } from '../../components/modals/ActionConfirmModal'
import { useDebouncedValue } from '../../hooks/useDebouncedValue'
import { formatDate, formatMoney } from '../../utils/format'
import { ApiError } from '../../api/client'
import ReceiptAllocationModal from './ReceiptAllocationModal'
import ReceiptCancelModal from './ReceiptCancelModal'
import ReceiptSurplusQueuePanel from './ReceiptSurplusQueuePanel'
import ReceiptViewAllocationsModal from './ReceiptViewAllocationsModal'
import {
  allocationPriorityLabels,
  allocationStatusLabels,
  methodLabels,
  receiptStatusLabels,
} from './receiptLabels'

const DEFAULT_PAGE_SIZE = 10
const PAGE_SIZE_STORAGE_KEY = 'pref.table.pageSize'
const RECEIPTS_STATUS_KEY = 'pref.receipts.status'
const RECEIPTS_ALLOCATION_KEY = 'pref.receipts.allocationStatus'
const RECEIPTS_DOCUMENT_KEY = 'pref.receipts.documentNo'
const RECEIPTS_DATE_FROM_KEY = 'pref.receipts.dateFrom'
const RECEIPTS_DATE_TO_KEY = 'pref.receipts.dateTo'
const RECEIPTS_AMOUNT_MIN_KEY = 'pref.receipts.amountMin'
const RECEIPTS_AMOUNT_MAX_KEY = 'pref.receipts.amountMax'
const RECEIPTS_METHOD_KEY = 'pref.receipts.method'
const RECEIPTS_PRIORITY_KEY = 'pref.receipts.allocationPriority'
const RECEIPTS_REMINDER_KEY = 'pref.receipts.reminder'
const DEFAULT_REMINDER_FILTER = 'ENABLED'
const ALLOCATION_FILTERS = new Set(['ALLOCATED', 'UNALLOCATED', 'PARTIAL'])

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

const getStoredReminderFilter = () => {
  const stored = getStoredFilter(RECEIPTS_REMINDER_KEY)
  return stored || DEFAULT_REMINDER_FILTER
}

const parseNumber = (value: string) => {
  if (!value) return undefined
  const parsed = Number(value)
  return Number.isFinite(parsed) ? parsed : undefined
}

type ReceiptListSectionProps = {
  token: string
  reloadSignal: number
}

export default function ReceiptListSection({ token, reloadSignal }: ReceiptListSectionProps) {
  const [sellerOptions, setSellerOptions] = useState<LookupOption[]>([])
  const [customerOptions, setCustomerOptions] = useState<LookupOption[]>([])
  const [sellerQuery, setSellerQuery] = useState('')
  const [customerQuery, setCustomerQuery] = useState('')
  const debouncedSellerQuery = useDebouncedValue(sellerQuery, 300)
  const debouncedCustomerQuery = useDebouncedValue(customerQuery, 300)

  const [listRows, setListRows] = useState<ReceiptListItem[]>([])
  const [listPage, setListPage] = useState(1)
  const [listPageSize, setListPageSize] = useState(() => getStoredPageSize())
  const [listTotal, setListTotal] = useState(0)
  const [listAllocationStatus, setListAllocationStatus] = useState(() =>
    getStoredFilter(RECEIPTS_ALLOCATION_KEY),
  )
  const [listStatus, setListStatus] = useState(() => {
    const allocation = getStoredFilter(RECEIPTS_ALLOCATION_KEY)
    return allocation ? '' : getStoredFilter(RECEIPTS_STATUS_KEY)
  })
  const [listCustomer, setListCustomer] = useState('')
  const [listSeller, setListSeller] = useState('')
  const [listDocumentNo, setListDocumentNo] = useState(() => getStoredFilter(RECEIPTS_DOCUMENT_KEY))
  const [listDateFrom, setListDateFrom] = useState(() => getStoredFilter(RECEIPTS_DATE_FROM_KEY))
  const [listDateTo, setListDateTo] = useState(() => getStoredFilter(RECEIPTS_DATE_TO_KEY))
  const [listAmountMin, setListAmountMin] = useState(() => getStoredFilter(RECEIPTS_AMOUNT_MIN_KEY))
  const [listAmountMax, setListAmountMax] = useState(() => getStoredFilter(RECEIPTS_AMOUNT_MAX_KEY))
  const [listMethod, setListMethod] = useState(() => getStoredFilter(RECEIPTS_METHOD_KEY))
  const [listPriority, setListPriority] = useState(() => getStoredFilter(RECEIPTS_PRIORITY_KEY))
  const [listReminder, setListReminder] = useState(() => getStoredReminderFilter())
  const [listLoading, setListLoading] = useState(false)
  const [activeTab, setActiveTab] = useState<'receipts' | 'surplusQueue'>('receipts')
  const [listError, setListError] = useState<string | null>(null)
  const [listMessage, setListMessage] = useState<string | null>(null)
  const [advancedOpen, setAdvancedOpen] = useState(false)
  const [listReloadTick, setListReloadTick] = useState(0)
  const [selectedReceiptIds, setSelectedReceiptIds] = useState<string[]>([])

  const [cancelRow, setCancelRow] = useState<ReceiptListItem | null>(null)
  const [cancelLoading, setCancelLoading] = useState(false)
  const [cancelError, setCancelError] = useState<string | null>(null)
  const [unvoidRow, setUnvoidRow] = useState<ReceiptListItem | null>(null)
  const [unvoidError, setUnvoidError] = useState<string | null>(null)
  const [unvoidLoadingId, setUnvoidLoadingId] = useState<string | null>(null)
  const [bulkApproveOpen, setBulkApproveOpen] = useState(false)
  const [bulkApproveError, setBulkApproveError] = useState<string | null>(null)
  const [bulkApproveLoading, setBulkApproveLoading] = useState(false)

  const [approvalRow, setApprovalRow] = useState<ReceiptListItem | null>(null)
  const [approvalTargets, setApprovalTargets] = useState<ReceiptTargetRef[]>([])
  const [approvalPriority, setApprovalPriority] = useState('ISSUE_DATE')
  const [approvalOpenItems, setApprovalOpenItems] = useState<ReceiptOpenItem[]>([])
  const [approvalLoading, setApprovalLoading] = useState(false)
  const [approvalError, setApprovalError] = useState<string | null>(null)

  const [viewRow, setViewRow] = useState<ReceiptListItem | null>(null)
  const [viewAllocations, setViewAllocations] = useState<ReceiptAllocationDetail[]>([])
  const [viewLoading, setViewLoading] = useState(false)
  const [viewError, setViewError] = useState<string | null>(null)

  const debouncedDocumentNo = useDebouncedValue(listDocumentNo, 300)
  const parsedAmountMin = useMemo(() => parseNumber(listAmountMin), [listAmountMin])
  const parsedAmountMax = useMemo(() => parseNumber(listAmountMax), [listAmountMax])
  const reminderEnabled = useMemo(() => {
    if (listReminder === 'ENABLED') return true
    if (listReminder === 'DISABLED') return false
    return undefined
  }, [listReminder])
  const validationError = useMemo(() => {
    if (listDateFrom && listDateTo && listDateFrom > listDateTo) {
      return 'Ngày chứng từ từ phải nhỏ hơn hoặc bằng ngày chứng từ đến.'
    }
    if (parsedAmountMin !== undefined && parsedAmountMax !== undefined && parsedAmountMin > parsedAmountMax) {
      return 'Số tiền từ phải nhỏ hơn hoặc bằng số tiền đến.'
    }
    return null
  }, [listDateFrom, listDateTo, parsedAmountMin, parsedAmountMax])

  useEffect(() => {
    if (!token || activeTab !== 'receipts') return
    let isActive = true
    const loadSellers = async () => {
      try {
        const result = await fetchSellerLookup({
          token,
          search: debouncedSellerQuery || undefined,
          limit: 200,
        })
        if (!isActive) return
        setSellerOptions(mapTaxCodeOptions(result))
      } catch {
        if (!isActive) return
        setSellerOptions([])
      }
    }
    loadSellers()
    return () => {
      isActive = false
    }
  }, [token, debouncedSellerQuery])

  useEffect(() => {
    if (!token) return
    let isActive = true
    const loadCustomers = async () => {
      try {
        const result = await fetchCustomerLookup({
          token,
          search: debouncedCustomerQuery || undefined,
          limit: 200,
        })
        if (!isActive) return
        setCustomerOptions(mapTaxCodeOptions(result))
      } catch {
        if (!isActive) return
        setCustomerOptions([])
      }
    }
    loadCustomers()
    return () => {
      isActive = false
    }
  }, [token, debouncedCustomerQuery])

  useEffect(() => {
    if (!token) return
    let isActive = true

    const loadList = async () => {
      if (validationError) {
        return
      }
      setListLoading(true)
      setListError(null)
      try {
        const result = await listReceipts({
          token,
          sellerTaxCode: listSeller || undefined,
          customerTaxCode: listCustomer || undefined,
          status: listStatus || undefined,
          allocationStatus: listAllocationStatus || undefined,
          documentNo: debouncedDocumentNo || undefined,
          from: listDateFrom || undefined,
          to: listDateTo || undefined,
          amountMin: parsedAmountMin,
          amountMax: parsedAmountMax,
          method: listMethod || undefined,
          allocationPriority: listPriority || undefined,
          reminderEnabled,
          page: listPage,
          pageSize: listPageSize,
        })
        if (!isActive) return
        setListRows(result.items)
        setListTotal(result.total)
      } catch (err) {
        if (!isActive) return
        if (err instanceof ApiError) {
          setListError(err.message)
        } else {
          setListError('Không tải được danh sách phiếu thu.')
        }
      } finally {
        if (isActive) setListLoading(false)
      }
    }

    loadList()
    return () => {
      isActive = false
    }
  }, [
    activeTab,
    token,
    listPage,
    listPageSize,
    listStatus,
    listAllocationStatus,
    debouncedDocumentNo,
    listDateFrom,
    listDateTo,
    parsedAmountMin,
    parsedAmountMax,
    listMethod,
    listPriority,
    reminderEnabled,
    listCustomer,
    listSeller,
    reloadSignal,
    listReloadTick,
    validationError,
  ])

  useEffect(() => {
    setSelectedReceiptIds((prev) => {
      if (prev.length === 0) return prev
      const rowIds = new Set(listRows.map((row) => row.id))
      return prev.filter((id) => rowIds.has(id))
    })
  }, [listRows])

  const handleToggleReminder = useCallback(
    async (row: ReceiptListItem) => {
      if (!token) return
      setListMessage(null)
      try {
        await updateReceiptReminder(token, row.id, !row.reminderDisabledAt)
        setListRows((prev) =>
          prev.map((item) =>
            item.id === row.id
              ? { ...item, reminderDisabledAt: row.reminderDisabledAt ? null : new Date().toISOString() }
              : item,
          ),
        )
      } catch (err) {
        setListError(err instanceof ApiError ? err.message : 'Không cập nhật được nhắc duyệt.')
      }
    },
    [token],
  )

  const handleOpenApprove = useCallback(
    async (row: ReceiptListItem) => {
      if (!token) return
      setListMessage(null)
      setApprovalLoading(true)
      setApprovalError(null)
      try {
        const detail = await getReceipt(token, row.id)
        const openItems = await fetchReceiptOpenItems({
          token,
          sellerTaxCode: detail.sellerTaxCode,
          customerTaxCode: detail.customerTaxCode,
        })
        setApprovalRow({
          ...row,
          allocationPriority: detail.allocationPriority,
        })
        setApprovalPriority(detail.allocationPriority || 'ISSUE_DATE')
        setApprovalTargets(detail.selectedTargets ?? [])
        setApprovalOpenItems(openItems)
      } catch (err) {
        setApprovalError(err instanceof ApiError ? err.message : 'Không tải được phiếu thu.')
      } finally {
        setApprovalLoading(false)
      }
    },
    [token],
  )

  const handleApprove = async (targets: ReceiptTargetRef[]) => {
    if (!token || !approvalRow) return
    setListMessage(null)
    setApprovalLoading(true)
    setApprovalError(null)
    try {
      await approveReceipt(token, approvalRow.id, {
        selectedTargets: targets,
        version: approvalRow.version,
      })
      setApprovalRow(null)
      setApprovalTargets([])
      setApprovalOpenItems([])
      setListRows((prev) =>
        prev.map((item) =>
          item.id === approvalRow.id ? { ...item, status: 'APPROVED', allocationStatus: 'ALLOCATED' } : item,
        ),
      )
      setSelectedReceiptIds((prev) => prev.filter((id) => id !== approvalRow.id))
    } catch (err) {
      setApprovalError(err instanceof ApiError ? err.message : 'Không duyệt được phiếu thu.')
    } finally {
      setApprovalLoading(false)
    }
  }

  const handleOpenView = useCallback(
    async (row: ReceiptListItem) => {
      if (!token) return
      setListMessage(null)
      setViewRow(row)
      setViewLoading(true)
      setViewError(null)
      try {
        const allocations = await fetchReceiptAllocations(token, row.id)
        setViewAllocations(allocations)
      } catch (err) {
        setViewError(err instanceof ApiError ? err.message : 'Không tải được phân bổ.')
      } finally {
        setViewLoading(false)
      }
    },
    [token],
  )

  const handleUnvoid = useCallback(
    (row: ReceiptListItem) => {
      if (!token || !row.canManage) return
      setListMessage(null)
      setListError(null)
      setUnvoidError(null)
      setUnvoidRow(row)
    },
    [token],
  )

  const handleUnvoidConfirm = useCallback(
    async (payload: ActionConfirmPayload) => {
      if (!token || !unvoidRow) return
      setListMessage(null)
      setUnvoidError(null)
      setUnvoidLoadingId(unvoidRow.id)
      try {
        const restored = await unvoidReceipt(token, unvoidRow.id, {
          version: unvoidRow.version,
          overridePeriodLock: payload.overridePeriodLock,
          overrideReason: payload.overrideReason,
        })

        setListRows((prev) => {
          if (listStatus === 'VOID') {
            return prev.filter((item) => item.id !== unvoidRow.id)
          }

          return prev.map((item) =>
            item.id === unvoidRow.id
              ? {
                  ...item,
                  status: restored.status,
                  version: restored.version,
                  unallocatedAmount: restored.unallocatedAmount,
                  allocationStatus: restored.allocationStatus,
                  allocationPriority: restored.allocationPriority,
                  allocationSource: restored.allocationSource ?? null,
                  allocationSuggestedAt: restored.allocationSuggestedAt ?? null,
                }
              : item,
          )
        })

        if (listStatus === 'VOID') {
          setListTotal((prev) => Math.max(0, prev - 1))
        }
        setSelectedReceiptIds((prev) => prev.filter((id) => id !== unvoidRow.id))
        setUnvoidRow(null)
      } catch (err) {
        if (err instanceof ApiError) {
          setUnvoidError(err.message)
        } else {
          setUnvoidError('Không bỏ hủy được phiếu thu.')
        }
      } finally {
        setUnvoidLoadingId(null)
      }
    },
    [listStatus, token, unvoidRow],
  )

  const columns = useMemo(
    () => [
      {
        key: 'select',
        label: 'Chọn',
        align: 'center' as const,
        width: '72px',
        render: (row: ReceiptListItem) => {
          const canSelect = row.canManage && row.status.toUpperCase() === 'DRAFT'
          if (!canSelect) return <span className="muted">-</span>
          return (
            <input
              type="checkbox"
              checked={selectedReceiptIds.includes(row.id)}
              onChange={(event) => {
                setSelectedReceiptIds((prev) => {
                  if (event.target.checked) {
                    if (prev.includes(row.id)) return prev
                    return [...prev, row.id]
                  }
                  return prev.filter((id) => id !== row.id)
                })
              }}
              aria-label={`Chọn phiếu thu ${row.receiptNo?.trim() || row.id}`}
            />
          )
        },
      },
      {
        key: 'receiptDate',
        label: 'Ngày',
        render: (row: ReceiptListItem) => formatDate(row.receiptDate),
      },
      {
        key: 'receiptNo',
        label: 'Số chứng từ',
        render: (row: ReceiptListItem) =>
          row.receiptNo?.trim() ? row.receiptNo : <span className="muted">-</span>,
      },
      {
        key: 'customer',
        label: 'Khách hàng',
        render: (row: ReceiptListItem) =>
          row.customerName ? `${row.customerName} (${row.customerTaxCode})` : row.customerTaxCode,
      },
      {
        key: 'amount',
        label: 'Số tiền',
        align: 'right' as const,
        render: (row: ReceiptListItem) => formatMoney(row.amount),
      },
      {
        key: 'allocationStatus',
        label: 'Phân bổ',
        render: (row: ReceiptListItem) => {
          const status = row.allocationStatus.toUpperCase()
          const className =
            status === 'ALLOCATED'
              ? 'pill pill-ok'
              : status === 'SUGGESTED'
                ? 'pill pill-warn'
                : status === 'PARTIAL'
                  ? 'pill pill-info'
                  : 'pill pill-muted'
          return <span className={className}>{allocationStatusLabels[status] ?? status}</span>
        },
      },
      {
        key: 'status',
        label: 'Trạng thái',
        render: (row: ReceiptListItem) => {
          const status = row.status.toUpperCase()
          const className =
            status === 'APPROVED'
              ? 'pill pill-ok'
              : status === 'DRAFT'
                ? 'pill pill-warn'
                : 'pill pill-info'
          return <span className={className}>{receiptStatusLabels[status] ?? row.status}</span>
        },
      },
      {
        key: 'method',
        label: 'Hình thức',
        render: (row: ReceiptListItem) => methodLabels[row.method] ?? row.method,
      },
      {
        key: 'actions',
        label: 'Thao tác',
        render: (row: ReceiptListItem) => (
          <div className="table-actions">
            <button className="btn btn-ghost btn-sm" type="button" onClick={() => handleOpenView(row)}>
              Xem
            </button>
            {row.status === 'DRAFT' && row.canManage && (
              <button className="btn btn-ghost btn-sm" type="button" onClick={() => handleOpenApprove(row)}>
                Duyệt
              </button>
            )}
            {row.canManage && row.status !== 'VOID' && (
              <button className="btn btn-ghost btn-sm" type="button" onClick={() => setCancelRow(row)}>
                Hủy
              </button>
            )}
            {row.canManage && row.status === 'VOID' && (
              <button
                className="btn btn-ghost btn-sm"
                type="button"
                onClick={() => handleUnvoid(row)}
                disabled={unvoidLoadingId === row.id}
              >
                {unvoidLoadingId === row.id ? 'Đang bỏ hủy...' : 'Bỏ hủy'}
              </button>
            )}
            <button className="btn btn-ghost btn-sm" type="button" onClick={() => handleToggleReminder(row)}>
              {row.reminderDisabledAt ? 'Bật nhắc' : 'Tắt nhắc'}
            </button>
          </div>
        ),
      },
    ],
    [
      handleOpenApprove,
      handleOpenView,
      handleToggleReminder,
      handleUnvoid,
      selectedReceiptIds,
      unvoidLoadingId,
    ],
  )

  const unvoidReceiptLabel = useMemo(() => {
    if (!unvoidRow) return ''
    const receiptNo = unvoidRow.receiptNo?.trim()
    return receiptNo || unvoidRow.id
  }, [unvoidRow])

  const handleCancelConfirm = async (payload: {
    reason: string
    overridePeriodLock: boolean
    overrideReason?: string
  }) => {
    if (!token || !cancelRow) return
    setListMessage(null)
    setCancelLoading(true)
    setCancelError(null)
    try {
      await voidReceipt(token, cancelRow.id, {
        reason: payload.reason,
        version: cancelRow.version,
        overridePeriodLock: payload.overridePeriodLock,
        overrideReason: payload.overrideReason,
      })
      setCancelRow(null)
      setListRows((prev) => prev.filter((row) => row.id !== cancelRow.id))
      setSelectedReceiptIds((prev) => prev.filter((id) => id !== cancelRow.id))
    } catch (err) {
      setCancelError(err instanceof ApiError ? err.message : 'Không hủy được phiếu thu.')
    } finally {
      setCancelLoading(false)
    }
  }

  const selectableRows = useMemo(
    () => listRows.filter((row) => row.canManage && row.status.toUpperCase() === 'DRAFT'),
    [listRows],
  )

  const selectedEligibleRows = useMemo(
    () => selectableRows.filter((row) => selectedReceiptIds.includes(row.id)),
    [selectableRows, selectedReceiptIds],
  )

  const handleSelectAllReceipts = useCallback(() => {
    setSelectedReceiptIds(selectableRows.map((row) => row.id))
  }, [selectableRows])

  const handleClearSelection = useCallback(() => {
    setSelectedReceiptIds([])
  }, [])

  const handleBulkApproveConfirm = useCallback(
    async (payload: ActionConfirmPayload) => {
      if (!token || selectedEligibleRows.length === 0) return
      setBulkApproveLoading(true)
      setBulkApproveError(null)
      setListError(null)
      setListMessage(null)
      try {
        const result = await approveReceiptsBulk(token, {
          items: selectedEligibleRows.map((row) => ({
            receiptId: row.id,
            version: row.version,
            overridePeriodLock: payload.overridePeriodLock,
            overrideReason: payload.overrideReason,
          })),
          continueOnError: true,
        })

        const failedIds = new Set(
          result.items
            .filter(
              (item) =>
                Boolean(item.errorCode) ||
                Boolean(item.errorMessage) ||
                item.result?.toUpperCase() === 'FAILED',
            )
            .map((item) => item.receiptId),
        )
        const failedMessages = result.items
          .filter((item) => failedIds.has(item.receiptId))
          .map((item) => item.errorMessage || item.errorCode || `Lỗi ${item.receiptId}`)

        setSelectedReceiptIds((prev) => prev.filter((id) => failedIds.has(id)))
        setBulkApproveOpen(false)
        setListReloadTick((prev) => prev + 1)

        if (result.approved > 0) {
          const successMessage = `Đã duyệt ${result.approved}/${result.total} phiếu thu đã chọn.`
          if (failedIds.size > 0) {
            setListError(
              `${successMessage} ${failedIds.size} phiếu thu thất bại: ${failedMessages
                .slice(0, 2)
                .join('; ')}`,
            )
          } else {
            setListMessage(successMessage)
          }
        } else {
          setListError(
            `Không duyệt được phiếu thu nào: ${failedMessages.slice(0, 2).join('; ') || 'Lỗi không xác định.'}`,
          )
        }
      } catch (err) {
        if (err instanceof ApiError) {
          setBulkApproveError(err.message)
        } else {
          setBulkApproveError('Không duyệt hàng loạt được phiếu thu.')
        }
      } finally {
        setBulkApproveLoading(false)
      }
    },
    [selectedEligibleRows, token],
  )

  return (
    <section className="card">
      <div className="card-row">
        <div>
          <h3>Danh sách phiếu thu</h3>
        </div>
        {activeTab === 'receipts' && listLoading && <span className="muted">Đang tải...</span>}
      </div>

      <div className="tab-row" role="tablist" aria-label="Danh sách phiếu thu">
        <button
          className={`tab ${activeTab === 'receipts' ? 'tab--active' : ''}`}
          type="button"
          role="tab"
          aria-selected={activeTab === 'receipts'}
          onClick={() => setActiveTab('receipts')}
        >
          Tất cả phiếu thu
        </button>
        <button
          className={`tab ${activeTab === 'surplusQueue' ? 'tab--active' : ''}`}
          type="button"
          role="tab"
          aria-selected={activeTab === 'surplusQueue'}
          onClick={() => setActiveTab('surplusQueue')}
        >
          Tiền thừa chưa phân bổ
        </button>
      </div>

      {activeTab === 'receipts' ? (
        <>
          <div className="filters-grid">
            <label className="field">
              <span>Trạng thái</span>
              <select
                value={listAllocationStatus || listStatus}
                onChange={(event) => {
                  const next = event.target.value
                  if (!next) {
                    setListStatus('')
                    setListAllocationStatus('')
                    storeFilter(RECEIPTS_STATUS_KEY, '')
                    storeFilter(RECEIPTS_ALLOCATION_KEY, '')
                  } else if (ALLOCATION_FILTERS.has(next)) {
                    setListAllocationStatus(next)
                    setListStatus('')
                    storeFilter(RECEIPTS_ALLOCATION_KEY, next)
                    storeFilter(RECEIPTS_STATUS_KEY, '')
                  } else {
                    setListStatus(next)
                    setListAllocationStatus('')
                    storeFilter(RECEIPTS_STATUS_KEY, next)
                    storeFilter(RECEIPTS_ALLOCATION_KEY, '')
                  }
                  setListPage(1)
                }}
              >
                <option value="">Tất cả</option>
                <option value="DRAFT">{receiptStatusLabels.DRAFT}</option>
                <option value="APPROVED">{receiptStatusLabels.APPROVED}</option>
                <option value="VOID">{receiptStatusLabels.VOID}</option>
                <option value="ALLOCATED">{allocationStatusLabels.ALLOCATED}</option>
                <option value="PARTIAL">{allocationStatusLabels.PARTIAL}</option>
                <option value="UNALLOCATED">{allocationStatusLabels.UNALLOCATED}</option>
              </select>
            </label>
            <LookupInput
              label="MST bên bán"
              value={listSeller}
              placeholder="MST bên bán"
              options={sellerOptions}
              onChange={(value) => {
                setListSeller(value)
                setSellerQuery(value)
                setListPage(1)
              }}
            />
            <LookupInput
              label="MST bên mua"
              value={listCustomer}
              placeholder="MST bên mua"
              options={customerOptions}
              onChange={(value) => {
                setListCustomer(value)
                setCustomerQuery(value)
                setListPage(1)
              }}
            />
            <label className="field">
              <span>&nbsp;</span>
              <button
                className="btn btn-ghost btn-sm"
                type="button"
                onClick={() => setAdvancedOpen((prev) => !prev)}
              >
                {advancedOpen ? 'Ẩn tìm nâng cao' : 'Tìm kiếm nâng cao'}
              </button>
            </label>
          </div>

          {advancedOpen && (
            <div className="advanced-panel">
              <div className="card-row">
                <strong>Tìm kiếm nâng cao</strong>
                <button className="btn btn-ghost btn-sm" type="button" onClick={() => setAdvancedOpen(false)}>
                  Thu gọn
                </button>
              </div>
              <div className="advanced-panel__content">
                <div className="filters-grid filters-grid--compact">
                  <label className="field field-span-full">
                    <span>Tìm chứng từ</span>
                    <input
                      value={listDocumentNo}
                      placeholder="Số phiếu thu / hóa đơn / khoản trả hộ"
                      onChange={(event) => {
                        const next = event.target.value
                        setListDocumentNo(next)
                        storeFilter(RECEIPTS_DOCUMENT_KEY, next)
                        setListPage(1)
                      }}
                    />
                  </label>
                  <label className="field">
                    <span>Ngày chứng từ từ</span>
                    <input
                      type="date"
                      value={listDateFrom}
                      onChange={(event) => {
                        const next = event.target.value
                        setListDateFrom(next)
                        storeFilter(RECEIPTS_DATE_FROM_KEY, next)
                        setListPage(1)
                      }}
                    />
                  </label>
                  <label className="field">
                    <span>Ngày chứng từ đến</span>
                    <input
                      type="date"
                      value={listDateTo}
                      onChange={(event) => {
                        const next = event.target.value
                        setListDateTo(next)
                        storeFilter(RECEIPTS_DATE_TO_KEY, next)
                        setListPage(1)
                      }}
                    />
                  </label>
                  <label className="field">
                    <span>Số tiền từ</span>
                    <MoneyInput
                      value={listAmountMin}
                      placeholder="0"
                      onValueChange={(next) => {
                        setListAmountMin(next)
                        storeFilter(RECEIPTS_AMOUNT_MIN_KEY, next)
                        setListPage(1)
                      }}
                    />
                  </label>
                  <label className="field">
                    <span>Số tiền đến</span>
                    <MoneyInput
                      value={listAmountMax}
                      placeholder="0"
                      onValueChange={(next) => {
                        setListAmountMax(next)
                        storeFilter(RECEIPTS_AMOUNT_MAX_KEY, next)
                        setListPage(1)
                      }}
                    />
                  </label>
                  <label className="field">
                    <span>Hình thức</span>
                    <select
                      value={listMethod}
                      onChange={(event) => {
                        const next = event.target.value
                        setListMethod(next)
                        storeFilter(RECEIPTS_METHOD_KEY, next)
                        setListPage(1)
                      }}
                    >
                      <option value="">Tất cả</option>
                      <option value="BANK">{methodLabels.BANK}</option>
                      <option value="CASH">{methodLabels.CASH}</option>
                      <option value="OTHER">{methodLabels.OTHER}</option>
                    </select>
                  </label>
                  <label className="field">
                    <span>Ưu tiên phân bổ</span>
                    <select
                      value={listPriority}
                      onChange={(event) => {
                        const next = event.target.value
                        setListPriority(next)
                        storeFilter(RECEIPTS_PRIORITY_KEY, next)
                        setListPage(1)
                      }}
                    >
                      <option value="">Tất cả</option>
                      <option value="ISSUE_DATE">{allocationPriorityLabels.ISSUE_DATE}</option>
                      <option value="DUE_DATE">{allocationPriorityLabels.DUE_DATE}</option>
                    </select>
                  </label>
                  <label className="field">
                    <span>Nhắc duyệt</span>
                    <select
                      value={listReminder}
                      onChange={(event) => {
                        const next = event.target.value
                        setListReminder(next)
                        storeFilter(RECEIPTS_REMINDER_KEY, next)
                        setListPage(1)
                      }}
                    >
                      <option value="ALL">Tất cả</option>
                      <option value="ENABLED">Đang bật</option>
                      <option value="DISABLED">Đã tắt</option>
                    </select>
                  </label>
                </div>
                <div className="filters-actions">
                  <button
                    className="btn btn-ghost btn-sm"
                    type="button"
                    onClick={() => {
                      setListDocumentNo('')
                      setListDateFrom('')
                      setListDateTo('')
                      setListAmountMin('')
                      setListAmountMax('')
                      setListMethod('')
                      setListPriority('')
                      setListReminder(DEFAULT_REMINDER_FILTER)
                      storeFilter(RECEIPTS_DOCUMENT_KEY, '')
                      storeFilter(RECEIPTS_DATE_FROM_KEY, '')
                      storeFilter(RECEIPTS_DATE_TO_KEY, '')
                      storeFilter(RECEIPTS_AMOUNT_MIN_KEY, '')
                      storeFilter(RECEIPTS_AMOUNT_MAX_KEY, '')
                      storeFilter(RECEIPTS_METHOD_KEY, '')
                      storeFilter(RECEIPTS_PRIORITY_KEY, '')
                      storeFilter(RECEIPTS_REMINDER_KEY, DEFAULT_REMINDER_FILTER)
                      setListPage(1)
                    }}
                  >
                    Đặt lại
                  </button>
                  <span className="muted">Bộ lọc nâng cao áp dụng ngay khi thay đổi.</span>
                </div>
              </div>
            </div>
          )}

          {validationError && <div className="alert alert--error">{validationError}</div>}
          {listError && <div className="alert alert--error">{listError}</div>}
          {listMessage && <div className="alert alert--success">{listMessage}</div>}

          <div className="filters-actions">
            <span className="muted">
              Đã chọn {selectedEligibleRows.length}/{selectableRows.length} phiếu thu nháp có thể duyệt.
            </span>
            <button
              className="btn btn-ghost btn-sm"
              type="button"
              onClick={handleSelectAllReceipts}
              disabled={selectableRows.length === 0 || bulkApproveLoading}
            >
              Chọn tất cả
            </button>
            <button
              className="btn btn-ghost btn-sm"
              type="button"
              onClick={handleClearSelection}
              disabled={selectedReceiptIds.length === 0 || bulkApproveLoading}
            >
              Bỏ chọn
            </button>
            <button
              className="btn btn-primary btn-sm"
              type="button"
              onClick={() => {
                setBulkApproveError(null)
                setBulkApproveOpen(true)
              }}
              disabled={selectedEligibleRows.length === 0 || bulkApproveLoading}
            >
              Duyệt đã chọn
            </button>
          </div>

          <DataTable
            columns={columns}
            rows={listRows}
            getRowKey={(row) => row.id}
            minWidth="1100px"
            emptyMessage={listLoading ? 'Đang tải...' : 'Không có phiếu thu.'}
            pagination={{
              page: listPage,
              pageSize: listPageSize,
              total: listTotal,
            }}
            onPageChange={setListPage}
            onPageSizeChange={(size) => {
              storePageSize(size)
              setListPageSize(size)
              setListPage(1)
            }}
          />
        </>
      ) : (
        <ReceiptSurplusQueuePanel token={token} />
      )}

      <ReceiptCancelModal
        key={cancelRow?.id ?? 'receipt-cancel'}
        isOpen={Boolean(cancelRow)}
        onClose={() => {
          setCancelRow(null)
          setCancelError(null)
        }}
        onConfirm={handleCancelConfirm}
        loading={cancelLoading}
        error={cancelError}
      />

      <ReceiptAllocationModal
        isOpen={Boolean(approvalRow)}
        token={token}
        sellerTaxCode={approvalRow?.sellerTaxCode ?? ''}
        customerTaxCode={approvalRow?.customerTaxCode ?? ''}
        amount={approvalRow?.amount ?? 0}
        allocationPriority={approvalPriority}
        onPriorityChange={setApprovalPriority}
        openItems={approvalOpenItems}
        selectedTargets={approvalTargets}
        onApply={handleApprove}
        confirmLabel={approvalLoading ? 'Đang duyệt...' : 'Duyệt phiếu thu'}
        onClose={() => {
          setApprovalRow(null)
          setApprovalTargets([])
          setApprovalOpenItems([])
          setApprovalError(null)
        }}
      />

      <ReceiptViewAllocationsModal
        isOpen={Boolean(viewRow)}
        receipt={viewRow}
        allocations={viewAllocations}
        loading={viewLoading}
        error={viewError}
        onClose={() => {
          setViewRow(null)
          setViewAllocations([])
          setViewError(null)
        }}
      />

      <ActionConfirmModal
        isOpen={Boolean(unvoidRow)}
        title="Bỏ hủy phiếu thu"
        description={
          unvoidRow
            ? `Xác nhận bỏ hủy phiếu thu ${unvoidReceiptLabel}. Bạn có thể bật tùy chọn mở khóa kỳ nếu cần.`
            : undefined
        }
        confirmLabel="Xác nhận bỏ hủy"
        showOverrideOption
        overrideLabel="Cho phép bỏ hủy ngoài kỳ khóa"
        overrideReasonLabel="Lý do mở khóa kỳ"
        overrideReasonPlaceholder="Nhập lý do mở khóa kỳ"
        loading={Boolean(unvoidRow && unvoidLoadingId === unvoidRow.id)}
        error={unvoidError}
        tone="danger"
        onClose={() => {
          setUnvoidRow(null)
          setUnvoidError(null)
        }}
        onConfirm={(payload) => {
          void handleUnvoidConfirm(payload)
        }}
      />

      <ActionConfirmModal
        isOpen={bulkApproveOpen}
        title="Duyệt phiếu thu đã chọn"
        description={`Xác nhận duyệt ${selectedEligibleRows.length} phiếu thu nháp đã chọn.`}
        confirmLabel="Xác nhận duyệt"
        showOverrideOption
        overrideLabel="Cho phép duyệt ngoài kỳ khóa"
        overrideReasonLabel="Lý do mở khóa kỳ"
        overrideReasonPlaceholder="Nhập lý do mở khóa kỳ"
        loading={bulkApproveLoading}
        error={bulkApproveError}
        onClose={() => {
          if (bulkApproveLoading) return
          setBulkApproveOpen(false)
          setBulkApproveError(null)
        }}
        onConfirm={(payload) => {
          void handleBulkApproveConfirm(payload)
        }}
      />

      {approvalError && <div className="alert alert--error">{approvalError}</div>}
      {approvalRow && approvalLoading && (
        <div className="alert alert--info">Đang chuẩn bị duyệt phiếu thu...</div>
      )}
      {approvalRow && (
        <div className="alert alert--info">
          Ưu tiên phân bổ: {allocationPriorityLabels[approvalPriority] ?? approvalPriority}.
        </div>
      )}
    </section>
  )
}
