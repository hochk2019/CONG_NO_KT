import type { CustomerHeldCredit } from '../../../api/customers'
import { formatDate, formatMoney } from '../../../utils/format'
import { heldCreditStatusLabels } from './constants'
import { shortId } from './utils'

type HeldCreditColumnDeps = {
  canManageCustomers: boolean
  openApplyModal: (row: CustomerHeldCredit) => void
  openReleaseModal: (row: CustomerHeldCredit) => void
}

const resolveStatusClassName = (status: string) => {
  const normalized = status.toUpperCase()
  if (normalized === 'REAPPLIED') return 'pill pill-ok'
  if (normalized === 'RELEASED') return 'pill pill-warn'
  return 'pill pill-info'
}

const canManageHeldCredit = (row: CustomerHeldCredit) => {
  const normalized = row.status.toUpperCase()
  return row.amountRemaining > 0 && normalized !== 'REAPPLIED' && normalized !== 'RELEASED'
}

export const buildHeldCreditColumns = ({
  canManageCustomers,
  openApplyModal,
  openReleaseModal,
}: HeldCreditColumnDeps) => [
  {
    key: 'receiptNo',
    label: 'Phiếu thu nguồn',
    width: '220px',
    render: (row: CustomerHeldCredit) => (
      <div className="stacked-text">
        <span>{row.receiptNo?.trim() ? row.receiptNo : shortId(row.receiptId)}</span>
        <span className="muted">Ngày thu: {formatDate(row.receiptDate)}</span>
      </div>
    ),
  },
  {
    key: 'originalInvoiceNo',
    label: 'Hóa đơn gốc',
    width: '220px',
    render: (row: CustomerHeldCredit) => (
      <div className="stacked-text">
        <span>{row.originalInvoiceNo?.trim() ? row.originalInvoiceNo : shortId(row.originalInvoiceId)}</span>
        <span className="muted">Ngày HĐ: {formatDate(row.originalInvoiceDate)}</span>
      </div>
    ),
  },
  {
    key: 'originalAmount',
    label: 'Tiền gốc',
    align: 'right' as const,
    render: (row: CustomerHeldCredit) => formatMoney(row.originalAmount),
  },
  {
    key: 'appliedAmount',
    label: 'Đã áp',
    align: 'right' as const,
    render: (row: CustomerHeldCredit) => formatMoney(row.appliedAmount),
  },
  {
    key: 'amountRemaining',
    label: 'Còn treo',
    align: 'right' as const,
    render: (row: CustomerHeldCredit) => formatMoney(row.amountRemaining),
  },
  {
    key: 'updatedAt',
    label: 'Cập nhật',
    render: (row: CustomerHeldCredit) => formatDate(row.updatedAt),
  },
  {
    key: 'status',
    label: 'Trạng thái',
    render: (row: CustomerHeldCredit) => (
      <span className={resolveStatusClassName(row.status)}>
        {heldCreditStatusLabels[row.status] ?? row.status}
      </span>
    ),
  },
  {
    key: 'actions',
    label: 'Thao tác',
    width: '240px',
    render: (row: CustomerHeldCredit) => {
      const disabled = !canManageCustomers || !canManageHeldCredit(row)
      return (
        <div className="table-actions">
          <button
            className="btn btn-primary btn-table"
            type="button"
            onClick={() => openApplyModal(row)}
            disabled={disabled}
            title={!canManageCustomers ? 'Bạn không có quyền thao tác' : undefined}
          >
            Áp sang HĐ
          </button>
          <button
            className="btn btn-outline-danger btn-table"
            type="button"
            onClick={() => openReleaseModal(row)}
            disabled={disabled}
            title={!canManageCustomers ? 'Bạn không có quyền thao tác' : undefined}
          >
            Chuyển credit chung
          </button>
        </div>
      )
    },
  },
]
