import { useEffect, useState } from 'react'
import { ApiError } from '../../api/client'
import {
  approveAdvance,
  createAdvance,
  listAdvances,
  unvoidAdvance,
  updateAdvance,
  voidAdvance,
  type AdvanceListItem,
} from '../../api/advances'
import {
  fetchCustomerLookup,
  fetchSellerLookup,
  mapTaxCodeOptions,
  type LookupOption,
} from '../../api/lookups'
import DataTable from '../../components/DataTable'
import LookupInput from '../../components/LookupInput'
import ActionConfirmModal, { type ActionConfirmPayload } from '../../components/modals/ActionConfirmModal'
import { useDebouncedValue } from '../../hooks/useDebouncedValue'
import {
  advanceStatusLabels,
  buildManualAdvanceColumns,
  shortAdvanceId,
} from './manualAdvancesColumns'

const DEFAULT_PAGE_SIZE = 10
const PAGE_SIZE_STORAGE_KEY = 'pref.table.pageSize'
const ADVANCES_STATUS_KEY = 'pref.advances.status'

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

const DEFAULT_SOURCE_FILTER = ''

type ManualAdvancesSectionProps = {
  token: string
  canApprove: boolean
}

type ManualAdvanceConfirmAction = 'approve' | 'void' | 'unvoid'

type ManualAdvanceConfirmState = {
  action: ManualAdvanceConfirmAction
  row: AdvanceListItem
}

