import { useEffect, useMemo, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { ApiError } from '../api/client'
import { listInvoices, type InvoiceListItem } from '../api/invoices'
import DataTable from '../components/DataTable'
import { useAuth } from '../context/AuthStore'
import { formatDate, formatMoney } from '../utils/format'
import { invoiceStatusLabels } from './customers/transactions/constants'
import TransactionFilters from './customers/transactions/TransactionFilters'
import {
  applyQuickRange,
  getStoredPageSize,
  renderSellerLabel,
  storePageSize,
} from './customers/transactions/utils'
import ManualInvoicesSection from './imports/ManualInvoicesSection'

const INVOICE_STATUS_STORAGE_KEY = 'pref.invoices.status'

const getStoredStatus = () => {
  if (typeof window === 'undefined') return ''
  return window.localStorage.getItem(INVOICE_STATUS_STORAGE_KEY) ?? ''
}

const storeStatus = (value: string) => {
  if (typeof window === 'undefined') return
  if (!value) {
    window.localStorage.removeItem(INVOICE_STATUS_STORAGE_KEY)
    return
  }

  window.localStorage.setItem(INVOICE_STATUS_STORAGE_KEY, value)
}

const renderReferenceChips = (
  refs: { id: string; invoiceNo: string; amount: number }[],
  emptyText = '-',
) => {
  if (refs.length === 0) {
    return <span className="muted">{emptyText}</span>
  }

  return (
    <div className="stacked-text">
      {refs.map((ref) => (
        <span key={ref.id}>
          {ref.invoiceNo} · {formatMoney(ref.amount)}
        </span>
      ))}
    </div>
  )
}

const renderReceiptRefs = (refs: InvoiceListItem['receiptRefs']) => {
  if (refs.length === 0) {
    return <span className="muted">-</span>
  }

  return (
    <div className="stacked-text">
      {refs.map((ref) => (
        <span key={ref.id}>
          {ref.receiptNo ?? 'PT'} · {formatMoney(ref.amount)}
        </span>
      ))}
    </div>
  )
}

const renderLinkedInvoices = (row: InvoiceListItem) => {
  if (row.reductionInvoiceRefs.length === 0 && row.reducedInvoiceRefs.length === 0) {
    return <span className="muted">-</span>
  }

  return (
    <div className="stacked-text">
      {row.reductionInvoiceRefs.length > 0 ? (
        <div className="stacked-text">
          <span>Giảm HĐ:</span>
          {renderReferenceChips(row.reductionInvoiceRefs, '')}
        </div>
      ) : null}
      {row.reducedInvoiceRefs.length > 0 ? (
        <div className="stacked-text">
          <span>Được giảm bởi:</span>
          {renderReferenceChips(row.reducedInvoiceRefs, '')}
        </div>
      ) : null}
    </div>
  )
}

export default function InvoicesPage() {
  const { state } = useAuth()
  const token = state.accessToken ?? ''
  const canCommit = state.permissions.includes('import.commit.invoice')
  const location = useLocation()
  const navigate = useNavigate()

  const [rows, setRows] = useState<InvoiceListItem[]>([])
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(() => getStoredPageSize())
  const [total, setTotal] = useState(0)
  const [status, setStatus] = useState(() => getStoredStatus())
  const [search, setSearch] = useState('')
  const [dateFrom, setDateFrom] = useState('')
  const [dateTo, setDateTo] = useState('')
  const [quickRange, setQuickRange] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const tab = new URLSearchParams(location.search).get('tab')
    if (tab === 'import') {
      navigate('/imports?tab=batch&type=INVOICE', { replace: true })
    }
  }, [location.search, navigate])

  useEffect(() => {
    if (!token) return

    let isActive = true
    setLoading(true)
    setError(null)

    listInvoices(token, {
      status: status || undefined,
      search: search || undefined,
      from: dateFrom || undefined,
      to: dateTo || undefined,
      page,
      pageSize,
    })
      .then((result) => {
        if (!isActive) return
        setRows(result.items)
        setTotal(result.total)
      })
      .catch((err) => {
        if (!isActive) return
        if (err instanceof ApiError) {
          setError(err.message)
          return
        }

        setError('Không tải được danh sách hóa đơn.')
      })
      .finally(() => {
        if (isActive) {
          setLoading(false)
        }
      })

    return () => {
      isActive = false
    }
  }, [dateFrom, dateTo, page, pageSize, search, status, token])

  const columns = useMemo(
    () => [
      {
        key: 'issueDate',
        label: 'Ngày phát hành',
        render: (row: InvoiceListItem) => formatDate(row.issueDate),
      },
      {
        key: 'invoiceNo',
        label: 'Số hóa đơn',
      },
      {
        key: 'customer',
        label: 'Khách hàng',
        render: (row: InvoiceListItem) => (
          <div className="stacked-text">
            <span>{row.customerTaxCode}</span>
            <span className="muted">{row.customerName}</span>
          </div>
        ),
      },
      {
        key: 'totalAmount',
        label: 'Tổng tiền',
        align: 'right' as const,
        render: (row: InvoiceListItem) => formatMoney(row.totalAmount),
      },
      {
        key: 'outstandingAmount',
        label: 'Còn lại',
        align: 'right' as const,
        render: (row: InvoiceListItem) => formatMoney(row.outstandingAmount),
      },
      {
        key: 'status',
        label: 'Trạng thái',
        render: (row: InvoiceListItem) => {
          const normalized = row.status.toUpperCase()
          const className =
            normalized === 'PAID'
              ? 'pill pill-ok'
              : normalized === 'VOID'
                ? 'pill pill-warn'
                : 'pill pill-info'
          return <span className={className}>{invoiceStatusLabels[normalized] ?? row.status}</span>
        },
      },
      {
        key: 'receiptRefs',
        label: 'Phiếu thu',
        render: (row: InvoiceListItem) => renderReceiptRefs(row.receiptRefs),
      },
      {
        key: 'linkedInvoices',
        label: 'Liên kết HĐ',
        render: (row: InvoiceListItem) => renderLinkedInvoices(row),
      },
      {
        key: 'sellerTaxCode',
        label: 'Bên bán',
        render: (row: InvoiceListItem) => renderSellerLabel(row.sellerTaxCode, row.sellerShortName),
      },
    ],
    [],
  )

  const hasFilters = Boolean(status || search || dateFrom || dateTo || quickRange)

  const handleClearFilters = () => {
    setStatus('')
    setSearch('')
    setDateFrom('')
    setDateTo('')
    setQuickRange('')
    setPage(1)
    storeStatus('')
  }

  const handleOpenImportTemplate = () => {
    navigate('/imports?tab=batch&type=INVOICE')
  }

  return (
    <div className="page-stack">
      <ManualInvoicesSection
        token={token}
        canCommit={canCommit}
        onImportTemplate={handleOpenImportTemplate}
      />

      <section className="card">
        <div className="card-row">
          <div>
            <h3>Danh sách hóa đơn</h3>
            <p className="muted">Tìm nhanh theo số hóa đơn hoặc số phiếu thu tham chiếu.</p>
          </div>
        </div>

        <TransactionFilters
          searchLabel="Tìm chứng từ (HĐ / PT)"
          searchValue={search}
          searchPlaceholder="VD: HD:000123 hoặc PT:PT-001"
          searchTooltip="Prefix hỗ trợ: HD: số hóa đơn, PT: số phiếu thu. Không cần prefix nếu muốn tìm tất cả."
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
            applyQuickRange(value, setDateFrom, setDateTo, (next) => {
              setQuickRange(next)
              setPage(1)
            })
          }
          statusValue={status}
          statusOptions={[
            { value: 'OPEN', label: invoiceStatusLabels.OPEN },
            { value: 'PARTIAL', label: invoiceStatusLabels.PARTIAL },
            { value: 'PAID', label: invoiceStatusLabels.PAID },
            { value: 'VOID', label: invoiceStatusLabels.VOID },
          ]}
          onStatusChange={(value) => {
            setStatus(value)
            setPage(1)
            storeStatus(value)
          }}
          hasFilters={hasFilters}
          onClear={handleClearFilters}
          helperText="Nhập số hóa đơn hoặc phiếu thu để tìm nhanh."
        />

        {error && <div className="alert alert--error" role="alert">{error}</div>}

        <DataTable
          columns={columns}
          rows={rows}
          getRowKey={(row) => row.id}
          minWidth="1400px"
          emptyMessage={loading ? 'Đang tải...' : 'Không có hóa đơn.'}
          pagination={{ page, pageSize, total }}
          onPageChange={setPage}
          onPageSizeChange={(size) => {
            storePageSize(size)
            setPageSize(size)
            setPage(1)
          }}
        />
      </section>
    </div>
  )
}
