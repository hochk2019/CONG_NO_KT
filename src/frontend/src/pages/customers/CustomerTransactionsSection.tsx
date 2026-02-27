import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  type CustomerAdvance,
  type CustomerInvoice,
  type CustomerReceipt,
  fetchCustomerAdvances,
  fetchCustomerInvoices,
  fetchCustomerReceipts,
} from '../../api/customers'
import { ApiError } from '../../api/client'
import { voidAdvance } from '../../api/advances'
import { voidInvoice } from '../../api/invoices'
import { useReceiptModal } from './transactions/useReceiptModal'
import DataTable from '../../components/DataTable'
import CustomerTransactionModals from './CustomerTransactionModals'
import TransactionFilters from './transactions/TransactionFilters'
import {
  allocationTypeLabels,
  advanceStatusLabels,
  CUSTOMER_ADVANCE_STATUS_KEY,
  CUSTOMER_INVOICE_STATUS_KEY,
  CUSTOMER_RECEIPT_STATUS_KEY,
  invoiceStatusLabels,
  receiptStatusLabels,
} from './transactions/constants'
import { buildAdvanceColumns, buildInvoiceColumns, buildReceiptColumns } from './transactions/transactionColumns'
import { applyQuickRange, getStoredFilter, getStoredPageSize, shortId, storeFilter, storePageSize } from './transactions/utils'

type CustomerTransactionsSectionProps = {
  token: string
  canManageCustomers: boolean
  selectedTaxCode: string | null
  selectedName: string
  initialTab?: CustomerTransactionsTab
  initialDoc?: string | null
  onTabChange?: (tab: CustomerTransactionsTab) => void
  onClearSelection: () => void
}

type CustomerTransactionsTab = 'invoices' | 'advances' | 'receipts'

type InvoiceModalState = {
  mode: 'view' | 'void'
  row: CustomerInvoice
}

type AdvanceModalState = {
  mode: 'view' | 'void'
  row: CustomerAdvance
}

