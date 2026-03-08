import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  type CustomerHeldCredit,
  fetchCustomerHeldCredits,
} from '../../api/customers'
import { ApiError } from '../../api/client'
import DataTable from '../../components/DataTable'
import { formatMoney } from '../../utils/format'
import CustomerHeldCreditModals from './CustomerHeldCreditModals'
import {
  CUSTOMER_HELD_CREDIT_STATUS_KEY,
  heldCreditStatusLabels,
} from './transactions/constants'
import { buildHeldCreditColumns } from './transactions/heldCreditColumns'
import TransactionFilters from './transactions/TransactionFilters'
import {
  applyQuickRange,
  getStoredFilter,
  getStoredPageSize,
  storeFilter,
  storePageSize,
} from './transactions/utils'

type CustomerHeldCreditsPanelProps = {
  token: string
  canManageCustomers: boolean
  selectedTaxCode: string | null
  initialDoc?: string | null
}

type FlashMessage = {
  tone: 'success' | 'info'
  text: string
}

export default function CustomerHeldCreditsPanel({
  token,
  canManageCustomers,
  selectedTaxCode,
  initialDoc,
}: CustomerHeldCreditsPanelProps) {
  const [rows, setRows] = useState<CustomerHeldCredit[]>([])
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(() => getStoredPageSize())
  const [total, setTotal] = useState(0)
  const [status, setStatus] = useState(() => getStoredFilter(CUSTOMER_HELD_CREDIT_STATUS_KEY))
  const [search, setSearch] = useState(initialDoc?.trim() ?? '')
  const [dateFrom, setDateFrom] = useState('')
  const [dateTo, setDateTo] = useState('')
  const [quickRange, setQuickRange] = useState('')
  const [reload, setReload] = useState(0)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [flash, setFlash] = useState<FlashMessage | null>(null)
  const [applyTarget, setApplyTarget] = useState<CustomerHeldCredit | null>(null)
  const [releaseTarget, setReleaseTarget] = useState<CustomerHeldCredit | null>(null)

  useEffect(() => {
    if (!selectedTaxCode) return
    setRows([])
    setPage(1)
    setSearch(initialDoc?.trim() ?? '')
    setFlash(null)
    setApplyTarget(null)
    setReleaseTarget(null)
  }, [initialDoc, selectedTaxCode])

  useEffect(() => {
    if (!token || !selectedTaxCode) return

    let isActive = true

    const loadHeldCredits = async () => {
      setLoading(true)
      setError(null)
      try {
        const result = await fetchCustomerHeldCredits({
          token,
          taxCode: selectedTaxCode,
          status: status || undefined,
          search: search.trim() || undefined,
          from: dateFrom || undefined,
          to: dateTo || undefined,
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
          setError('Không tải được danh sách tiền thừa do hủy HĐ.')
        }
      } finally {
        if (isActive) {
          setLoading(false)
        }
      }
    }

    void loadHeldCredits()
    return () => {
      isActive = false
    }
  }, [dateFrom, dateTo, page, pageSize, reload, search, selectedTaxCode, status, token])

  const statusOptions = useMemo(
    () => [
      { value: 'HOLDING', label: heldCreditStatusLabels.HOLDING },
      { value: 'PARTIAL', label: heldCreditStatusLabels.PARTIAL },
      { value: 'REAPPLIED', label: heldCreditStatusLabels.REAPPLIED },
      { value: 'RELEASED', label: heldCreditStatusLabels.RELEASED },
    ],
    [],
  )

  const summary = useMemo(() => {
    return rows.reduce(
      (accumulator, row) => {
        accumulator.remaining += row.amountRemaining
        accumulator.applied += row.appliedAmount
        return accumulator
      },
      { remaining: 0, applied: 0 },
    )
  }, [rows])

  const hasFilters = Boolean(status || search.trim() || dateFrom || dateTo || quickRange)

  const handleClearFilters = useCallback(() => {
    setStatus('')
    setSearch('')
    setDateFrom('')
    setDateTo('')
    setQuickRange('')
    setPage(1)
    storeFilter(CUSTOMER_HELD_CREDIT_STATUS_KEY, '')
  }, [])

  const handleApplied = useCallback((message: string) => {
    setApplyTarget(null)
    setFlash({ tone: 'success', text: message })
    setReload((value) => value + 1)
  }, [])

  const handleReleased = useCallback((message: string) => {
    setReleaseTarget(null)
    setFlash({ tone: 'info', text: message })
    setReload((value) => value + 1)
  }, [])

  const columns = useMemo(
    () =>
      buildHeldCreditColumns({
        canManageCustomers,
        openApplyModal: (row) => {
          setFlash(null)
          setApplyTarget(row)
        },
        openReleaseModal: (row) => {
          setFlash(null)
          setReleaseTarget(row)
        },
      }),
    [canManageCustomers],
  )

  if (!selectedTaxCode) {
    return null
  }

  return (
    <>
      <div id="customer-panel-held-credits" role="tabpanel" aria-labelledby="customer-tab-held-credits">
        <div className="alert alert--info" role="status">
          Khoản tiền ở đây được tạo khi hủy hóa đơn đã có thu tiền. Hệ thống không tự dùng khoản
          này cho hóa đơn khác cho đến khi kế toán chủ động áp sang hóa đơn thay thế hoặc chuyển về
          credit chung.
        </div>

        <div className="summary-grid summary-grid--emphasis">
          <div>
            <strong>{total}</strong>
            <span>Bút toán đang theo dõi</span>
          </div>
          <div>
            <strong>{formatMoney(summary.remaining)}</strong>
            <span>Còn treo trong trang</span>
          </div>
          <div>
            <strong>{formatMoney(summary.applied)}</strong>
            <span>Đã áp trong trang</span>
          </div>
        </div>

        <TransactionFilters
          searchLabel="Tìm phiếu thu / hóa đơn gốc"
          searchValue={search}
          searchPlaceholder="VD: PT-001 hoặc HD-OLD-001"
          searchTooltip="Tìm theo phiếu thu nguồn hoặc hóa đơn gốc đã bị hủy."
          onSearchChange={(value) => {
            setSearch(value)
            setPage(1)
          }}
          dateFrom={dateFrom}
          dateTo={dateTo}
          onDateFromChange={(value) => {
            setDateFrom(value)
            setPage(1)
          }}
          onDateToChange={(value) => {
            setDateTo(value)
            setPage(1)
          }}
          quickRange={quickRange}
          onQuickRangeChange={(value) =>
            applyQuickRange(value, setDateFrom, setDateTo, setQuickRange)
          }
          statusValue={status}
          statusOptions={statusOptions}
          onStatusChange={(value) => {
            setStatus(value)
            setPage(1)
            storeFilter(CUSTOMER_HELD_CREDIT_STATUS_KEY, value)
          }}
          hasFilters={hasFilters}
          onClear={handleClearFilters}
          helperText="Quản lý các khoản tiền treo phát sinh khi hủy hóa đơn đã thu."
        />

        {flash && (
          <div
            className={`alert ${flash.tone === 'success' ? 'alert--success' : 'alert--info'}`}
            role="status"
            aria-live="polite"
          >
            {flash.text}
          </div>
        )}
        {error && (
          <div className="alert alert--error" role="alert">
            {error}
          </div>
        )}

        <DataTable
          columns={columns}
          rows={rows}
          getRowKey={(row) => row.id}
          minWidth="1280px"
          emptyMessage={loading ? 'Đang tải...' : 'Không có khoản tiền thừa do hủy HĐ.'}
          pagination={{ page, pageSize, total }}
          onPageChange={setPage}
          onPageSizeChange={(nextPageSize) => {
            storePageSize(nextPageSize)
            setPageSize(nextPageSize)
            setPage(1)
          }}
        />
      </div>

      <CustomerHeldCreditModals
        token={token}
        selectedTaxCode={selectedTaxCode}
        applyTarget={applyTarget}
        releaseTarget={releaseTarget}
        onCloseApply={() => setApplyTarget(null)}
        onCloseRelease={() => setReleaseTarget(null)}
        onApplied={handleApplied}
        onReleased={handleReleased}
      />
    </>
  )
}
