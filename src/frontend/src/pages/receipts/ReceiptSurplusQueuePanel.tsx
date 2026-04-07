import { useEffect, useMemo, useState } from 'react'
import { ApiError } from '../../api/client'
import {
  listReceiptSurplusQueue,
  type ReceiptSurplusQueueItem,
} from '../../api/receipts'
import DataTable from '../../components/DataTable'
import { useDebouncedValue } from '../../hooks/useDebouncedValue'
import { formatMoney } from '../../utils/format'
import { buildReceiptSurplusQueueColumns } from './receiptSurplusQueueColumns'

const DEFAULT_PAGE_SIZE = 10

type ReceiptSurplusQueuePanelProps = {
  token: string
}

export default function ReceiptSurplusQueuePanel({
  token,
}: ReceiptSurplusQueuePanelProps) {
  const [rows, setRows] = useState<ReceiptSurplusQueueItem[]>([])
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE)
  const [total, setTotal] = useState(0)
  const [itemType, setItemType] = useState('')
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const debouncedSearch = useDebouncedValue(search, 300)

  const columns = useMemo(() => buildReceiptSurplusQueueColumns(), [])

  useEffect(() => {
    if (!token) return
    let isActive = true

    const load = async () => {
      setLoading(true)
      setError(null)
      try {
        const result = await listReceiptSurplusQueue({
          token,
          itemType: itemType || undefined,
          search: debouncedSearch.trim() || undefined,
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
          setError('Không tải được danh sách tiền thừa chưa phân bổ.')
        }
      } finally {
        if (isActive) {
          setLoading(false)
        }
      }
    }

    void load()
    return () => {
      isActive = false
    }
  }, [debouncedSearch, itemType, page, pageSize, token])

  const summary = useMemo(() => {
    return rows.reduce(
      (accumulator, row) => {
        accumulator.amountRemaining += row.amountRemaining
        if (row.itemType === 'HELD_CREDIT') {
          accumulator.heldAmount += row.amountRemaining
        }
        return accumulator
      },
      { amountRemaining: 0, heldAmount: 0 },
    )
  }, [rows])

  const hasFilters = Boolean(itemType || search.trim())

  return (
    <section className="card">
      <div className="section-header">
        <div>
          <h4>Tiền thừa chưa phân bổ</h4>
          <p className="muted">
            Gồm phiếu thu chưa phân bổ, phiếu thu phân bổ một phần và tiền treo do
            hủy hóa đơn.
          </p>
        </div>
        {loading ? <span className="muted">Đang tải...</span> : null}
      </div>

      <div className="filters-grid">
        <label className="field">
          <span>Loại khoản</span>
          <select
            aria-label="Loại khoản"
            value={itemType}
            onChange={(event) => {
              setItemType(event.target.value)
              setPage(1)
            }}
          >
            <option value="">Tất cả</option>
            <option value="UNALLOCATED_RECEIPT">Phiếu thu chưa phân bổ</option>
            <option value="PARTIAL_RECEIPT">Phiếu thu phân bổ một phần</option>
            <option value="HELD_CREDIT">Tiền treo do hủy HĐ</option>
          </select>
        </label>

        <label className="field">
          <span>Tìm chứng từ</span>
          <input
            value={search}
            placeholder="VD: PT-001 hoặc INV-001"
            onChange={(event) => {
              setSearch(event.target.value)
              setPage(1)
            }}
          />
        </label>

        <label className="field">
          <span>&nbsp;</span>
          <button
            className="btn btn-ghost btn-sm"
            type="button"
            disabled={!hasFilters}
            onClick={() => {
              setItemType('')
              setSearch('')
              setPage(1)
            }}
          >
            Đặt lại
          </button>
        </label>
      </div>

      {error && <div className="alert alert--error">{error}</div>}

      <div className="summary-grid">
        <div>
          <strong>{total}</strong>
          <span>Khoản đang treo</span>
        </div>
        <div>
          <strong>{formatMoney(summary.amountRemaining)}</strong>
          <span>Tổng còn treo trong trang</span>
        </div>
        <div>
          <strong>{formatMoney(summary.heldAmount)}</strong>
          <span>Tiền treo do hủy HĐ</span>
        </div>
      </div>

      <DataTable
        columns={columns}
        rows={rows}
        getRowKey={(row) => row.id}
        minWidth="1560px"
        emptyMessage={loading ? 'Đang tải...' : 'Không có khoản tiền thừa chưa phân bổ.'}
        pagination={{ page, pageSize, total }}
        onPageChange={setPage}
        onPageSizeChange={(value) => {
          setPageSize(value)
          setPage(1)
        }}
      />
    </section>
  )
}
