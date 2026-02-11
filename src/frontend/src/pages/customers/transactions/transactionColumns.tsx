import type { ReactNode } from 'react'
import type { CustomerAdvance, CustomerInvoice, CustomerReceipt, CustomerReceiptRef } from '../../../api/customers'
import { formatDate, formatMoney } from '../../../utils/format'
import { advanceStatusLabels, invoiceStatusLabels, receiptStatusLabels } from './constants'
import { renderSellerLabel } from './utils'

type InvoiceColumnDeps = {
  canManageCustomers: boolean
  openInvoiceModal: (mode: 'view' | 'void', row: CustomerInvoice) => void
  renderReceiptRefs: (refs: CustomerReceiptRef[]) => ReactNode
}

type AdvanceColumnDeps = {
  canManageCustomers: boolean
  openAdvanceModal: (mode: 'view' | 'void', row: CustomerAdvance) => void
  renderReceiptRefs: (refs: CustomerReceiptRef[]) => ReactNode
}

type ReceiptColumnDeps = {
  openReceiptModal: (params: {
    id: string
    receiptNo?: string | null
    receiptDate?: string
    allocatedAmount?: number
  }) => void
}

export const buildInvoiceColumns = ({ canManageCustomers, openInvoiceModal, renderReceiptRefs }: InvoiceColumnDeps) => [
  {
    key: 'issueDate',
    label: 'Ngày phát hành',
    render: (row: CustomerInvoice) => formatDate(row.issueDate),
  },
  {
    key: 'invoiceNo',
    label: 'Số hóa đơn',
  },
  {
    key: 'totalAmount',
    label: 'Tổng tiền',
    align: 'right' as const,
    render: (row: CustomerInvoice) => formatMoney(row.totalAmount),
  },
  {
    key: 'outstandingAmount',
    label: 'Còn lại',
    align: 'right' as const,
    render: (row: CustomerInvoice) => formatMoney(row.outstandingAmount),
  },
  {
    key: 'status',
    label: 'Trạng thái',
    render: (row: CustomerInvoice) => {
      const status = row.status.toUpperCase()
      const className =
        status === 'PAID' ? 'pill pill-ok' : status === 'VOID' ? 'pill pill-warn' : 'pill pill-info'
      return <span className={className}>{invoiceStatusLabels[row.status] ?? row.status}</span>
    },
  },
  {
    key: 'receiptRefs',
    label: 'Phiếu thu',
    render: (row: CustomerInvoice) => renderReceiptRefs(row.receiptRefs),
  },
  {
    key: 'sellerTaxCode',
    label: 'Bên bán',
    render: (row: CustomerInvoice) => renderSellerLabel(row.sellerTaxCode, row.sellerShortName),
  },
  {
    key: 'actions',
    label: 'Thao tác',
    render: (row: CustomerInvoice) => (
      <div className="table-actions">
        <button className="btn btn-ghost btn-table" type="button" onClick={() => openInvoiceModal('view', row)}>
          Xem
        </button>
        <button
          className="btn btn-outline-danger btn-table"
          type="button"
          onClick={() => openInvoiceModal('void', row)}
          disabled={!canManageCustomers || row.status.toUpperCase() === 'VOID'}
          title={!canManageCustomers ? 'Bạn không có quyền hủy' : undefined}
        >
          Hủy
        </button>
      </div>
    ),
  },
]

export const buildAdvanceColumns = ({ canManageCustomers, openAdvanceModal, renderReceiptRefs }: AdvanceColumnDeps) => [
  {
    key: 'advanceDate',
    label: 'Ngày trả hộ',
    render: (row: CustomerAdvance) => formatDate(row.advanceDate),
  },
  {
    key: 'advanceNo',
    label: 'Số chứng từ',
    render: (row: CustomerAdvance) => (row.advanceNo?.trim() ? row.advanceNo : <span className="muted">-</span>),
  },
  {
    key: 'amount',
    label: 'Tổng tiền',
    align: 'right' as const,
    render: (row: CustomerAdvance) => formatMoney(row.amount),
  },
  {
    key: 'outstandingAmount',
    label: 'Còn lại',
    align: 'right' as const,
    render: (row: CustomerAdvance) => formatMoney(row.outstandingAmount),
  },
  {
    key: 'status',
    label: 'Trạng thái',
    render: (row: CustomerAdvance) => {
      const status = row.status.toUpperCase()
      const className =
        status === 'PAID' ? 'pill pill-ok' : status === 'VOID' ? 'pill pill-warn' : 'pill pill-info'
      return <span className={className}>{advanceStatusLabels[row.status] ?? row.status}</span>
    },
  },
  {
    key: 'receiptRefs',
    label: 'Phiếu thu',
    render: (row: CustomerAdvance) => renderReceiptRefs(row.receiptRefs),
  },
  {
    key: 'sellerTaxCode',
    label: 'Bên bán',
    render: (row: CustomerAdvance) => renderSellerLabel(row.sellerTaxCode, row.sellerShortName),
  },
  {
    key: 'actions',
    label: 'Thao tác',
    render: (row: CustomerAdvance) => (
      <div className="table-actions">
        <button className="btn btn-ghost btn-table" type="button" onClick={() => openAdvanceModal('view', row)}>
          Xem
        </button>
        <button
          className="btn btn-outline-danger btn-table"
          type="button"
          onClick={() => openAdvanceModal('void', row)}
          disabled={!canManageCustomers || row.status.toUpperCase() === 'VOID'}
          title={!canManageCustomers ? 'Bạn không có quyền hủy' : undefined}
        >
          Hủy
        </button>
      </div>
    ),
  },
]

export const buildReceiptColumns = ({ openReceiptModal }: ReceiptColumnDeps) => [
  {
    key: 'receiptDate',
    label: 'Ngày phiếu thu',
    render: (row: CustomerReceipt) => formatDate(row.receiptDate),
  },
  {
    key: 'receiptNo',
    label: 'Số phiếu thu',
    render: (row: CustomerReceipt) => (row.receiptNo?.trim() ? row.receiptNo : <span className="muted">-</span>),
  },
  {
    key: 'amount',
    label: 'Tổng tiền',
    align: 'right' as const,
    render: (row: CustomerReceipt) => formatMoney(row.amount),
  },
  {
    key: 'unallocatedAmount',
    label: 'Chưa phân bổ',
    align: 'right' as const,
    render: (row: CustomerReceipt) => formatMoney(row.unallocatedAmount),
  },
  {
    key: 'status',
    label: 'Trạng thái',
    render: (row: CustomerReceipt) => {
      const status = row.status.toUpperCase()
      const className =
        status === 'APPROVED' ? 'pill pill-ok' : status === 'VOID' ? 'pill pill-warn' : 'pill pill-info'
      return <span className={className}>{receiptStatusLabels[row.status] ?? row.status}</span>
    },
  },
  {
    key: 'sellerTaxCode',
    label: 'Bên bán',
    render: (row: CustomerReceipt) => renderSellerLabel(row.sellerTaxCode, row.sellerShortName),
  },
  {
    key: 'actions',
    label: 'Thao tác',
    render: (row: CustomerReceipt) => (
      <button
        className="btn btn-ghost btn-table"
        type="button"
        onClick={() =>
          openReceiptModal({
            id: row.id,
            receiptNo: row.receiptNo,
            receiptDate: row.receiptDate,
            allocatedAmount: row.amount - row.unallocatedAmount,
          })
        }
      >
        Xem
      </button>
    ),
  },
]
