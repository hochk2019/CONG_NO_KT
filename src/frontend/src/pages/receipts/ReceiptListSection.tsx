import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  approveReceipt,
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
import { useDebouncedValue } from '../../hooks/useDebouncedValue'
import { formatDate, formatMoney } from '../../utils/format'
import { ApiError } from '../../api/client'
import ReceiptAllocationModal from './ReceiptAllocationModal'
import ReceiptCancelModal from './ReceiptCancelModal'
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

const collectOverrideOptions = (actionLabel: string) => {
  const overrideInput = window.prompt(
    `${actionLabel}: nếu cần vượt khóa kỳ, nhập lý do (bỏ trống nếu không).`,
    '',
  )
  if (overrideInput === null) {
    return null
  }

  const reason = overrideInput.trim()
  if (!reason) {
    return { overridePeriodLock: false, overrideReason: undefined }
  }

  return { overridePeriodLock: true, overrideReason: reason }
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
  const [listError, setListError] = useState<string | null>(null)
  const [advancedOpen, setAdvancedOpen] = useState(false)

  const [cancelRow, setCancelRow] = useState<ReceiptListItem | null>(null)
  const [cancelLoading, setCancelLoading] = useState(false)
  const [cancelError, setCancelError] = useState<string | null>(null)
  const [unvoidLoadingId, setUnvoidLoadingId] = useState<string | null>(null)

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
    if (!token) return
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
    validationError,
  ])

  const handleToggleReminder = useCallback(
    async (row: ReceiptListItem) => {
      if (!token) return
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
    } catch (err) {
      setApprovalError(err instanceof ApiError ? err.message : 'Không duyệt được phiếu thu.')
    } finally {
      setApprovalLoading(false)
    }
  }

  const handleOpenView = useCallback(
    async (row: ReceiptListItem) => {
      if (!token) return
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
    async (row: ReceiptListItem) => {
      if (!token || !row.canManage) return
      setListError(null)

      const confirmed = window.confirm(`Bạn chắc chắn muốn bỏ hủy phiếu thu ${row.receiptNo ?? row.id}?`)
      if (!confirmed) {
        return
      }

      const overrideOptions = collectOverrideOptions(`Bỏ hủy ${row.receiptNo ?? row.id}`)
      if (!overrideOptions) {
        return
      }

      setUnvoidLoadingId(row.id)
      try {
        const restored = await unvoidReceipt(token, row.id, {
          version: row.version,
          overridePeriodLock: overrideOptions.overridePeriodLock,
          overrideReason: overrideOptions.overrideReason,
        })

        setListRows((prev) => {
          if (listStatus === 'VOID') {
            return prev.filter((item) => item.id !== row.id)
          }

          return prev.map((item) =>
            item.id === row.id
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
      } catch (err) {
        if (err instanceof ApiError) {
          setListError(err.message)
        } else {
          setListError('Không bỏ hủy được phiếu thu.')
        }
      } finally {
        setUnvoidLoadingId(null)
      }
    },
    [listStatus, token],
  )

  const columns = useMemo(
    () => [
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
                onClick={() => void handleUnvoid(row)}
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
    [handleOpenApprove, handleOpenView, handleToggleReminder, handleUnvoid, unvoidLoadingId],
  )

  const handleCancelConfirm = async (payload: {
    reason: string
    overridePeriodLock: boolean
    overrideReason?: string
  }) => {
    if (!token || !cancelRow) return
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
    } catch (err) {
      setCancelError(err instanceof ApiError ? err.message : 'Không hủy được phiếu thu.')
    } finally {
      setCancelLoading(false)
    }
  }

  return (
    <section className="card">
      <div className="card-row">
        <div>
          <h3>Danh sách phiếu thu</h3>
        </div>
        {listLoading && <span className="muted">Đang tải...</span>}
      </div>

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
                <input
                  type="number"
                  min={0}
                  step="1000"
                  value={listAmountMin}
                  placeholder="0"
                  onChange={(event) => {
                    const next = event.target.value
                    setListAmountMin(next)
                    storeFilter(RECEIPTS_AMOUNT_MIN_KEY, next)
                    setListPage(1)
                  }}
                />
              </label>
              <label className="field">
                <span>Số tiền đến</span>
                <input
                  type="number"
                  min={0}
                  step="1000"
                  value={listAmountMax}
                  placeholder="0"
                  onChange={(event) => {
                    const next = event.target.value
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
