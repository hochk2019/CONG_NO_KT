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

  const handleApprove = async (row: AdvanceListItem) => {
    if (!token || !row.canManage) return
    resetMessages()
    const overrideOptions = collectOverrideOptions(`Phê duyệt ${shortAdvanceId(row.id)}`)
    if (!overrideOptions) {
      return
    }
    setLoadingAction(`approve:${row.id}`)
    try {
      await approveAdvance(token, row.id, {
        version: row.version,
        overridePeriodLock: overrideOptions.overridePeriodLock,
        overrideReason: overrideOptions.overrideReason,
      })
      setActionMessage(`Đã phê duyệt khoản trả hộ ${shortAdvanceId(row.id)}.`)
      setListReload((value) => value + 1)
    } catch (err) {
      if (err instanceof ApiError) {
        setActionError(err.message)
      } else {
        setActionError('Không phê duyệt được khoản trả hộ.')
      }
    } finally {
      setLoadingAction('')
    }
  }

  const handleVoid = async (row: AdvanceListItem) => {
    if (!token || !row.canManage) return
    resetMessages()
    const status = row.status.toUpperCase()
    if (status === 'PAID') {
      setActionError(
        'Khoản trả hộ đã tất toán. Muốn hủy cần hoàn tác phân bổ/phiếu thu liên quan trước.',
      )
      return
    }

    const confirmed = window.confirm(
      `Bạn chắc chắn muốn hủy khoản trả hộ ${shortAdvanceId(row.id)}?`,
    )
    if (!confirmed) {
      return
    }

    const reasonInput = window.prompt(
      `Nhập lý do hủy khoản trả hộ ${shortAdvanceId(row.id)}:`,
      '',
    )
    if (reasonInput === null) {
      return
    }
    const reason = reasonInput.trim()
    if (!reason) {
      setActionError('Vui lòng nhập lý do hủy.')
      return
    }

    const overrideOptions = collectOverrideOptions(`Hủy ${shortAdvanceId(row.id)}`)
    if (!overrideOptions) {
      return
    }

    setLoadingAction(`void:${row.id}`)
    try {
      await voidAdvance(token, row.id, {
        reason,
        version: row.version,
        overridePeriodLock: overrideOptions.overridePeriodLock,
        overrideReason: overrideOptions.overrideReason,
      })
      setActionMessage(`Đã hủy khoản trả hộ ${shortAdvanceId(row.id)}.`)
      setListReload((value) => value + 1)
    } catch (err) {
      if (err instanceof ApiError) {
        setActionError(err.message)
      } else {
        setActionError('Không hủy được khoản trả hộ.')
      }
    } finally {
      setLoadingAction('')
    }
  }

  const handleUnvoid = async (row: AdvanceListItem) => {
    if (!token || !row.canManage) return
    resetMessages()
    const confirmed = window.confirm(
      `Bạn chắc chắn muốn bỏ hủy khoản trả hộ ${shortAdvanceId(row.id)}?`,
    )
    if (!confirmed) {
      return
    }

    const overrideOptions = collectOverrideOptions(`Bỏ hủy ${shortAdvanceId(row.id)}`)
    if (!overrideOptions) {
      return
    }

    setLoadingAction(`unvoid:${row.id}`)
    try {
      await unvoidAdvance(token, row.id, {
        version: row.version,
        overridePeriodLock: overrideOptions.overridePeriodLock,
        overrideReason: overrideOptions.overrideReason,
      })
      setActionMessage(`Đã bỏ hủy khoản trả hộ ${shortAdvanceId(row.id)}.`)
      setListReload((value) => value + 1)
    } catch (err) {
      if (err instanceof ApiError) {
        setActionError(err.message)
      } else {
        setActionError('Không bỏ hủy được khoản trả hộ.')
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

  return (
    <div className="page-stack">
      <div className="page-header">
        <div>
          <h2>Khoản trả hộ KH</h2>
          <p className="muted">
            Tạo nhanh khoản trả hộ, theo dõi trạng thái và xử lý trực tiếp trên danh sách. Nếu có quyền, dùng
            "Tạo & duyệt" để phê duyệt ngay.
          </p>
        </div>
        <div className="header-actions">
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

      <section className="card">
        <h3>Tạo khoản trả hộ KH</h3>
        <div className="form-grid form-grid--advance">
          <LookupInput
            label="MST bên bán"
            value={sellerTaxCode}
            placeholder="MST bên bán"
            options={sellerOptions}
            helpText="Gõ để tìm và chọn MST."
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
          <LookupInput
            label="MST bên mua"
            value={customerTaxCode}
            placeholder="MST bên mua"
            options={customerOptions}
            helpText="Gõ để tìm và chọn MST."
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
          <label className="field">
            <span>Số chứng từ</span>
            <input
              value={advanceNo}
              onChange={(event) => setAdvanceNo(event.target.value)}
              placeholder="VD: CT-001"
            />
            <span className="muted">Không bắt buộc.</span>
          </label>
          <label className={fieldErrors.advanceDate ? 'field field--error' : 'field'}>
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
          <label className={fieldErrors.amount ? 'field field--error' : 'field'}>
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
          <label className="field field-span-full">
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
      </section>

      <section className="card">
        <div className="card-row">
          <div>
            <h3>Danh sách khoản trả hộ KH</h3>
            <p className="muted">Phê duyệt/hủy và sửa ghi chú trực tiếp trên từng dòng.</p>
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

        {listError && <div className="alert alert--error" role="alert" aria-live="assertive">{listError}</div>}
        {actionError && <div className="alert alert--error" role="alert" aria-live="assertive">{actionError}</div>}
        {actionMessage && <div className="alert alert--success" role="alert" aria-live="assertive">{actionMessage}</div>}

        <DataTable
          columns={listColumns}
          rows={listRows}
          getRowKey={(row) => row.id}
          minWidth="1200px"
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
      </section>
    </div>
  )
}
