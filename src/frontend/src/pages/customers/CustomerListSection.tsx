import { useCallback, useEffect, useMemo, useState } from 'react'
import type { CustomerDetail, CustomerListItem } from '../../api/customers'
import { fetchCustomerDetail, fetchCustomers, updateCustomer } from '../../api/customers'
import { ApiError } from '../../api/client'
import {
  fetchOwnerLookup,
  fetchUserLookup,
  mapOwnerOptions,
  type LookupOption,
} from '../../api/lookups'
import DataTable from '../../components/DataTable'
import { useDebouncedValue } from '../../hooks/useDebouncedValue'
import { formatMoney } from '../../utils/format'
import CustomerEditModal from './CustomerEditModal'
type CustomerListSectionProps = {
  token: string
  canManageCustomers: boolean
  selectedTaxCode: string | null
  selectedName: string
  onSelectCustomer: (row: CustomerListItem) => void
  onUnauthorized?: () => void
}
const customerStatusLabels: Record<string, string> = {
  ACTIVE: 'Đang hoạt động',
  INACTIVE: 'Ngừng hoạt động',
}
const DEFAULT_PAGE_SIZE = 10
const PAGE_SIZE_STORAGE_KEY = 'pref.table.pageSize'
const CUSTOMER_STATUS_KEY = 'pref.customers.status'

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

