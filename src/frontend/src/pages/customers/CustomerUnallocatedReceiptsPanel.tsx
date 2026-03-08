import { useCallback, useEffect, useMemo, useState } from 'react'
import type { CustomerReceipt } from '../../api/customers'
import { fetchCustomerReceipts } from '../../api/customers'
import { ApiError } from '../../api/client'
import {
  allocateApprovedReceipt,
  fetchReceiptAllocations,
  fetchReceiptOpenItems,
  type ReceiptAllocationDetail,
  type ReceiptListItem,
  type ReceiptOpenItem,
  type ReceiptTargetRef,
  updateReceiptAutoAllocation,
} from '../../api/receipts'
import DataTable from '../../components/DataTable'
import { formatMoney } from '../../utils/format'
import ReceiptAllocationModal from '../receipts/ReceiptAllocationModal'
import ReceiptViewAllocationsModal from '../receipts/ReceiptViewAllocationsModal'
import { buildUnallocatedReceiptColumns } from './transactions/unallocatedReceiptColumns'
import TransactionFilters from './transactions/TransactionFilters'
import {
  applyQuickRange,
  getStoredPageSize,
  storePageSize,
  shortId,
} from './transactions/utils'

type CustomerUnallocatedReceiptsPanelProps = {
  token: string
  canManageCustomers: boolean
  selectedTaxCode: string | null
  initialDoc?: string | null
}

type FlashMessage = {
  tone: 'success' | 'error'
  message: string
}

const emptyStatusOptions = [{ value: 'APPROVED', label: 'Đã phê duyệt' }]

const toReceiptListItem = (
  row: CustomerReceipt,
  customerTaxCode: string,
  canManageCustomers: boolean,
): ReceiptListItem => ({
  id: row.id,
  status: row.status,
  version: row.version,
  receiptNo: row.receiptNo,
  receiptDate: row.receiptDate,
  amount: row.amount,
  unallocatedAmount: row.unallocatedAmount,
  autoAllocateEnabled: row.autoAllocateEnabled,
  allocationMode: 'MANUAL',
  allocationStatus: 'PENDING',
  allocationPriority: 'ISSUE_DATE',
  method: 'UNKNOWN',
  sellerTaxCode: row.sellerTaxCode,
  customerTaxCode,
  canManage: canManageCustomers,
})