export default function ManualAdvancesSection({ token, canApprove }: ManualAdvancesSectionProps) {
  const [sellerOptions, setSellerOptions] = useState<LookupOption[]>([])
  const [customerOptions, setCustomerOptions] = useState<LookupOption[]>([])
  const [sellerQuery, setSellerQuery] = useState('')
  const [customerQuery, setCustomerQuery] = useState('')
  const debouncedSellerQuery = useDebouncedValue(sellerQuery, 300)
  const debouncedCustomerQuery = useDebouncedValue(customerQuery, 300)
  const [sellerTaxCode, setSellerTaxCode] = useState('')
  const [customerTaxCode, setCustomerTaxCode] = useState('')
  const [advanceNo, setAdvanceNo] = useState('')
  const [advanceDate, setAdvanceDate] = useState('')
  const [amount, setAmount] = useState('0')
  const [description, setDescription] = useState('')
  const [listRows, setListRows] = useState<AdvanceListItem[]>([])
  const [listPage, setListPage] = useState(1)
  const [listPageSize, setListPageSize] = useState(() => getStoredPageSize())
  const [listTotal, setListTotal] = useState(0)
  const [listStatus, setListStatus] = useState(() => getStoredFilter(ADVANCES_STATUS_KEY))
  const [listSeller, setListSeller] = useState('')
  const [listCustomer, setListCustomer] = useState('')
  const [listAdvanceNo, setListAdvanceNo] = useState('')
  const [listFromDate, setListFromDate] = useState('')
  const [listToDate, setListToDate] = useState('')
  const [listAmountMin, setListAmountMin] = useState('')
  const [listAmountMax, setListAmountMax] = useState('')
  const [listSource, setListSource] = useState(DEFAULT_SOURCE_FILTER)
  const [showAdvancedFilters, setShowAdvancedFilters] = useState(false)
  const [listReload, setListReload] = useState(0)
  const [listLoading, setListLoading] = useState(false)
  const [listError, setListError] = useState<string | null>(null)
  const [createMessage, setCreateMessage] = useState<string | null>(null)
  const [createError, setCreateError] = useState<string | null>(null)
  const [actionMessage, setActionMessage] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [loadingAction, setLoadingAction] = useState('')
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editingDescription, setEditingDescription] = useState('')
  const [confirmState, setConfirmState] = useState<ManualAdvanceConfirmState | null>(null)
  const [confirmError, setConfirmError] = useState<string | null>(null)
  const [selectedAdvanceIds, setSelectedAdvanceIds] = useState<string[]>([])
  const [bulkConfirmAction, setBulkConfirmAction] = useState<ManualAdvanceConfirmAction | null>(null)
  const [bulkConfirmError, setBulkConfirmError] = useState<string | null>(null)

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
      setListLoading(true)
      setListError(null)
      try {
        const minAmountValue = Number(listAmountMin)
        const maxAmountValue = Number(listAmountMax)
        const hasMin = listAmountMin.trim() !== '' && Number.isFinite(minAmountValue)
        const hasMax = listAmountMax.trim() !== '' && Number.isFinite(maxAmountValue)
        if (hasMin && hasMax && minAmountValue > maxAmountValue) {
          setListError('Khoảng số tiền không hợp lệ: "Số tiền từ" phải nhỏ hơn hoặc bằng "Số tiền đến".')
          setListLoading(false)
          return
        }
        const hasFrom = listFromDate.trim() !== ''
        const hasTo = listToDate.trim() !== ''
        if (hasFrom && hasTo) {
          const fromValue = Date.parse(listFromDate)
          const toValue = Date.parse(listToDate)
          if (Number.isFinite(fromValue) && Number.isFinite(toValue) && fromValue > toValue) {
            setListError('Khoảng ngày không hợp lệ: "Từ ngày" phải nhỏ hơn hoặc bằng "Đến ngày".')
            setListLoading(false)
            return
          }
        }
        const result = await listAdvances({
          token,
          sellerTaxCode: listSeller || undefined,
          customerTaxCode: listCustomer || undefined,
          status: listStatus || undefined,
          advanceNo: listAdvanceNo.trim() || undefined,
          from: listFromDate || undefined,
          to: listToDate || undefined,
          amountMin: hasMin ? minAmountValue : undefined,
          amountMax: hasMax ? maxAmountValue : undefined,
          source: listSource || undefined,
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
          setListError('Không tải được danh sách khoản trả hộ KH.')
        }
      } finally {
        if (isActive) {
          setListLoading(false)
        }
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
    listCustomer,
    listSeller,
    listAdvanceNo,
    listFromDate,
    listToDate,
    listAmountMin,
    listAmountMax,
    listSource,
    listReload,
  ])

  useEffect(() => {
    setSelectedAdvanceIds((prev) => {
      if (prev.length === 0) return prev
      const rowIds = new Set(listRows.map((row) => row.id))
      return prev.filter((id) => rowIds.has(id))
    })
  }, [listRows])

  const resetMessages = () => {
    setCreateError(null)
    setCreateMessage(null)
    setActionError(null)
    setActionMessage(null)
  }

  const resetAdvancedFilters = () => {
    setListAdvanceNo('')
    setListFromDate('')
    setListToDate('')
    setListAmountMin('')
    setListAmountMax('')
    setListSource(DEFAULT_SOURCE_FILTER)
    setListPage(1)
  }

  const setFieldError = (field: string, message?: string) => {
    setFieldErrors((prev) => ({
      ...prev,
      [field]: message ?? '',
    }))
  }

  const validateCreate = () => {
    resetMessages()
    setFieldErrors({})

    let hasError = false
    if (!sellerTaxCode.trim() || !customerTaxCode.trim()) {
      setFieldError('sellerTaxCode', sellerTaxCode.trim() ? '' : 'Vui lòng nhập MST bên bán.')
      setFieldError('customerTaxCode', customerTaxCode.trim() ? '' : 'Vui lòng nhập MST bên mua.')
      hasError = true
    }
    if (!advanceDate.trim()) {
      setFieldError('advanceDate', 'Vui lòng chọn ngày trả hộ.')
      hasError = true
    }
    const amountValue = Number(amount)
    if (!Number.isFinite(amountValue) || amountValue <= 0) {
      setFieldError('amount', 'Số tiền không hợp lệ.')
      hasError = true
    }
    if (hasError) {
      setCreateError('Vui lòng kiểm tra lại các trường bắt buộc.')
      return null
    }

    return {
      sellerTaxCode: sellerTaxCode.trim(),
      customerTaxCode: customerTaxCode.trim(),
      advanceNo: advanceNo.trim() || null,
      advanceDate: advanceDate.trim(),
      amount: amountValue,
      description: description.trim() || undefined,
    }
  }

  const handleCreate = async () => {
    if (!token) return
    const payload = validateCreate()
    if (!payload) return

    setLoadingAction('create')
    try {
      const result = await createAdvance(token, payload)
      setCreateMessage(`Đã tạo khoản trả hộ ${shortAdvanceId(result.id)} (DRAFT).`)
      setDescription('')
      setAdvanceNo('')
      setAmount('0')
      setListReload((value) => value + 1)
    } catch (err) {
      if (err instanceof ApiError) {
        setCreateError(err.message)
      } else {
        setCreateError('Không tạo được khoản trả hộ.')
      }
    } finally {
      setLoadingAction('')
    }
  }

  const handleCreateAndApprove = async () => {
    if (!token || !canApprove) return
    const payload = validateCreate()
    if (!payload) return

    setLoadingAction('create-approve')
    try {
      const created = await createAdvance(token, payload)
      await approveAdvance(token, created.id, { version: created.version })
      setCreateMessage(`Đã tạo và phê duyệt khoản trả hộ ${shortAdvanceId(created.id)}.`)
      setDescription('')
      setAdvanceNo('')
      setAmount('0')
      setListReload((value) => value + 1)
    } catch (err) {
      if (err instanceof ApiError) {
        setCreateError(err.message)
      } else {
        setCreateError('Không tạo/phê duyệt được khoản trả hộ.')
      }
    } finally {
      setLoadingAction('')
    }
  }

  const handleApprove = (row: AdvanceListItem) => {
    if (!token || !row.canManage) return
    resetMessages()
    setConfirmError(null)
    setConfirmState({ action: 'approve', row })
  }

  const handleVoid = (row: AdvanceListItem) => {
    if (!token || !row.canManage) return
    resetMessages()
    const status = row.status.toUpperCase()
    if (status === 'PAID') {
      setActionError(
        'Khoản trả hộ đã tất toán. Muốn hủy cần hoàn tác phân bổ/phiếu thu liên quan trước.',
      )
      return
    }

    setConfirmError(null)
    setConfirmState({ action: 'void', row })
  }

  const handleUnvoid = (row: AdvanceListItem) => {
    if (!token || !row.canManage) return
    resetMessages()
    setConfirmError(null)
    setConfirmState({ action: 'unvoid', row })
  }

  const handleConfirmAction = async (payload: ActionConfirmPayload) => {
    if (!token || !confirmState) return
    const { action, row } = confirmState
    const actionKey = `${action}:${row.id}`
    setConfirmError(null)
    setLoadingAction(actionKey)
    try {
      if (action === 'approve') {
        await approveAdvance(token, row.id, {
          version: row.version,
          overridePeriodLock: payload.overridePeriodLock,
          overrideReason: payload.overrideReason,
        })
        setActionMessage(`Đã phê duyệt khoản trả hộ ${shortAdvanceId(row.id)}.`)
      } else if (action === 'void') {
        await voidAdvance(token, row.id, {
          reason: payload.reason,
          version: row.version,
          overridePeriodLock: payload.overridePeriodLock,
          overrideReason: payload.overrideReason,
        })
        setActionMessage(`Đã hủy khoản trả hộ ${shortAdvanceId(row.id)}.`)
      } else {
        await unvoidAdvance(token, row.id, {
          version: row.version,
          overridePeriodLock: payload.overridePeriodLock,
          overrideReason: payload.overrideReason,
        })
        setActionMessage(`Đã bỏ hủy khoản trả hộ ${shortAdvanceId(row.id)}.`)
      }
      setSelectedAdvanceIds((prev) => prev.filter((id) => id !== row.id))
      setConfirmState(null)
      setListReload((value) => value + 1)
    } catch (err) {
      if (err instanceof ApiError) {
        setConfirmError(err.message)
      } else if (action === 'approve') {
        setConfirmError('Không phê duyệt được khoản trả hộ.')
      } else if (action === 'void') {
        setConfirmError('Không hủy được khoản trả hộ.')
      } else {
        setConfirmError('Không bỏ hủy được khoản trả hộ.')
      }
    } finally {
      setLoadingAction('')
    }
  }

  const handleStartEdit = (row: AdvanceListItem) => {
    resetMessages()
    setEditingId(row.id)
    setEditingDescription(row.description ?? '')
  }

  const handleCancelEdit = () => {
    setEditingId(null)
    setEditingDescription('')
  }

  const handleSaveEdit = async (row: AdvanceListItem) => {
    if (!token || !row.canManage) return
    resetMessages()
    setLoadingAction(`update:${row.id}`)
    try {
      const result = await updateAdvance(token, row.id, {
        description: editingDescription.trim() || null,
        version: row.version,
      })
      setListRows((prev) =>
        prev.map((item) =>
          item.id === row.id
            ? { ...item, description: result.description ?? null, version: result.version }
            : item,
        ),
      )
      setActionMessage(`Đã cập nhật ghi chú cho ${shortAdvanceId(row.id)}.`)
      setEditingId(null)
      setEditingDescription('')
    } catch (err) {
      if (err instanceof ApiError) {
        setActionError(err.message)
      } else {
        setActionError('Không cập nhật được ghi chú.')
      }
    } finally {
      setLoadingAction('')
    }
  }

  const isActionableForSelection = (row: AdvanceListItem) => {
    if (!row.canManage) return false
    const status = row.status.toUpperCase()
    if (status === 'DRAFT') return true
    if (status === 'VOID') return true
    if (status === 'APPROVED' || status === 'PAID') return true
    return false
  }

  const isBulkEligible = (row: AdvanceListItem, action: ManualAdvanceConfirmAction) => {
    if (!row.canManage) return false
    const status = row.status.toUpperCase()
    if (action === 'approve') return status === 'DRAFT'
    if (action === 'void') return status !== 'VOID' && status !== 'PAID'
    return status === 'VOID'
  }

  const selectedRows = listRows.filter((row) => selectedAdvanceIds.includes(row.id))
  const selectedApproveCount = selectedRows.filter((row) => isBulkEligible(row, 'approve')).length
  const selectedVoidCount = selectedRows.filter((row) => isBulkEligible(row, 'void')).length
  const selectedUnvoidCount = selectedRows.filter((row) => isBulkEligible(row, 'unvoid')).length
  const selectableRowIds = listRows.filter(isActionableForSelection).map((row) => row.id)
  const visibleDraftCount = listRows.filter((row) => row.status.toUpperCase() === 'DRAFT').length
  const visibleApprovedCount = listRows.filter((row) => {
    const status = row.status.toUpperCase()
    return status === 'APPROVED' || status === 'PAID'
  }).length
  const visibleVoidCount = listRows.filter((row) => row.status.toUpperCase() === 'VOID').length
  const hasSelectedRows = selectedAdvanceIds.length > 0
  const quickCreateModeLabel = canApprove
    ? 'Hồ sơ đủ thông tin có thể tạo và phê duyệt ngay trên cùng một nhịp thao tác.'
    : 'Tài khoản hiện tại chỉ tạo nháp; bước phê duyệt sẽ do Admin hoặc Supervisor xử lý.'

  const handleBulkAction = (action: ManualAdvanceConfirmAction) => {
    if (action === 'approve' && selectedApproveCount === 0) return
    if (action === 'void' && selectedVoidCount === 0) return
    if (action === 'unvoid' && selectedUnvoidCount === 0) return
    resetMessages()
    setBulkConfirmError(null)
    setBulkConfirmAction(action)
  }

  const handleBulkConfirmAction = async (payload: ActionConfirmPayload) => {
    if (!token || !bulkConfirmAction) return

    const targetRows = selectedRows.filter((row) => isBulkEligible(row, bulkConfirmAction))
    if (targetRows.length === 0) return

    const actionKey = `bulk:${bulkConfirmAction}`
    setLoadingAction(actionKey)
    setBulkConfirmError(null)

    const failedIds: string[] = []
    const failedMessages: string[] = []
    let successCount = 0

    for (const row of targetRows) {
      try {
        if (bulkConfirmAction === 'approve') {
          await approveAdvance(token, row.id, {
            version: row.version,
            overridePeriodLock: payload.overridePeriodLock,
            overrideReason: payload.overrideReason,
          })
        } else if (bulkConfirmAction === 'void') {
          await voidAdvance(token, row.id, {
            reason: payload.reason,
            version: row.version,
            overridePeriodLock: payload.overridePeriodLock,
            overrideReason: payload.overrideReason,
          })
        } else {
          await unvoidAdvance(token, row.id, {
            version: row.version,
            overridePeriodLock: payload.overridePeriodLock,
            overrideReason: payload.overrideReason,
          })
        }
        successCount += 1
      } catch (err) {
        failedIds.push(row.id)
        if (err instanceof ApiError) {
          failedMessages.push(`${shortAdvanceId(row.id)}: ${err.message}`)
        } else {
          failedMessages.push(`${shortAdvanceId(row.id)}: Lỗi không xác định`)
        }
      }
    }

    if (successCount > 0) {
      const actionLabel =
        bulkConfirmAction === 'approve'
          ? 'phê duyệt'
          : bulkConfirmAction === 'void'
            ? 'hủy'
            : 'bỏ hủy'
      setActionMessage(`Đã ${actionLabel} ${successCount}/${targetRows.length} khoản trả hộ đã chọn.`)
      setListReload((value) => value + 1)
    }

    if (failedIds.length > 0) {
      setBulkConfirmError(
        `Thất bại ${failedIds.length} khoản trả hộ: ${failedMessages.slice(0, 2).join('; ')}`,
      )
      setSelectedAdvanceIds(failedIds)
    } else {
      setSelectedAdvanceIds([])
      setBulkConfirmAction(null)
    }

    setLoadingAction('')
  }

  const listColumns = buildManualAdvanceColumns({
    editingId,
    editingDescription,
    setEditingDescription,
    onStartEdit: handleStartEdit,
    onSaveEdit: handleSaveEdit,
    onCancelEdit: handleCancelEdit,
    onApprove: handleApprove,
    onVoid: handleVoid,
    onUnvoid: handleUnvoid,
    loadingAction,
  })

  const tableColumns = [
    {
      key: 'select',
      label: 'Chọn',
      align: 'center' as const,
      width: '72px',
      render: (row: AdvanceListItem) => {
        if (!isActionableForSelection(row)) return <span className="muted">-</span>
        return (
          <input
            type="checkbox"
            checked={selectedAdvanceIds.includes(row.id)}
            onChange={(event) => {
              setSelectedAdvanceIds((prev) => {
                if (event.target.checked) {
                  if (prev.includes(row.id)) return prev
                  return [...prev, row.id]
                }
                return prev.filter((id) => id !== row.id)
              })
            }}
            aria-label={`Chọn khoản trả hộ ${shortAdvanceId(row.id)}`}
          />
        )
      },
    },
    ...listColumns,
  ]

  const confirmActionKey = confirmState ? `${confirmState.action}:${confirmState.row.id}` : ''
  const confirmLoading = Boolean(confirmState) && loadingAction === confirmActionKey
  const confirmTargetLabel = confirmState ? shortAdvanceId(confirmState.row.id) : ''
  const confirmTitle = confirmState
    ? confirmState.action === 'approve'
      ? `Phê duyệt ${confirmTargetLabel}`
      : confirmState.action === 'void'
        ? `Hủy ${confirmTargetLabel}`
        : `Bỏ hủy ${confirmTargetLabel}`
    : ''

  const bulkConfirmLabel =
    bulkConfirmAction === 'approve'
      ? 'Xác nhận phê duyệt'
      : bulkConfirmAction === 'void'
        ? 'Xác nhận hủy'
        : 'Xác nhận bỏ hủy'

  const bulkConfirmDescription =
    bulkConfirmAction === 'approve'
      ? `Xác nhận phê duyệt ${selectedApproveCount} khoản trả hộ đã chọn.`
      : bulkConfirmAction === 'void'
        ? `Xác nhận hủy ${selectedVoidCount} khoản trả hộ đã chọn.`
        : `Xác nhận bỏ hủy ${selectedUnvoidCount} khoản trả hộ đã chọn.`

  const bulkConfirmLoading = Boolean(bulkConfirmAction) && loadingAction === `bulk:${bulkConfirmAction}`

  return (
    <div className="page-stack advances-workspace">
      <section className="card">
        <div className="advances-create-shell">
          <div className="advances-create-main">
            <div className="advances-section-header">
              <span className="advances-section-kicker">Tạo nhanh</span>
              <h3>Tạo khoản trả hộ KH</h3>
              <p className="muted advances-section-lead">
                Ưu tiên hoàn thành MST bên bán, MST bên mua, ngày trả hộ và số tiền trước. Số chứng từ
                và ghi chú giữ ở lớp thông tin phụ để thao tác nhập nhanh không bị loãng.
              </p>
            </div>

            <div className="advances-create-toolbar" aria-label="Trạng thái tạo nhanh">
              <div className="advances-create-toolbar__status">
                <span className={canApprove ? 'pill pill-ok' : 'pill pill-warn'}>
                  {canApprove ? 'Có thể duyệt ngay' : 'Chỉ tạo nháp'}
                </span>
                <span className="muted">{quickCreateModeLabel}</span>
              </div>
              <p className="muted advances-create-tip">
                Điền 4 trường bắt buộc trước, trường phụ chỉ bổ sung khi cần đối chiếu hoặc truy vết.
              </p>
            </div>

            <div className="form-grid form-grid--advance">
              <div className="advances-field-primary">
                <LookupInput
                  label="MST bên bán"
                  value={sellerTaxCode}
                  placeholder="MST bên bán"
                  options={sellerOptions}
                  onChange={(value) => {
                    setSellerTaxCode(value)
                    setSellerQuery(value)
                    if (value.trim()) {
                      setFieldError('sellerTaxCode')
                    }
                  }}
                  onBlur={() => {
                    if (!sellerTaxCode.trim()) {
                      setFieldError('sellerTaxCode', 'Vui lòng nhập MST bên bán.')
                    }
                  }}
                  errorText={fieldErrors.sellerTaxCode}
                />
              </div>
              <div className="advances-field-primary">
                <LookupInput
                  label="MST bên mua"
                  value={customerTaxCode}
                  placeholder="MST bên mua"
                  options={customerOptions}
                  onChange={(value) => {
                    setCustomerTaxCode(value)
                    setCustomerQuery(value)
                    if (value.trim()) {
                      setFieldError('customerTaxCode')
                    }
                  }}
                  onBlur={() => {
                    if (!customerTaxCode.trim()) {
                      setFieldError('customerTaxCode', 'Vui lòng nhập MST bên mua.')
                    }
                  }}
                  errorText={fieldErrors.customerTaxCode}
                />
              </div>
              <label className={fieldErrors.advanceDate ? 'field field--error advances-field-primary' : 'field advances-field-primary'}>
                <span>Ngày trả hộ</span>
                <input
                  type="date"
                  value={advanceDate}
                  onChange={(event) => {
                    setAdvanceDate(event.target.value)
                    if (event.target.value.trim()) {
                      setFieldError('advanceDate')
                    }
                  }}
                  onBlur={() => {
                    if (!advanceDate.trim()) {
                      setFieldError('advanceDate', 'Vui lòng chọn ngày trả hộ.')
                    }
                  }}
                  aria-invalid={Boolean(fieldErrors.advanceDate)}
                />
                {fieldErrors.advanceDate && <span className="field-error">{fieldErrors.advanceDate}</span>}
              </label>
              <label className={fieldErrors.amount ? 'field field--error advances-field-primary' : 'field advances-field-primary'}>
                <span>Số tiền</span>
                <input
                  type="number"
                  min="0"
                  inputMode="decimal"
                  value={amount}
                  onChange={(event) => {
                    setAmount(event.target.value)
                    const amountValue = Number(event.target.value)
                    if (Number.isFinite(amountValue) && amountValue > 0) {
                      setFieldError('amount')
                    }
                  }}
                  onBlur={() => {
                    const amountValue = Number(amount)
                    if (!Number.isFinite(amountValue) || amountValue <= 0) {
                      setFieldError('amount', 'Số tiền không hợp lệ.')
                    }
                  }}
                  aria-invalid={Boolean(fieldErrors.amount)}
                />
                {fieldErrors.amount && <span className="field-error">{fieldErrors.amount}</span>}
              </label>
              <label className="field advances-field-secondary">
                <span>Số chứng từ</span>
                <input
                  value={advanceNo}
                  onChange={(event) => setAdvanceNo(event.target.value)}
                  placeholder="VD: CT-001"
                />
              </label>
              <label className="field field-span-full advances-field-secondary advances-field-notes">
                <span>Ghi chú</span>
                <input
                  value={description}
                  onChange={(event) => setDescription(event.target.value)}
                  placeholder="Mục đích hoặc nội dung khoản trả hộ"
                />
              </label>
            </div>
            {createError && <div className="alert alert--error" role="alert" aria-live="assertive">{createError}</div>}
            {createMessage && <div className="alert alert--success" role="alert" aria-live="assertive">{createMessage}</div>}

            <div className="advances-submit-row">
              <div className="advances-submit-row__copy">
                <strong>{canApprove ? 'Tạo xong có thể chốt ngay.' : 'Tạo nháp để chuyển người duyệt.'}</strong>
                <span className="muted">{quickCreateModeLabel}</span>
              </div>
              <div className="advances-submit-row__buttons">
                {canApprove ? (
                  <>
                    <button
                      className="btn btn-primary"
                      type="button"
                      onClick={handleCreateAndApprove}
                      disabled={loadingAction === 'create' || loadingAction === 'create-approve'}
                    >
                      {loadingAction === 'create-approve' ? 'Đang tạo & duyệt...' : 'Tạo & duyệt'}
                    </button>
                    <button
                      className="btn btn-outline"
                      type="button"
                      onClick={handleCreate}
                      disabled={loadingAction === 'create' || loadingAction === 'create-approve'}
                    >
                      {loadingAction === 'create' ? 'Đang tạo...' : 'Tạo nháp'}
                    </button>
                  </>
                ) : (
                  <button
                    className="btn btn-primary"
                    type="button"
                    onClick={handleCreate}
                    disabled={loadingAction === 'create' || loadingAction === 'create-approve'}
                  >
                    {loadingAction === 'create' ? 'Đang tạo...' : 'Tạo nháp'}
                  </button>
                )}
              </div>
            </div>
          </div>
        </div>
      </section>

      <section className="card advances-worklist-card" id="advances-worklist">
        <div className="advances-worklist-header">
          <div className="advances-section-header">
            <span className="advances-section-kicker">Danh sách xử lý</span>
            <h3>Worklist khoản trả hộ KH</h3>
            <p className="muted advances-section-lead">
              Tập trung các khoản cần theo dõi, phê duyệt, hủy hoặc bỏ hủy trên cùng một mặt bàn thao tác.
            </p>
          </div>

          <div className="advances-mini-stats" aria-label="Tóm tắt trạng thái hiện tại">
            <div className="advances-mini-stat">
              <span>Đang hiện</span>
              <strong>{listRows.length}</strong>
            </div>
            <div className="advances-mini-stat">
              <span>Nháp</span>
              <strong>{visibleDraftCount}</strong>
            </div>
            <div className="advances-mini-stat">
              <span>Đã duyệt / tất toán</span>
              <strong>{visibleApprovedCount}</strong>
            </div>
            <div className="advances-mini-stat">
              <span>Đã hủy</span>
              <strong>{visibleVoidCount}</strong>
            </div>
          </div>
        </div>

        <div className="advances-filter-shell">
          <div className="advances-filter-head">
            <div>
              <p className="filters-block__title">Bộ lọc vận hành</p>
              <p className="muted">Lọc nhanh theo đối tượng, trạng thái và chỉ mở rộng khi cần truy vết sâu hơn.</p>
            </div>
            {listLoading && <span className="muted">Đang tải...</span>}
          </div>

          <div className="filters-grid">
            <LookupInput
              label="Lọc MST bên bán"
              value={listSeller}
              placeholder="Tất cả"
              options={sellerOptions}
              onChange={(value) => {
                setListSeller(value)
                setSellerQuery(value)
                setListPage(1)
              }}
            />
            <LookupInput
              label="Lọc MST bên mua"
              value={listCustomer}
              placeholder="Tất cả"
              options={customerOptions}
              onChange={(value) => {
                setListCustomer(value)
                setCustomerQuery(value)
                setListPage(1)
              }}
            />
            <label className="field">
              <span>Trạng thái</span>
              <select
                value={listStatus}
                onChange={(event) => {
                  const next = event.target.value
                  setListStatus(next)
                  setListPage(1)
                  storeFilter(ADVANCES_STATUS_KEY, next)
                }}
              >
                <option value="">Tất cả</option>
                <option value="DRAFT">{advanceStatusLabels.DRAFT}</option>
                <option value="APPROVED">{advanceStatusLabels.APPROVED}</option>
                <option value="PAID">{advanceStatusLabels.PAID}</option>
                <option value="VOID">{advanceStatusLabels.VOID}</option>
              </select>
            </label>
          </div>

          <div className="filters-actions">
            <button
              className="btn btn-ghost"
              type="button"
              onClick={() => setShowAdvancedFilters((prev) => !prev)}
            >
              {showAdvancedFilters ? 'Ẩn lọc nâng cao' : 'Bộ lọc nâng cao'}
            </button>
            {showAdvancedFilters && (
              <button className="btn btn-ghost" type="button" onClick={resetAdvancedFilters}>
                Xóa lọc nâng cao
              </button>
            )}
          </div>

          {showAdvancedFilters && (
            <div className="filters-grid filters-grid--compact">
              <label className="field">
                <span>Số chứng từ</span>
                <input
                  value={listAdvanceNo}
                  onChange={(event) => {
                    setListAdvanceNo(event.target.value)
                    setListPage(1)
                  }}
                  placeholder="VD: CT-001"
                />
              </label>
              <label className="field">
                <span>Từ ngày</span>
                <input
                  type="date"
                  value={listFromDate}
                  onChange={(event) => {
                    setListFromDate(event.target.value)
                    setListPage(1)
                  }}
                />
              </label>
              <label className="field">
                <span>Đến ngày</span>
                <input
                  type="date"
                  value={listToDate}
                  onChange={(event) => {
                    setListToDate(event.target.value)
                    setListPage(1)
                  }}
                />
              </label>
              <label className="field">
                <span>Số tiền từ</span>
                <input
                  type="number"
                  min="0"
                  inputMode="decimal"
                  value={listAmountMin}
                  onChange={(event) => {
                    setListAmountMin(event.target.value)
                    setListPage(1)
                  }}
                />
              </label>
              <label className="field">
                <span>Số tiền đến</span>
                <input
                  type="number"
                  min="0"
                  inputMode="decimal"
                  value={listAmountMax}
                  onChange={(event) => {
                    setListAmountMax(event.target.value)
                    setListPage(1)
                  }}
                />
              </label>
              <label className="field">
                <span>Nguồn dữ liệu</span>
                <select
                  value={listSource}
                  onChange={(event) => {
                    setListSource(event.target.value)
                    setListPage(1)
                  }}
                >
                  <option value="">Tất cả</option>
                  <option value="MANUAL">Thủ công</option>
                  <option value="IMPORT">Import</option>
                </select>
              </label>
            </div>
          )}
        </div>

        {listError && <div className="alert alert--error" role="alert" aria-live="assertive">{listError}</div>}
        {actionError && <div className="alert alert--error" role="alert" aria-live="assertive">{actionError}</div>}
        {actionMessage && <div className="alert alert--success" role="alert" aria-live="assertive">{actionMessage}</div>}

        <div className="advances-selection-bar">
          <div className="advances-selection-state">
            <strong>
              {hasSelectedRows
                ? `Đã chọn ${selectedAdvanceIds.length}/${selectableRowIds.length} khoản trả hộ.`
                : 'Chưa chọn khoản trả hộ nào.'}
            </strong>
            <span className="muted">
              {hasSelectedRows
                ? 'Bulk action chỉ áp dụng cho các dòng đủ điều kiện theo trạng thái hiện tại.'
                : 'Chọn nhiều dòng để mở phê duyệt, hủy hoặc bỏ hủy hàng loạt.'}
            </span>
          </div>

          <div className="advances-selection-actions">
            <button
              className="btn btn-ghost"
              type="button"
              onClick={() => setSelectedAdvanceIds(selectableRowIds)}
              disabled={selectableRowIds.length === 0 || bulkConfirmLoading}
            >
              Chọn tất cả
            </button>
            <button
              className="btn btn-ghost"
              type="button"
              onClick={() => setSelectedAdvanceIds([])}
              disabled={selectedAdvanceIds.length === 0 || bulkConfirmLoading}
            >
              Bỏ chọn
            </button>
          </div>

          {hasSelectedRows ? (
            <div className="advances-selection-actions advances-selection-actions--bulk">
              <button
                className="btn btn-outline"
                type="button"
                onClick={() => handleBulkAction('approve')}
                disabled={selectedApproveCount === 0 || bulkConfirmLoading}
              >
                Phê duyệt đã chọn ({selectedApproveCount})
              </button>
              <button
                className="btn btn-outline-danger"
                type="button"
                onClick={() => handleBulkAction('void')}
                disabled={selectedVoidCount === 0 || bulkConfirmLoading}
              >
                Hủy đã chọn ({selectedVoidCount})
              </button>
              <button
                className="btn btn-outline"
                type="button"
                onClick={() => handleBulkAction('unvoid')}
                disabled={selectedUnvoidCount === 0 || bulkConfirmLoading}
              >
                Bỏ hủy đã chọn ({selectedUnvoidCount})
              </button>
            </div>
          ) : (
            <span className="advances-empty-selection">Danh sách vẫn có thể sửa hoặc duyệt trực tiếp trên từng dòng.</span>
          )}
        </div>

        <div className="advances-table-wrap">
          <DataTable
            columns={tableColumns}
            rows={listRows}
            getRowKey={(row) => row.id}
            minWidth="920px"
            emptyMessage={listLoading ? 'Đang tải...' : 'Chưa có khoản trả hộ KH.'}
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
        </div>
      </section>

      <ActionConfirmModal
        isOpen={Boolean(confirmState)}
        title={confirmTitle}
        description={
          confirmState?.action === 'void'
            ? `Xác nhận hủy khoản trả hộ ${confirmTargetLabel}.`
            : confirmState?.action === 'approve'
              ? `Xác nhận phê duyệt khoản trả hộ ${confirmTargetLabel}.`
              : `Xác nhận bỏ hủy khoản trả hộ ${confirmTargetLabel}.`
        }
        confirmLabel={
          confirmState?.action === 'approve'
            ? 'Xác nhận phê duyệt'
            : confirmState?.action === 'void'
              ? 'Xác nhận hủy'
              : 'Xác nhận bỏ hủy'
        }
        reasonRequired={confirmState?.action === 'void'}
        reasonLabel="Lý do hủy"
        reasonPlaceholder="Nhập lý do hủy khoản trả hộ"
        showOverrideOption
        overrideReasonPlaceholder="Nhập lý do vượt khóa kỳ"
        loading={confirmLoading}
        error={confirmError}
        tone={confirmState?.action === 'void' ? 'danger' : 'primary'}
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
          bulkConfirmAction === 'approve'
            ? 'Phê duyệt khoản trả hộ đã chọn'
            : bulkConfirmAction === 'void'
              ? 'Hủy khoản trả hộ đã chọn'
              : 'Bỏ hủy khoản trả hộ đã chọn'
        }
        description={bulkConfirmDescription}
        confirmLabel={bulkConfirmLabel}
        reasonRequired={bulkConfirmAction === 'void'}
        reasonLabel="Lý do hủy"
        reasonPlaceholder="Nhập lý do hủy khoản trả hộ"
        showOverrideOption
        overrideReasonPlaceholder="Nhập lý do vượt khóa kỳ"
        loading={bulkConfirmLoading}
        error={bulkConfirmError}
        tone={bulkConfirmAction === 'void' ? 'danger' : 'primary'}
        onClose={() => {
          if (bulkConfirmLoading) return
          setBulkConfirmAction(null)
          setBulkConfirmError(null)
        }}
        onConfirm={handleBulkConfirmAction}
      />
    </div>
  )
}