export default function CustomerListSection({
  token,
  canManageCustomers,
  selectedTaxCode,
  selectedName,
  onSelectCustomer,
  onUnauthorized,
}: CustomerListSectionProps) {
  const [rows, setRows] = useState<CustomerListItem[]>([])
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(() => getStoredPageSize())
  const [total, setTotal] = useState(0)
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebouncedValue(search, 400)
  const [status, setStatus] = useState(() => getStoredFilter(CUSTOMER_STATUS_KEY))
  const [ownerId, setOwnerId] = useState('')
  const [ownerOptions, setOwnerOptions] = useState<LookupOption[]>([])
  const [ownerLoading, setOwnerLoading] = useState(false)
  const [ownerError, setOwnerError] = useState<string | null>(null)
  const [managerOptions, setManagerOptions] = useState<LookupOption[]>([])
  const [managerLoading, setManagerLoading] = useState(false)
  const [managerError, setManagerError] = useState<string | null>(null)
  const [listLoading, setListLoading] = useState(false)
  const [listError, setListError] = useState<string | null>(null)
  const [listErrorStatus, setListErrorStatus] = useState<number | null>(null)
  const [listReload, setListReload] = useState(0)
  const [statusActionLoading, setStatusActionLoading] = useState<string | null>(null)
  const [statusActionError, setStatusActionError] = useState<string | null>(null)

  const [detail, setDetail] = useState<CustomerDetail | null>(null)
  const [detailLoading, setDetailLoading] = useState(false)
  const [detailError, setDetailError] = useState<string | null>(null)
  const [isEditOpen, setIsEditOpen] = useState(false)
  const [editName, setEditName] = useState('')
  const [editAddress, setEditAddress] = useState('')
  const [editEmail, setEditEmail] = useState('')
  const [editPhone, setEditPhone] = useState('')
  const [editStatus, setEditStatus] = useState('ACTIVE')
  const [editPaymentTermsDays, setEditPaymentTermsDays] = useState('0')
  const [editCreditLimit, setEditCreditLimit] = useState('')
  const [editOwnerId, setEditOwnerId] = useState('')
  const [editManagerId, setEditManagerId] = useState('')
  const [editLoading, setEditLoading] = useState(false)
  const [editSuccess, setEditSuccess] = useState<string | null>(null)
  const [editError, setEditError] = useState<string | null>(null)
  const [copyMessage, setCopyMessage] = useState<string | null>(null)

  useEffect(() => {
    if (!token) return
    let isActive = true

    const load = async () => {
      setListLoading(true)
      setListError(null)
      setListErrorStatus(null)
      try {
        const result = await fetchCustomers({
          token,
          search: debouncedSearch.trim() || undefined,
          ownerId: ownerId || undefined,
          status: status || undefined,
          page,
          pageSize,
        })
        if (!isActive) return
        setRows(result.items)
        setTotal(result.total)
      } catch (err) {
        if (!isActive) return
        if (err instanceof ApiError) {
          if (err.status === 401) {
            setListError('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.')
            setListErrorStatus(err.status)
          } else {
            setListError(err.message)
            setListErrorStatus(err.status)
          }
        } else {
          setListError('Không tải được danh sách khách hàng.')
        }
      } finally {
        if (isActive) {
          setListLoading(false)
        }
      }
    }

    load()
    return () => {
      isActive = false
    }
  }, [token, debouncedSearch, status, ownerId, page, pageSize, listReload])
  useEffect(() => {
    if (!token) return
    let isActive = true

    const load = async () => {
      setOwnerLoading(true)
      setOwnerError(null)
      try {
        const result = await fetchOwnerLookup({ token })
        if (!isActive) return
        setOwnerOptions(mapOwnerOptions(result))
      } catch (err) {
        if (!isActive) return
        if (err instanceof ApiError) {
          if (err.status === 401) {
            setOwnerError('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.')
          } else if (err.status === 403) {
            setOwnerError('Bạn không có quyền xem danh sách phụ trách.')
          } else {
            setOwnerError(err.message)
          }
        } else {
          setOwnerError('Không tải được danh sách phụ trách.')
        }
      } finally {
        if (isActive) {
          setOwnerLoading(false)
        }
      }
    }

    load()
    return () => {
      isActive = false
    }
  }, [token])
  useEffect(() => {
    if (!token || !isEditOpen) return
    let isActive = true

    const load = async () => {
      setManagerLoading(true)
      setManagerError(null)
      try {
        const result = await fetchUserLookup({ token })
        if (!isActive) return
        setManagerOptions(mapOwnerOptions(result))
      } catch (err) {
        if (!isActive) return
        if (err instanceof ApiError) {
          if (err.status === 401) {
            setManagerError('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.')
          } else if (err.status === 403) {
            setManagerError('Bạn không có quyền xem danh sách quản lý.')
          } else {
            setManagerError(err.message)
          }
        } else {
          setManagerError('Không tải được danh sách quản lý.')
        }
      } finally {
        if (isActive) {
          setManagerLoading(false)
        }
      }
    }

    load()
    return () => {
      isActive = false
    }
  }, [token, isEditOpen])
  useEffect(() => {
    if (!token || !selectedTaxCode) return
    let isActive = true

    const load = async () => {
      setDetailLoading(true)
      setDetailError(null)
      try {
        const result = await fetchCustomerDetail(token, selectedTaxCode)
        if (!isActive) return
        setDetail(result)
      } catch (err) {
        if (!isActive) return
        if (err instanceof ApiError) {
          if (err.status === 401) {
            setDetailError('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.')
          } else {
            setDetailError(err.message)
          }
        } else {
          setDetailError('Không tải được chi tiết khách hàng.')
        }
      } finally {
        if (isActive) {
          setDetailLoading(false)
        }
      }
    }

    load()
    return () => {
      isActive = false
    }
  }, [token, selectedTaxCode])
  useEffect(() => {
    if (!detail || !isEditOpen) return
    setEditName(detail.name ?? '')
    setEditAddress(detail.address ?? '')
    setEditEmail(detail.email ?? '')
    setEditPhone(detail.phone ?? '')
    setEditStatus(detail.status ?? 'ACTIVE')
    setEditPaymentTermsDays(String(detail.paymentTermsDays ?? 0))
    setEditCreditLimit(
      detail.creditLimit === null || detail.creditLimit === undefined ? '' : String(detail.creditLimit),
    )
    setEditOwnerId(detail.ownerId ?? '')
    setEditManagerId(detail.managerId ?? '')
  }, [detail, isEditOpen])

  const hasFilters = Boolean(search.trim() || status || ownerId)

  const handleClearFilters = useCallback(() => {
    setSearch('')
    setStatus('')
    setOwnerId('')
    setPage(1)
    storeFilter(CUSTOMER_STATUS_KEY, '')
  }, [])

  const handleSelectCustomer = useCallback(
    (row: CustomerListItem) => {
      onSelectCustomer(row)
      if (typeof window !== 'undefined') {
        requestAnimationFrame(() => {
          document.getElementById('customer-transactions')?.scrollIntoView({
            behavior: 'smooth',
            block: 'start',
          })
        })
      }
    },
    [onSelectCustomer],
  )

  const handleOpenEdit = useCallback(
    (row: CustomerListItem) => {
      onSelectCustomer(row)
      setEditSuccess(null)
      setEditError(null)
      setIsEditOpen(true)
    },
    [onSelectCustomer],
  )

  const handleCloseEdit = useCallback(() => {
    setIsEditOpen(false)
    setCopyMessage(null)
  }, [])

  const handleToggleCustomerStatus = useCallback(
    async (row: CustomerListItem) => {
      if (!token) return
      if (!canManageCustomers) {
        setStatusActionError('Bạn không có quyền thay đổi trạng thái khách hàng.')
        return
      }

      const nextStatus = row.status === 'ACTIVE' ? 'INACTIVE' : 'ACTIVE'
      if (nextStatus === 'INACTIVE' && row.currentBalance > 0) {
        setStatusActionError('Không thể ẩn khách hàng đang còn dư nợ.')
        return
      }
      const confirmMessage =
        nextStatus === 'INACTIVE'
          ? `Xác nhận ẩn khách hàng "${row.name}"?`
          : `Khôi phục khách hàng "${row.name}"?`
      if (!window.confirm(confirmMessage)) return

      setStatusActionLoading(row.taxCode)
      setStatusActionError(null)
      try {
        const sourceDetail =
          detail && detail.taxCode === row.taxCode ? detail : await fetchCustomerDetail(token, row.taxCode)

        await updateCustomer(token, row.taxCode, {
          name: sourceDetail.name,
          address: sourceDetail.address ?? null,
          email: sourceDetail.email ?? null,
          phone: sourceDetail.phone ?? null,
          status: nextStatus,
          paymentTermsDays: sourceDetail.paymentTermsDays,
          creditLimit: sourceDetail.creditLimit ?? null,
          ownerId: sourceDetail.ownerId ?? null,
          managerId: sourceDetail.managerId ?? null,
        })

        if (detail && detail.taxCode === row.taxCode) {
          setDetail({ ...sourceDetail, status: nextStatus })
        }
        setListReload((value) => value + 1)
      } catch (err) {
        if (err instanceof ApiError) {
          setStatusActionError(err.message)
        } else {
          setStatusActionError('Không cập nhật được trạng thái khách hàng.')
        }
      } finally {
        setStatusActionLoading(null)
      }
    },
    [token, canManageCustomers, detail],
  )

  const handleSaveCustomer = useCallback(async () => {
    if (!token || !selectedTaxCode) return
    if (!canManageCustomers) {
      setEditError('Bạn không có quyền cập nhật khách hàng.')
      return
    }

    const name = editName.trim()
    if (!name) {
      setEditError('Vui lòng nhập tên khách hàng.')
      return
    }

    const paymentTerms = Number(editPaymentTermsDays)
    if (!Number.isFinite(paymentTerms) || paymentTerms < 0) {
      setEditError('Điều khoản thanh toán không hợp lệ.')
      return
    }

    const creditLimit = editCreditLimit.trim()
    let creditValue: number | null = null
    if (creditLimit) {
      const parsed = Number(creditLimit)
      if (!Number.isFinite(parsed) || parsed < 0) {
        setEditError('Hạn mức tín dụng không hợp lệ.')
        return
      }
      creditValue = parsed
    }

    setEditLoading(true)
    setEditError(null)
    setEditSuccess(null)
    try {
      await updateCustomer(token, selectedTaxCode, {
        name,
        address: editAddress.trim() || null,
        email: editEmail.trim() || null,
        phone: editPhone.trim() || null,
        status: editStatus,
        paymentTermsDays: paymentTerms,
        creditLimit: creditValue,
        ownerId: editOwnerId || null,
        managerId: editManagerId || null,
      })
      setEditSuccess('Đã cập nhật thông tin khách hàng.')
      const refreshed = await fetchCustomerDetail(token, selectedTaxCode)
      setDetail(refreshed)
    } catch (err) {
      if (err instanceof ApiError) {
        setEditError(err.message)
      } else {
        setEditError('Không cập nhật được thông tin khách hàng.')
      }
    } finally {
      setEditLoading(false)
    }
  }, [
    token,
    selectedTaxCode,
    canManageCustomers,
    editName,
    editAddress,
    editEmail,
    editPhone,
    editStatus,
    editPaymentTermsDays,
    editCreditLimit,
    editOwnerId,
    editManagerId,
  ])

  const handleCopy = useCallback(async (value: string, label: string) => {
    try {
      await navigator.clipboard.writeText(value)
      setCopyMessage(`Đã sao chép ${label}.`)
    } catch {
      setCopyMessage('Không thể sao chép, vui lòng thử lại.')
    }
  }, [])

  const listStats = useMemo(() => {
    return rows.reduce(
      (acc, row) => {
        if (row.status === 'ACTIVE') acc.activeCount += 1
        if (row.status === 'INACTIVE') acc.inactiveCount += 1
        acc.pageBalance += row.currentBalance
        return acc
      },
      { activeCount: 0, inactiveCount: 0, pageBalance: 0 },
    )
  }, [rows])

  const listColumns = useMemo(
    () => [
      {
        key: 'taxCode',
        label: 'MST',
        width: '140px',
        render: (row: CustomerListItem) => <span className="cell-wrap">{row.taxCode}</span>,
      },
      {
        key: 'name',
        label: 'Khách hàng',
        width: '320px',
        render: (row: CustomerListItem) => <span className="cell-wrap">{row.name}</span>,
      },
      {
        key: 'ownerName',
        label: 'Phụ trách',
        width: '180px',
        render: (row: CustomerListItem) =>
          row.ownerName ? <span className="cell-wrap">{row.ownerName}</span> : <span className="muted">-</span>,
      },
      {
        key: 'currentBalance',
        label: 'Dư nợ',
        width: '240px',
        align: 'right' as const,
        render: (row: CustomerListItem) => formatMoney(row.currentBalance),
      },
      {
        key: 'actions',
        label: 'Thao tác',
        width: '170px',
        render: (row: CustomerListItem) => (
          <button
            className="btn btn-ghost btn-table"
            type="button"
            onClick={() => handleSelectCustomer(row)}
          >
            Xem
          </button>
        ),
      },
      {
        key: 'edit',
        label: 'Sửa TT',
        width: '110px',
        align: 'center' as const,
        render: (row: CustomerListItem) => (
          <button
            className="btn btn-ghost btn-table"
            type="button"
            onClick={() => handleOpenEdit(row)}
            aria-label={`Sửa thông tin ${row.name}`}
            title={canManageCustomers ? 'Sửa thông tin khách hàng' : 'Bạn không có quyền sửa'}
            disabled={!canManageCustomers}
          >
            <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true">
              <path
                d="M4 20h4.2l9.5-9.5-4.2-4.2L4 15.8V20zm14.7-11.8 1.1-1.1a1.5 1.5 0 0 0 0-2.1l-1.9-1.9a1.5 1.5 0 0 0-2.1 0l-1.1 1.1 4.1 4z"
                fill="currentColor"
              />
            </svg>
          </button>
        ),
      },
      {
        key: 'status',
        label: 'Trạng thái',
        width: '160px',
        align: 'center' as const,
        render: (row: CustomerListItem) => {
          const className = row.status === 'ACTIVE' ? 'pill pill-ok' : 'pill pill-warn'
          return (
            <span className={`${className} pill-wrap`}>
              {customerStatusLabels[row.status] ?? row.status}
            </span>
          )
        },
      },
      {
        key: 'visibility',
        label: 'Ẩn/Hiện',
        width: '140px',
        align: 'center' as const,
        render: (row: CustomerListItem) => {
          const isInactive = row.status === 'INACTIVE'
          const label = isInactive ? 'Bật lại' : 'Ẩn'
          const className = isInactive ? 'btn btn-outline btn-table' : 'btn btn-outline-danger btn-table'
          const hasBalance = row.currentBalance > 0
          const isHideAction = !isInactive
          const disabled =
            !canManageCustomers || statusActionLoading === row.taxCode || (isHideAction && hasBalance)
          const title = !canManageCustomers
            ? 'Bạn không có quyền thay đổi trạng thái'
            : isHideAction && hasBalance
            ? 'Không thể ẩn khách hàng đang còn dư nợ'
            : undefined
          return (
            <button
              className={className}
              type="button"
              disabled={disabled}
              onClick={() => handleToggleCustomerStatus(row)}
              title={title}
            >
              {statusActionLoading === row.taxCode ? 'Đang xử lý...' : label}
            </button>
          )
        },
      },
    ],
    [
      handleSelectCustomer,
      handleOpenEdit,
      canManageCustomers,
      handleToggleCustomerStatus,
      statusActionLoading,
    ],
  )

  return (
    <>
      <div className="page-header">
        <div>
          <h2>Tra cứu MST và công nợ</h2>
          <p className="muted">
            Tìm theo MST hoặc tên, chọn khách hàng để xem hóa đơn, khoản trả hộ KH và phiếu thu.
          </p>
        </div>
      </div>
      <div className="stat-grid">
        <div className="stat-card">
          <div className="stat-card__label">Tổng khách hàng</div>
          <div className="stat-card__value">{total}</div>
          <div className="stat-card__meta">Toàn bộ hệ thống</div>
        </div>
        <div className="stat-card">
          <div className="stat-card__label">Đang hoạt động</div>
          <div className="stat-card__value">{listStats.activeCount}</div>
          <div className="stat-card__meta">Theo trang hiện tại</div>
        </div>
        <div className="stat-card">
          <div className="stat-card__label">Ngừng hoạt động</div>
          <div className="stat-card__value">{listStats.inactiveCount}</div>
          <div className="stat-card__meta">Theo trang hiện tại</div>
        </div>
        <div className="stat-card stat-card--danger">
          <div className="stat-card__label">Dư nợ đang hiển thị</div>
          <div className="stat-card__value">{formatMoney(listStats.pageBalance)}</div>
          <div className="stat-card__meta">Theo trang hiện tại</div>
        </div>
      </div>

      <section className="card">
        <div className="card-row">
          <div>
            <h3>Danh sách khách hàng</h3>
            <p className="muted">Tìm theo MST hoặc tên, xem nhanh dư nợ hiện tại.</p>
          </div>
          <div className="list-actions">
            {listLoading && <span className="muted">Đang tải...</span>}
            {ownerLoading && <span className="muted">Đang tải phụ trách...</span>}
            {hasFilters && (
              <button className="btn btn-outline btn-table" type="button" onClick={handleClearFilters}>
                Xóa lọc
              </button>
            )}
          </div>
        </div>
        <div className="filters-grid">
          <label className="field">
            <span>Tìm kiếm</span>
            <input
              value={search}
              onChange={(event) => {
                setSearch(event.target.value)
                setPage(1)
              }}
              placeholder="MST hoặc tên"
            />
          </label>
          <label className="field">
            <span>Trạng thái</span>
            <select
              value={status}
              onChange={(event) => {
                const next = event.target.value
                setStatus(next)
                setPage(1)
                storeFilter(CUSTOMER_STATUS_KEY, next)
              }}
            >
              <option value="">Tất cả</option>
              <option value="ACTIVE">Đang hoạt động</option>
              <option value="INACTIVE">Ngừng hoạt động</option>
            </select>
          </label>
          <label className="field">
            <span>Phụ trách</span>
            <select
              value={ownerId}
              onChange={(event) => {
                setOwnerId(event.target.value)
                setPage(1)
              }}
            >
              <option value="">Tất cả</option>
              {ownerOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
            <span className="muted">Danh sách lấy từ Admin &gt; Người dùng.</span>
          </label>
        </div>
        <div className="filters-actions">
          {hasFilters ? (
            <div className="filter-chips">
              {search.trim() && <span className="filter-chip">Từ khóa: {search.trim()}</span>}
              {status && (
                <span className="filter-chip">
                  Trạng thái: {customerStatusLabels[status] ?? status}
                </span>
              )}
              {ownerId && (
                <span className="filter-chip">
                  Phụ trách: {ownerOptions.find((option) => option.value === ownerId)?.label ?? ownerId}
                </span>
              )}
            </div>
          ) : (
            <span className="muted">Bạn có thể lọc theo trạng thái hoặc phụ trách.</span>
          )}
        </div>
        {ownerError && <div className="alert alert--error" role="alert">{ownerError}</div>}
        {listError && (
          <div className="alert alert--error" role="alert">
            <div>{listError}</div>
            {listErrorStatus === 401 && (
              <button className="btn btn-outline btn-table" type="button" onClick={() => onUnauthorized?.()}>
                Đăng nhập lại
              </button>
            )}
          </div>
        )}
        {statusActionError && (
          <div className="alert alert--error" role="alert">
            {statusActionError}
          </div>
        )}
        <DataTable
          columns={listColumns}
          rows={rows}
          getRowKey={(row) => row.taxCode}
          getRowClassName={(row) => (row.taxCode === selectedTaxCode ? 'table-row--selected' : undefined)}
          minWidth="1200px"
          emptyMessage={listLoading ? 'Đang tải...' : 'Không có khách hàng.'}
          pagination={{ page, pageSize, total }}
          onPageChange={setPage}
          onPageSizeChange={(size) => {
            storePageSize(size)
            setPageSize(size)
            setPage(1)
          }}
        />
      </section>

      <CustomerEditModal
        open={isEditOpen}
        onClose={handleCloseEdit}
        onCopy={handleCopy}
        canManageCustomers={canManageCustomers}
        selectedName={selectedName}
        selectedTaxCode={selectedTaxCode}
        detail={detail}
        detailLoading={detailLoading}
        detailError={detailError}
        copyMessage={copyMessage}
        ownerOptions={ownerOptions}
        managerOptions={managerOptions}
        ownerLoading={ownerLoading}
        managerLoading={managerLoading}
        ownerError={ownerError}
        managerError={managerError}
        editName={editName}
        editAddress={editAddress}
        editEmail={editEmail}
        editPhone={editPhone}
        editStatus={editStatus}
        editPaymentTermsDays={editPaymentTermsDays}
        editCreditLimit={editCreditLimit}
        editOwnerId={editOwnerId}
        editManagerId={editManagerId}
        setEditName={setEditName}
        setEditAddress={setEditAddress}
        setEditEmail={setEditEmail}
        setEditPhone={setEditPhone}
        setEditStatus={setEditStatus}
        setEditPaymentTermsDays={setEditPaymentTermsDays}
        setEditCreditLimit={setEditCreditLimit}
        setEditOwnerId={setEditOwnerId}
        setEditManagerId={setEditManagerId}
        editLoading={editLoading}
        editSuccess={editSuccess}
        editError={editError}
        statusLabels={customerStatusLabels}
        onSave={handleSaveCustomer}
        onReset={() => {
          if (!detail) return
          setEditName(detail.name ?? '')
          setEditAddress(detail.address ?? '')
          setEditEmail(detail.email ?? '')
          setEditPhone(detail.phone ?? '')
          setEditStatus(detail.status ?? 'ACTIVE')
          setEditPaymentTermsDays(String(detail.paymentTermsDays ?? 0))
          setEditCreditLimit(
            detail.creditLimit === null || detail.creditLimit === undefined ? '' : String(detail.creditLimit),
          )
          setEditOwnerId(detail.ownerId ?? '')
          setEditManagerId(detail.managerId ?? '')
          setEditSuccess(null)
          setEditError(null)
        }}
      />
    </>
  )
}
