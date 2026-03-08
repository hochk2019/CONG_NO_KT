import { Link } from 'react-router-dom'
import type { ReceiptSurplusQueueItem } from '../../api/receipts'
import { formatDate, formatMoney } from '../../utils/format'

const itemTypeLabels: Record<string, string> = {
  UNALLOCATED_RECEIPT: 'Phiếu thu chưa phân bổ',
  PARTIAL_RECEIPT: 'Phiếu thu phân bổ một phần',
  HELD_CREDIT: 'Tiền treo do hủy HĐ',
}

const statusLabels: Record<string, string> = {
  UNALLOCATED: 'Chưa phân bổ',
  PARTIAL: 'Phân bổ một phần',
  SELECTED: 'Đã chọn phân bổ',
  SUGGESTED: 'Đề xuất phân bổ',
  HOLDING: 'Đang giữ',
}

const shortId = (value: string) => value.slice(0, 8)

const resolveStatusClassName = (status: string) => {
  const normalized = status.toUpperCase()
  if (normalized === 'PARTIAL' || normalized === 'UNALLOCATED') return 'pill pill-warn'
  if (normalized === 'HOLDING' || normalized === 'SUGGESTED' || normalized === 'SELECTED') {
    return 'pill pill-info'
  }
  return 'pill pill-muted'
}

const resolveReceiptLabel = (row: ReceiptSurplusQueueItem) =>
  row.receiptNo?.trim() ? row.receiptNo : shortId(row.receiptId)

const resolveActionLabel = (row: ReceiptSurplusQueueItem) =>
  row.itemType === 'HELD_CREDIT' && row.originalInvoiceNo?.trim()
    ? row.originalInvoiceNo
    : resolveReceiptLabel(row)

export const buildReceiptSurplusQueueDeepLink = (row: ReceiptSurplusQueueItem) => {
  const taxCode = encodeURIComponent(row.customerTaxCode)
  if (row.itemType === 'HELD_CREDIT') {
    const doc = encodeURIComponent(resolveActionLabel(row))
    return `/customers?taxCode=${taxCode}&tab=heldCredits&doc=${doc}`
  }

  const doc = encodeURIComponent(resolveReceiptLabel(row))
  return `/customers?taxCode=${taxCode}&tab=unallocatedReceipts&doc=${doc}`
}

export const buildReceiptSurplusQueueColumns = () => [
  {
    key: 'receiptDate',
    label: 'Ngày phiếu thu',
    width: '140px',
    render: (row: ReceiptSurplusQueueItem) => formatDate(row.receiptDate),
  },
  {
    key: 'itemType',
    label: 'Loại khoản',
    width: '220px',
    render: (row: ReceiptSurplusQueueItem) => itemTypeLabels[row.itemType] ?? row.itemType,
  },
  {
    key: 'receiptNo',
    label: 'Số chứng từ',
    width: '220px',
    render: (row: ReceiptSurplusQueueItem) => (
      <div className="stacked-text">
        <span>{resolveReceiptLabel(row)}</span>
        {row.originalInvoiceNo?.trim() ? (
          <span className="muted">HĐ gốc: {row.originalInvoiceNo}</span>
        ) : null}
      </div>
    ),
  },
  {
    key: 'customerName',
    label: 'Khách hàng',
    width: '220px',
    render: (row: ReceiptSurplusQueueItem) => (
      <div className="stacked-text">
        <span>{row.customerName?.trim() ? row.customerName : row.customerTaxCode}</span>
        <span className="muted">MST: {row.customerTaxCode}</span>
      </div>
    ),
  },
  {
    key: 'amountRemaining',
    label: 'Còn treo',
    width: '140px',
    align: 'right' as const,
    render: (row: ReceiptSurplusQueueItem) => formatMoney(row.amountRemaining),
  },
  {
    key: 'ageDays',
    label: 'Tuổi khoản',
    width: '120px',
    align: 'right' as const,
    render: (row: ReceiptSurplusQueueItem) => `${row.ageDays} ngày`,
  },
  {
    key: 'ownerName',
    label: 'Phụ trách',
    width: '180px',
    render: (row: ReceiptSurplusQueueItem) => row.ownerName?.trim() || '-',
  },
  {
    key: 'status',
    label: 'Trạng thái',
    width: '160px',
    render: (row: ReceiptSurplusQueueItem) => (
      <span className={resolveStatusClassName(row.status)}>
        {statusLabels[row.status] ?? row.status}
      </span>
    ),
  },
  {
    key: 'actions',
    label: 'Thao tác',
    width: '160px',
    render: (row: ReceiptSurplusQueueItem) => (
      <div className="table-actions">
        <Link
          className="btn btn-primary btn-table"
          to={buildReceiptSurplusQueueDeepLink(row)}
          aria-label={`Mở chi tiết ${resolveActionLabel(row)}`}
        >
          Mở chi tiết
        </Link>
      </div>
    ),
  },
]