export default function CustomerUnallocatedReceiptsPanel({
  token,
  canManageCustomers,
  selectedTaxCode,
  initialDoc,
}: CustomerUnallocatedReceiptsPanelProps) {
  const [rows, setRows] = useState<CustomerReceipt[]>([])
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(() => getStoredPageSize())
  const [total, setTotal] = useState(0)
  const [search, setSearch] = useState(initialDoc?.trim() ?? '')
  const [dateFrom, setDateFrom] = useState('')
  const [dateTo, setDateTo] = useState('')
  const [quickRange, setQuickRange] = useState('')
  const [reload, setReload] = useState(0)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [flash, setFlash] = useState<FlashMessage | null>(null)
  const [toggleLoadingId, setToggleLoadingId] = useState<string | null>(null)
  const [manualLoadingId, setManualLoadingId] = useState<string | null>(null)
  const [viewRow, setViewRow] = useState<CustomerReceipt | null>(null)
  const [viewAllocations, setViewAllocations] = useState<ReceiptAllocationDetail[]>([])
  const [viewAllocLoading, setViewAllocLoading] = useState(false)
  const [viewAllocError, setViewAllocError] = useState<string | null>(null)
  const [allocationRow, setAllocationRow] = useState<CustomerReceipt | null>(null)
  const [allocationOpenItems, setAllocationOpenItems] = useState<ReceiptOpenItem[]>([])
  const [allocationTargets] = useState<ReceiptTargetRef[]>([])
  const [allocationPriority, setAllocationPriority] = useState('ISSUE_DATE')
  const [allocationSaving, setAllocationSaving] = useState(false)

  useEffect(() => {
    if (!selectedTaxCode) return
    setRows([])
    setTotal(0)
    setPage(1)
    setSearch(initialDoc?.trim() ?? '')
    setDateFrom('')
    setDateTo('')
    setQuickRange('')
    setFlash(null)
  }, [initialDoc, selectedTaxCode])

  useEffect(() => {
    if (!token || !selectedTaxCode) return
    let isActive = true

    const load = async () => {
      setLoading(true)
      setError(null)
      try {
        const result = await fetchCustomerReceipts({
          token,
          taxCode: selectedTaxCode,
          search: search || undefined,
          from: dateFrom || undefined,
          to: dateTo || undefined,
          unallocatedOnly: true,
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
          setError('Không tải được danh sách phiếu thu chưa phân bổ.')
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
  }, [dateFrom, dateTo, page, pageSize, reload, search, selectedTaxCode, token])

  useEffect(() => {
    if (!token || !viewRow) {
      setViewAllocations([])
      setViewAllocError(null)
      return
    }
    let isActive = true

    const load = async () => {
      setViewAllocLoading(true)
      setViewAllocError(null)
      try {
        const result = await fetchReceiptAllocations(token, viewRow.id)
        if (!isActive) return
        setViewAllocations(result)
      } catch (err) {
        if (!isActive) return
        if (err instanceof ApiError) {
          setViewAllocError(err.message)
        } else {
          setViewAllocError('Không tải được chi tiết phân bổ phiếu thu.')
        }
      } finally {
        if (isActive) {
          setViewAllocLoading(false)
        }
      }
    }

    load()
    return () => {
      isActive = false
    }
  }, [token, viewRow])

  const summary = useMemo(() => {
    return rows.reduce(
      (accumulator, row) => {
        accumulator.totalAmount += row.amount
        accumulator.unallocatedAmount += row.unallocatedAmount
        return accumulator
      },
      { totalAmount: 0, unallocatedAmount: 0 },
    )
  }, [rows])

  const hasFilters = Boolean(search || dateFrom || dateTo || quickRange)

  const handleQuickRangeChange = useCallback((value: string) => {
    applyQuickRange(value, setDateFrom, setDateTo, setQuickRange)
    setPage(1)
  }, [])

  const handleClearFilters = useCallback(() => {
    setSearch('')
    setDateFrom('')
    setDateTo('')
    setQuickRange('')
    setPage(1)
  }, [])

  const openReceiptModal = useCallback((row: CustomerReceipt) => {
    setViewRow(row)
  }, [])

  const closeReceiptModal = useCallback(() => {
    setViewRow(null)
    setViewAllocations([])
    setViewAllocError(null)
  }, [])

  const closeAllocationModal = useCallback(() => {
    if (allocationSaving) return
    setAllocationRow(null)
    setAllocationOpenItems([])
    setManualLoadingId(null)
    setAllocationPriority('ISSUE_DATE')
  }, [allocationSaving])

  const handleToggleAutoAllocation = useCallback(
    async (row: CustomerReceipt) => {
      setToggleLoadingId(row.id)
      setFlash(null)
      try {
        const result = await updateReceiptAutoAllocation(token, row.id, {
          autoAllocateEnabled: !row.autoAllocateEnabled,
          version: row.version,
        })
        setFlash({
          tone: 'success',
          message: result.autoAllocateEnabled
            ? `Đã bật tự phân bổ cho phiếu thu ${result.receiptNo?.trim() || shortId(result.id)}.`
            : `Đã tắt tự phân bổ cho phiếu thu ${result.receiptNo?.trim() || shortId(result.id)}.`,
        })
        setReload((value) => value + 1)
      } catch (err) {
        if (err instanceof ApiError) {
          setFlash({ tone: 'error', message: err.message })
        } else {
          setFlash({ tone: 'error', message: 'Không cập nhật được chế độ tự phân bổ.' })
        }
      } finally {
        setToggleLoadingId(null)
      }
    },
    [token],
  )

  const handleOpenManualAllocation = useCallback(
    async (row: CustomerReceipt) => {
      if (!selectedTaxCode) return
      setManualLoadingId(row.id)
      setFlash(null)
      try {
        const openItems = await fetchReceiptOpenItems({
          token,
          sellerTaxCode: row.sellerTaxCode,
          customerTaxCode: selectedTaxCode,
        })
        setAllocationOpenItems(openItems)
        setAllocationPriority('ISSUE_DATE')
        setAllocationRow(row)
      } catch (err) {
        if (err instanceof ApiError) {
          setFlash({ tone: 'error', message: err.message })
        } else {
          setFlash({ tone: 'error', message: 'Không tải được danh sách hóa đơn mở để áp tay.' })
        }
      } finally {
        setManualLoadingId(null)
      }
    },
    [selectedTaxCode, token],
  )

  const handleApplyAllocation = useCallback(
    async (targets: ReceiptTargetRef[]) => {
      if (!allocationRow) return
      setAllocationSaving(true)
      setFlash(null)
      try {
        await allocateApprovedReceipt(token, allocationRow.id, {
          selectedTargets: targets,
          version: allocationRow.version,
        })
        setFlash({
          tone: 'success',
          message: `Đã áp tay phiếu thu ${allocationRow.receiptNo?.trim() || shortId(allocationRow.id)}.`,
        })
        setAllocationRow(null)
        setAllocationOpenItems([])
        setReload((value) => value + 1)
      } catch (err) {
        if (err instanceof ApiError) {
          setFlash({ tone: 'error', message: err.message })
        } else {
          setFlash({ tone: 'error', message: 'Không áp tay được phiếu thu.' })
        }
      } finally {
        setAllocationSaving(false)
      }
    },
    [allocationRow, token],
  )

  const columns = useMemo(
    () =>
      buildUnallocatedReceiptColumns({
        canManageCustomers,
        toggleLoadingId,
        manualLoadingId,
        openReceiptModal,
        onToggleAutoAllocation: handleToggleAutoAllocation,
        onOpenManualAllocation: handleOpenManualAllocation,
      }),
    [
      canManageCustomers,
      handleOpenManualAllocation,
      handleToggleAutoAllocation,
      manualLoadingId,
      openReceiptModal,
      toggleLoadingId,
    ],
  )

  const selectedReceiptForView = useMemo(() => {
    if (!viewRow || !selectedTaxCode) return null
    return toReceiptListItem(viewRow, selectedTaxCode, canManageCustomers)
  }, [canManageCustomers, selectedTaxCode, viewRow])

  return (
    <>
      <div
        id="customer-panel-unallocated-receipts"
        role="tabpanel"
        aria-labelledby="customer-tab-unallocated-receipts"
      >
        <section className="card">
          <div className="section-header">
            <div>
              <h4>Phiếu thu chưa phân bổ</h4>
              <p className="muted">
                Chỉ hiện phiếu thu đã duyệt còn tiền chưa phân bổ. Có thể bật lại tự phân bổ hoặc
                áp tay vào hóa đơn mở.
              </p>
            </div>
          </div>

          <TransactionFilters
            searchLabel="Tìm phiếu thu"
            searchValue={search}
            searchPlaceholder="VD: PT-001"
            onSearchChange={(value) => {
              setSearch(value)
              setPage(1)
            }}
            dateFrom={dateFrom}
            dateTo={dateTo}
            onDateFromChange={(value) => {
              setDateFrom(value)
              setQuickRange('')
              setPage(1)
            }}
            onDateToChange={(value) => {
              setDateTo(value)
              setQuickRange('')
              setPage(1)
            }}
            quickRange={quickRange}
            onQuickRangeChange={handleQuickRangeChange}
            hideStatus
            statusValue="APPROVED"
            statusOptions={emptyStatusOptions}
            onStatusChange={() => undefined}
            hasFilters={hasFilters}
            onClear={handleClearFilters}
            helperText="Lọc theo số phiếu thu hoặc ngày thu."
          />

          {flash && <div className={`alert alert--${flash.tone}`}>{flash.message}</div>}
          {error && <div className="alert alert--error">{error}</div>}

          <div className="summary-grid">
            <div>
              <strong>{rows.length}</strong>
              <span>Phiếu thu trong trang</span>
            </div>
            <div>
              <strong>{formatMoney(summary.totalAmount)}</strong>
              <span>Tổng tiền phiếu thu</span>
            </div>
            <div>
              <strong>{formatMoney(summary.unallocatedAmount)}</strong>
              <span>Tổng chưa phân bổ</span>
            </div>
          </div>

          <DataTable
            columns={columns}
            rows={rows}
            getRowKey={(row) => row.id}
            minWidth="1320px"
            emptyMessage={loading ? 'Đang tải...' : 'Không có phiếu thu chưa phân bổ.'}
            pagination={{ page, pageSize, total }}
            onPageChange={setPage}
            onPageSizeChange={(value) => {
              setPageSize(value)
              setPage(1)
              storePageSize(value)
            }}
          />
        </section>
      </div>

      <ReceiptViewAllocationsModal
        isOpen={Boolean(selectedReceiptForView)}
        receipt={selectedReceiptForView}
        allocations={viewAllocations}
        loading={viewAllocLoading}
        error={viewAllocError}
        onClose={closeReceiptModal}
      />

      {allocationRow && selectedTaxCode && (
        <ReceiptAllocationModal
          isOpen
          token={token}
          sellerTaxCode={allocationRow.sellerTaxCode}
          customerTaxCode={selectedTaxCode}
          amount={allocationRow.unallocatedAmount}
          allocationPriority={allocationPriority}
          onPriorityChange={setAllocationPriority}
          openItems={allocationOpenItems}
          selectedTargets={allocationTargets}
          onApply={handleApplyAllocation}
          confirmLabel={allocationSaving ? 'Đang áp...' : 'Áp tay'}
          onClose={closeAllocationModal}
        />
      )}
    </>
  )
}