export default function CustomerTransactionsSection({
  token,
  canManageCustomers,
  selectedTaxCode,
  selectedName,
  initialTab,
  initialDoc,
  onTabChange,
  onClearSelection,
}: CustomerTransactionsSectionProps) {
  const [activeTab, setActiveTab] = useState<CustomerTransactionsTab>(initialTab ?? 'invoices')

  const [invoiceRows, setInvoiceRows] = useState<CustomerInvoice[]>([])
  const [invoicePage, setInvoicePage] = useState(1)
  const [invoicePageSize, setInvoicePageSize] = useState(() => getStoredPageSize())
  const [invoiceTotal, setInvoiceTotal] = useState(0)
  const [invoiceStatus, setInvoiceStatus] = useState(() => getStoredFilter(CUSTOMER_INVOICE_STATUS_KEY))
  const [invoiceSearch, setInvoiceSearch] = useState('')
  const [invoiceDateFrom, setInvoiceDateFrom] = useState('')
  const [invoiceDateTo, setInvoiceDateTo] = useState('')
  const [invoiceQuickRange, setInvoiceQuickRange] = useState('')
  const [invoiceReload, setInvoiceReload] = useState(0)
  const [invoiceLoading, setInvoiceLoading] = useState(false)
  const [invoiceError, setInvoiceError] = useState<string | null>(null)
  const [invoiceModal, setInvoiceModal] = useState<InvoiceModalState | null>(null)
  const [invoiceVoidId, setInvoiceVoidId] = useState('')
  const [invoiceVoidVersion, setInvoiceVoidVersion] = useState('')
  const [invoiceVoidStatus, setInvoiceVoidStatus] = useState('')
  const [invoiceVoidReason, setInvoiceVoidReason] = useState('')
  const [invoiceReplacementId, setInvoiceReplacementId] = useState('')
  const [invoiceVoidLoading, setInvoiceVoidLoading] = useState(false)
  const [invoiceVoidError, setInvoiceVoidError] = useState<string | null>(null)
  const [invoiceVoidSuccess, setInvoiceVoidSuccess] = useState<string | null>(null)

  const [advanceRows, setAdvanceRows] = useState<CustomerAdvance[]>([])
  const [advancePage, setAdvancePage] = useState(1)
  const [advancePageSize, setAdvancePageSize] = useState(() => getStoredPageSize())
  const [advanceTotal, setAdvanceTotal] = useState(0)
  const [advanceStatus, setAdvanceStatus] = useState(() => getStoredFilter(CUSTOMER_ADVANCE_STATUS_KEY))
  const [advanceSearch, setAdvanceSearch] = useState('')
  const [advanceDateFrom, setAdvanceDateFrom] = useState('')
  const [advanceDateTo, setAdvanceDateTo] = useState('')
  const [advanceQuickRange, setAdvanceQuickRange] = useState('')
  const [advanceReload, setAdvanceReload] = useState(0)
  const [advanceLoading, setAdvanceLoading] = useState(false)
  const [advanceError, setAdvanceError] = useState<string | null>(null)
  const [advanceModal, setAdvanceModal] = useState<AdvanceModalState | null>(null)
  const [advanceVoidReason, setAdvanceVoidReason] = useState('')
  const [advanceOverrideLock, setAdvanceOverrideLock] = useState(false)
  const [advanceOverrideReason, setAdvanceOverrideReason] = useState('')
  const [advanceVoidLoading, setAdvanceVoidLoading] = useState(false)
  const [advanceVoidError, setAdvanceVoidError] = useState<string | null>(null)
  const [advanceVoidSuccess, setAdvanceVoidSuccess] = useState<string | null>(null)

  const [receiptRows, setReceiptRows] = useState<CustomerReceipt[]>([])
  const [receiptPage, setReceiptPage] = useState(1)
  const [receiptPageSize, setReceiptPageSize] = useState(() => getStoredPageSize())
  const [receiptTotal, setReceiptTotal] = useState(0)
  const [receiptStatus, setReceiptStatus] = useState(() => getStoredFilter(CUSTOMER_RECEIPT_STATUS_KEY))
  const [receiptSearch, setReceiptSearch] = useState('')
  const [receiptDateFrom, setReceiptDateFrom] = useState('')
  const [receiptDateTo, setReceiptDateTo] = useState('')
  const [receiptQuickRange, setReceiptQuickRange] = useState('')
  const [receiptLoading, setReceiptLoading] = useState(false)
  const [receiptError, setReceiptError] = useState<string | null>(null)
  const {
    receiptModal,
    receiptAllocations,
    receiptAllocLoading,
    receiptAllocError,
    openReceiptModal: handleOpenReceiptModal,
    closeReceiptModal: handleCloseReceiptModal,
    renderReceiptRefs,
  } = useReceiptModal(token)

  useEffect(() => {
    if (!selectedTaxCode) return
    setActiveTab(initialTab ?? 'invoices')
    setInvoicePage(1)
    setAdvancePage(1)
    setReceiptPage(1)
    setInvoiceRows([])
    setAdvanceRows([])
    setReceiptRows([])
  }, [selectedTaxCode, initialTab])

  useEffect(() => {
    if (!selectedTaxCode) return

    const doc = initialDoc?.trim() ?? ''
    if (!doc) return

    const targetTab = initialTab ?? 'invoices'
    if (targetTab === 'invoices') {
      setInvoiceSearch(doc)
      setInvoicePage(1)
      return
    }
    if (targetTab === 'advances') {
      setAdvanceSearch(doc)
      setAdvancePage(1)
      return
    }
    setReceiptSearch(doc)
    setReceiptPage(1)
  }, [initialDoc, initialTab, selectedTaxCode])

  useEffect(() => {
    if (!token || !selectedTaxCode || activeTab !== 'invoices') return
    let isActive = true

    const load = async () => {
      setInvoiceLoading(true)
      setInvoiceError(null)
      try {
        const result = await fetchCustomerInvoices({
          token,
          taxCode: selectedTaxCode,
          status: invoiceStatus || undefined,
          search: invoiceSearch.trim() || undefined,
          from: invoiceDateFrom || undefined,
          to: invoiceDateTo || undefined,
          page: invoicePage,
          pageSize: invoicePageSize,
        })
        if (!isActive) return
        setInvoiceRows(result.items)
        setInvoiceTotal(result.total)
      } catch (err) {
        if (!isActive) return
        if (err instanceof ApiError) {
          setInvoiceError(err.message)
        } else {
          setInvoiceError('Không tải được hóa đơn.')
        }
      } finally {
        if (isActive) {
          setInvoiceLoading(false)
        }
      }
    }

    load()
    return () => {
      isActive = false
    }
  }, [
    token,
    selectedTaxCode,
    activeTab,
    invoiceStatus,
    invoiceSearch,
    invoiceDateFrom,
    invoiceDateTo,
    invoicePage,
    invoicePageSize,
    invoiceReload,
  ])

  useEffect(() => {
    if (!token || !selectedTaxCode || activeTab !== 'advances') return
    let isActive = true

    const load = async () => {
      setAdvanceLoading(true)
      setAdvanceError(null)
      try {
        const result = await fetchCustomerAdvances({
          token,
          taxCode: selectedTaxCode,
          status: advanceStatus || undefined,
          search: advanceSearch.trim() || undefined,
          from: advanceDateFrom || undefined,
          to: advanceDateTo || undefined,
          page: advancePage,
          pageSize: advancePageSize,
        })
        if (!isActive) return
        setAdvanceRows(result.items)
        setAdvanceTotal(result.total)
      } catch (err) {
        if (!isActive) return
        if (err instanceof ApiError) {
          setAdvanceError(err.message)
        } else {
          setAdvanceError('Không tải được khoản trả hộ KH.')
        }
      } finally {
        if (isActive) {
          setAdvanceLoading(false)
        }
      }
    }

    load()
    return () => {
      isActive = false
    }
  }, [
    token,
    selectedTaxCode,
    activeTab,
    advanceStatus,
    advanceSearch,
    advanceDateFrom,
    advanceDateTo,
    advancePage,
    advancePageSize,
    advanceReload,
  ])

  useEffect(() => {
    if (!token || !selectedTaxCode || activeTab !== 'receipts') return
    let isActive = true

    const load = async () => {
      setReceiptLoading(true)
      setReceiptError(null)
      try {
        const result = await fetchCustomerReceipts({
          token,
          taxCode: selectedTaxCode,
          status: receiptStatus || undefined,
          search: receiptSearch.trim() || undefined,
          from: receiptDateFrom || undefined,
          to: receiptDateTo || undefined,
          page: receiptPage,
          pageSize: receiptPageSize,
        })
        if (!isActive) return
        setReceiptRows(result.items)
        setReceiptTotal(result.total)
      } catch (err) {
        if (!isActive) return
        if (err instanceof ApiError) {
          setReceiptError(err.message)
        } else {
          setReceiptError('Không tải được phiếu thu.')
        }
      } finally {
        if (isActive) {
          setReceiptLoading(false)
        }
      }
    }

    load()
    return () => {
      isActive = false
    }
  }, [
    token,
    selectedTaxCode,
    activeTab,
    receiptStatus,
    receiptSearch,
    receiptDateFrom,
    receiptDateTo,
    receiptPage,
    receiptPageSize,
  ])

  const invoiceHasFilters = Boolean(
    invoiceStatus || invoiceSearch.trim() || invoiceDateFrom || invoiceDateTo,
  )
  const advanceHasFilters = Boolean(
    advanceStatus || advanceSearch.trim() || advanceDateFrom || advanceDateTo,
  )
  const receiptHasFilters = Boolean(receiptStatus || receiptSearch.trim() || receiptDateFrom || receiptDateTo)

  const handleClearInvoiceFilters = useCallback(() => {
    setInvoiceStatus('')
    setInvoiceSearch('')
    setInvoiceDateFrom('')
    setInvoiceDateTo('')
    setInvoiceQuickRange('')
    setInvoicePage(1)
    storeFilter(CUSTOMER_INVOICE_STATUS_KEY, '')
  }, [])

  const handleClearAdvanceFilters = useCallback(() => {
    setAdvanceStatus('')
    setAdvanceSearch('')
    setAdvanceDateFrom('')
    setAdvanceDateTo('')
    setAdvanceQuickRange('')
    setAdvancePage(1)
    storeFilter(CUSTOMER_ADVANCE_STATUS_KEY, '')
  }, [])

  const handleClearReceiptFilters = useCallback(() => {
    setReceiptStatus('')
    setReceiptSearch('')
    setReceiptDateFrom('')
    setReceiptDateTo('')
    setReceiptQuickRange('')
    setReceiptPage(1)
    storeFilter(CUSTOMER_RECEIPT_STATUS_KEY, '')
  }, [])

  const handleOpenInvoiceModal = useCallback((mode: 'view' | 'void', row: CustomerInvoice) => {
    setInvoiceModal({ mode, row })
    if (mode === 'void') {
      setInvoiceVoidId(row.id)
      setInvoiceVoidVersion(String(row.version))
      setInvoiceVoidStatus(row.status)
      setInvoiceVoidReason('')
      setInvoiceReplacementId('')
      setInvoiceVoidError(null)
      setInvoiceVoidSuccess(null)
    }
  }, [])

  const handleOpenAdvanceModal = useCallback((mode: 'view' | 'void', row: CustomerAdvance) => {
    setAdvanceModal({ mode, row })
    if (mode === 'void') {
      setAdvanceVoidReason('')
      setAdvanceOverrideLock(false)
      setAdvanceOverrideReason('')
      setAdvanceVoidError(null)
      setAdvanceVoidSuccess(null)
    }
  }, [])

  const handleVoidInvoice = useCallback(async () => {
    if (!token || !invoiceVoidId.trim()) {
      setInvoiceVoidError('Vui lòng chọn hóa đơn cần hủy.')
      return
    }
    if (!canManageCustomers) {
      setInvoiceVoidError('Bạn không có quyền hủy hóa đơn.')
      return
    }
    if (!invoiceVoidReason.trim()) {
      setInvoiceVoidError('Vui lòng nhập lý do hủy hóa đơn.')
      return
    }

    const versionValue = Number(invoiceVoidVersion)
    if (!Number.isInteger(versionValue) || versionValue < 0) {
      setInvoiceVoidError('Phiên bản hóa đơn không hợp lệ.')
      return
    }

    const status = invoiceVoidStatus.toUpperCase()
    const requiresReplacement = status === 'PAID' || status === 'PARTIAL'
    if (requiresReplacement && !invoiceReplacementId.trim()) {
      setInvoiceVoidError('Hóa đơn đã thu tiền, cần nhập hóa đơn thay thế trước khi hủy.')
      return
    }

    if (requiresReplacement) {
      const confirm = window.confirm(
        'Hóa đơn đã thu tiền. Hãy chắc chắn đã import hóa đơn thay thế trước khi hủy.',
      )
      if (!confirm) return
    }

    setInvoiceVoidLoading(true)
    setInvoiceVoidError(null)
    setInvoiceVoidSuccess(null)
    try {
      await voidInvoice(token, invoiceVoidId, {
        reason: invoiceVoidReason.trim(),
        replacementInvoiceId: invoiceReplacementId.trim() || null,
        force: requiresReplacement,
        version: versionValue,
      })
      setInvoiceVoidSuccess('Đã hủy hóa đơn.')
      setInvoiceVoidReason('')
      setInvoiceReplacementId('')
      setInvoiceReload((value) => value + 1)
      setInvoiceModal(null)
    } catch (err) {
      if (err instanceof ApiError) {
        setInvoiceVoidError(err.message)
      } else {
        setInvoiceVoidError('Không hủy được hóa đơn.')
      }
    } finally {
      setInvoiceVoidLoading(false)
    }
  }, [
    token,
    canManageCustomers,
    invoiceVoidId,
    invoiceVoidReason,
    invoiceVoidVersion,
    invoiceVoidStatus,
    invoiceReplacementId,
  ])

  const handleVoidAdvance = useCallback(async () => {
    if (!token || !advanceModal?.row) {
      setAdvanceVoidError('Vui lòng chọn khoản trả hộ cần hủy.')
      return
    }
    if (!canManageCustomers) {
      setAdvanceVoidError('Bạn không có quyền hủy khoản trả hộ KH.')
      return
    }
    if (!advanceVoidReason.trim()) {
      setAdvanceVoidError('Vui lòng nhập lý do hủy.')
      return
    }
    if (advanceModal.row.status.toUpperCase() === 'PAID') {
      setAdvanceVoidError('Khoản trả hộ đã tất toán, không thể hủy tại đây.')
      return
    }

    setAdvanceVoidLoading(true)
    setAdvanceVoidError(null)
    setAdvanceVoidSuccess(null)
    try {
      await voidAdvance(token, advanceModal.row.id, {
        reason: advanceVoidReason.trim(),
        version: advanceModal.row.version,
        overridePeriodLock: advanceOverrideLock,
        overrideReason: advanceOverrideReason.trim() || undefined,
      })
      setAdvanceVoidSuccess('Đã hủy khoản trả hộ KH.')
      setAdvanceVoidReason('')
      setAdvanceOverrideLock(false)
      setAdvanceOverrideReason('')
      setAdvanceReload((value) => value + 1)
      setAdvanceModal(null)
    } catch (err) {
      if (err instanceof ApiError) {
        setAdvanceVoidError(err.message)
      } else {
        setAdvanceVoidError('Không hủy được khoản trả hộ KH.')
      }
    } finally {
      setAdvanceVoidLoading(false)
    }
  }, [
    token,
    canManageCustomers,
    advanceModal,
    advanceVoidReason,
    advanceOverrideLock,
    advanceOverrideReason,
  ])

  const invoiceColumns = useMemo(
    () =>
      buildInvoiceColumns({
        canManageCustomers,
        openInvoiceModal: handleOpenInvoiceModal,
        renderReceiptRefs,
      }),
    [canManageCustomers, handleOpenInvoiceModal, renderReceiptRefs],
  )

  const advanceColumns = useMemo(
    () =>
      buildAdvanceColumns({
        canManageCustomers,
        openAdvanceModal: handleOpenAdvanceModal,
        renderReceiptRefs,
      }),
    [canManageCustomers, handleOpenAdvanceModal, renderReceiptRefs],
  )

  const receiptColumns = useMemo(
    () =>
      buildReceiptColumns({
        openReceiptModal: handleOpenReceiptModal,
      }),
    [handleOpenReceiptModal],
  )

  const handleSwitchTab = useCallback((tab: CustomerTransactionsTab) => {
    setActiveTab(tab)
    onTabChange?.(tab)
  }, [onTabChange])

  if (!selectedTaxCode) {
    return null
  }

  return (
    <section className="card" id="customer-transactions">
      <div className="card-row">
        <div>
          <h3>Giao dịch khách hàng</h3>
          <p className="muted">Hóa đơn, khoản trả hộ KH, phiếu thu.</p>
        </div>
        <div className="customer-actions customer-actions--active">
          <span className="customer-chip customer-chip--active">MST {selectedTaxCode}</span>
          <span className="customer-selected-name">{selectedName}</span>
          <button className="btn btn-ghost btn-table btn-strong" type="button" onClick={onClearSelection}>
            Bỏ chọn
          </button>
        </div>
      </div>
      <div className="tab-row" role="tablist" aria-label="Giao dịch khách hàng">
        <button
          id="customer-tab-invoices"
          className={`tab ${activeTab === 'invoices' ? 'tab--active' : ''}`}
          type="button"
          role="tab"
          aria-selected={activeTab === 'invoices'}
          aria-controls="customer-panel-invoices"
          onClick={() => handleSwitchTab('invoices')}
        >
          Hóa đơn
        </button>
        <button
          id="customer-tab-advances"
          className={`tab ${activeTab === 'advances' ? 'tab--active' : ''}`}
          type="button"
          role="tab"
          aria-selected={activeTab === 'advances'}
          aria-controls="customer-panel-advances"
          onClick={() => handleSwitchTab('advances')}
        >
          Khoản trả hộ KH
        </button>
        <button
          id="customer-tab-receipts"
          className={`tab ${activeTab === 'receipts' ? 'tab--active' : ''}`}
          type="button"
          role="tab"
          aria-selected={activeTab === 'receipts'}
          aria-controls="customer-panel-receipts"
          onClick={() => handleSwitchTab('receipts')}
        >
          Phiếu thu
        </button>
      </div>

      {activeTab === 'invoices' && (
        <div id="customer-panel-invoices" role="tabpanel" aria-labelledby="customer-tab-invoices">
          <TransactionFilters
            searchLabel="Tìm chứng từ (HĐ / PT)"
            searchValue={invoiceSearch}
            searchPlaceholder="VD: HD:000123 hoặc PT:PT-001"
            searchTooltip="Prefix hỗ trợ: HD: số hóa đơn, PT: số phiếu thu. Không cần prefix nếu muốn tìm tất cả."
            onSearchChange={(value) => {
              setInvoiceSearch(value)
              setInvoicePage(1)
            }}
            dateFrom={invoiceDateFrom}
            dateTo={invoiceDateTo}
            onDateFromChange={(value) => {
              setInvoiceDateFrom(value)
              setInvoicePage(1)
            }}
            onDateToChange={(value) => {
              setInvoiceDateTo(value)
              setInvoicePage(1)
            }}
            quickRange={invoiceQuickRange}
            onQuickRangeChange={(value) =>
              applyQuickRange(value, setInvoiceDateFrom, setInvoiceDateTo, setInvoiceQuickRange)
            }
            statusValue={invoiceStatus}
            statusOptions={[
              { value: 'OPEN', label: invoiceStatusLabels.OPEN },
              { value: 'PARTIAL', label: invoiceStatusLabels.PARTIAL },
              { value: 'PAID', label: invoiceStatusLabels.PAID },
              { value: 'VOID', label: invoiceStatusLabels.VOID },
            ]}
            onStatusChange={(value) => {
              setInvoiceStatus(value)
              setInvoicePage(1)
              storeFilter(CUSTOMER_INVOICE_STATUS_KEY, value)
            }}
            hasFilters={invoiceHasFilters}
            onClear={handleClearInvoiceFilters}
            helperText="Nhập số hóa đơn hoặc phiếu thu để tìm nhanh."
          />
          {invoiceError && <div className="alert alert--error" role="alert">{invoiceError}</div>}
          <DataTable
            columns={invoiceColumns}
            rows={invoiceRows}
            getRowKey={(row) => row.id}
            minWidth="1200px"
            emptyMessage={invoiceLoading ? 'Đang tải...' : 'Không có hóa đơn.'}
            pagination={{ page: invoicePage, pageSize: invoicePageSize, total: invoiceTotal }}
            onPageChange={setInvoicePage}
            onPageSizeChange={(size) => {
              storePageSize(size)
              setInvoicePageSize(size)
              setInvoicePage(1)
            }}
          />
        </div>
      )}

      {activeTab === 'advances' && (
        <div id="customer-panel-advances" role="tabpanel" aria-labelledby="customer-tab-advances">
          <TransactionFilters
            searchLabel="Tìm chứng từ (TH / PT)"
            searchValue={advanceSearch}
            searchPlaceholder="VD: TH:TH-001 hoặc PT:PT-001"
            searchTooltip="Prefix hỗ trợ: TH: số khoản trả hộ, PT: số phiếu thu. Không cần prefix nếu muốn tìm tất cả."
            onSearchChange={(value) => {
              setAdvanceSearch(value)
              setAdvancePage(1)
            }}
            dateFrom={advanceDateFrom}
            dateTo={advanceDateTo}
            onDateFromChange={(value) => {
              setAdvanceDateFrom(value)
              setAdvancePage(1)
            }}
            onDateToChange={(value) => {
              setAdvanceDateTo(value)
              setAdvancePage(1)
            }}
            quickRange={advanceQuickRange}
            onQuickRangeChange={(value) =>
              applyQuickRange(value, setAdvanceDateFrom, setAdvanceDateTo, setAdvanceQuickRange)
            }
            statusValue={advanceStatus}
            statusOptions={[
              { value: 'DRAFT', label: advanceStatusLabels.DRAFT },
              { value: 'APPROVED', label: advanceStatusLabels.APPROVED },
              { value: 'PAID', label: advanceStatusLabels.PAID },
              { value: 'VOID', label: advanceStatusLabels.VOID },
            ]}
            onStatusChange={(value) => {
              setAdvanceStatus(value)
              setAdvancePage(1)
              storeFilter(CUSTOMER_ADVANCE_STATUS_KEY, value)
            }}
            hasFilters={advanceHasFilters}
            onClear={handleClearAdvanceFilters}
            helperText="Nhập số chứng từ hoặc phiếu thu để tìm nhanh."
          />
          {advanceError && <div className="alert alert--error" role="alert">{advanceError}</div>}
          <DataTable
            columns={advanceColumns}
            rows={advanceRows}
            getRowKey={(row) => row.id}
            minWidth="1200px"
            emptyMessage={advanceLoading ? 'Đang tải...' : 'Không có khoản trả hộ KH.'}
            pagination={{ page: advancePage, pageSize: advancePageSize, total: advanceTotal }}
            onPageChange={setAdvancePage}
            onPageSizeChange={(size) => {
              storePageSize(size)
              setAdvancePageSize(size)
              setAdvancePage(1)
            }}
          />
        </div>
      )}

      {activeTab === 'receipts' && (
        <div id="customer-panel-receipts" role="tabpanel" aria-labelledby="customer-tab-receipts">
          <TransactionFilters
            searchLabel="Tìm chứng từ (PT / HD / TH)"
            searchValue={receiptSearch}
            searchPlaceholder="VD: PT:PT-001 hoặc HD:000123"
            searchTooltip="Prefix hỗ trợ: PT: phiếu thu, HD: hóa đơn, TH: khoản trả hộ. Không cần prefix nếu muốn tìm tất cả."
            onSearchChange={(value) => {
              setReceiptSearch(value)
              setReceiptPage(1)
            }}
            dateFrom={receiptDateFrom}
            dateTo={receiptDateTo}
            onDateFromChange={(value) => {
              setReceiptDateFrom(value)
              setReceiptPage(1)
            }}
            onDateToChange={(value) => {
              setReceiptDateTo(value)
              setReceiptPage(1)
            }}
            quickRange={receiptQuickRange}
            onQuickRangeChange={(value) =>
              applyQuickRange(value, setReceiptDateFrom, setReceiptDateTo, setReceiptQuickRange)
            }
            statusValue={receiptStatus}
            statusOptions={[
              { value: 'DRAFT', label: receiptStatusLabels.DRAFT },
              { value: 'APPROVED', label: receiptStatusLabels.APPROVED },
              { value: 'VOID', label: receiptStatusLabels.VOID },
            ]}
            onStatusChange={(value) => {
              setReceiptStatus(value)
              setReceiptPage(1)
              storeFilter(CUSTOMER_RECEIPT_STATUS_KEY, value)
            }}
            hasFilters={receiptHasFilters}
            onClear={handleClearReceiptFilters}
            helperText="Nhập số chứng từ để tìm nhanh."
          />
          {receiptError && <div className="alert alert--error" role="alert">{receiptError}</div>}
          <DataTable
            columns={receiptColumns}
            rows={receiptRows}
            getRowKey={(row) => row.id}
            minWidth="1050px"
            emptyMessage={receiptLoading ? 'Đang tải...' : 'Không có phiếu thu.'}
            pagination={{ page: receiptPage, pageSize: receiptPageSize, total: receiptTotal }}
            onPageChange={setReceiptPage}
            onPageSizeChange={(size) => {
              storePageSize(size)
              setReceiptPageSize(size)
              setReceiptPage(1)
            }}
          />
        </div>
      )}

      <CustomerTransactionModals
        invoiceModal={invoiceModal}
        advanceModal={advanceModal}
        receiptModal={receiptModal}
        token={token}
        invoiceStatusLabels={invoiceStatusLabels}
        advanceStatusLabels={advanceStatusLabels}
        allocationTypeLabels={allocationTypeLabels}
        onCloseInvoice={() => setInvoiceModal(null)}
        onCloseAdvance={() => setAdvanceModal(null)}
        onCloseReceipt={handleCloseReceiptModal}
        onVoidInvoice={handleVoidInvoice}
        onVoidAdvance={handleVoidAdvance}
        shortId={shortId}
        invoiceVoidReason={invoiceVoidReason}
        onInvoiceVoidReasonChange={setInvoiceVoidReason}
        invoiceReplacementId={invoiceReplacementId}
        onInvoiceReplacementChange={setInvoiceReplacementId}
        invoiceVoidLoading={invoiceVoidLoading}
        invoiceVoidError={invoiceVoidError}
        invoiceVoidSuccess={invoiceVoidSuccess}
        advanceVoidReason={advanceVoidReason}
        onAdvanceVoidReasonChange={setAdvanceVoidReason}
        advanceOverrideLock={advanceOverrideLock}
        onAdvanceOverrideLockChange={setAdvanceOverrideLock}
        advanceOverrideReason={advanceOverrideReason}
        onAdvanceOverrideReasonChange={setAdvanceOverrideReason}
        advanceVoidLoading={advanceVoidLoading}
        advanceVoidError={advanceVoidError}
        advanceVoidSuccess={advanceVoidSuccess}
        receiptAllocations={receiptAllocations}
        receiptAllocLoading={receiptAllocLoading}
        receiptAllocError={receiptAllocError}
      />
    </section>
  )
}
