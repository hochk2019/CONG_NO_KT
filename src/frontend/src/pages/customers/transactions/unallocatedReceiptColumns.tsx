import type { CustomerReceipt } from '../../../api/customers'
import { formatDate, formatMoney } from '../../../utils/format'
import { receiptStatusLabels } from './constants'
import { renderSellerLabel, shortId } from './utils'

type UnallocatedReceiptColumnDeps = {
  canManageCustomers: boolean
  toggleLoadingId: string | null
  manualLoadingId: string | null
  openReceiptModal: (row: CustomerReceipt) => void
  onToggleAutoAllocation: (row: CustomerReceipt) => void
  onOpenManualAllocation: (row: CustomerReceipt) => void
}

const resolveAutoAllocationClassName = (enabled: boolean) =>
  enabled ? 'pill pill-ok' : 'pill pill-warn'

const resolveStatusClassName = (status: string) => {
  const normalized = status.toUpperCase()
  if (normalized === 'APPROVED') return 'pill pill-ok'
  if (normalized === 'VOID') return 'pill pill-warn'
  return 'pill pill-info'
}

export const buildUnallocatedReceiptColumns = ({
  canManageCustomers,
  toggleLoadingId,
  manualLoadingId,
  openReceiptModal,
  onToggleAutoAllocation,
  onOpenManualAllocation,
}: UnallocatedReceiptColumnDeps) => [
  {
    key: 'receiptDate',
    label: 'Ngày phiếu thu',
    render: (row: CustomerReceipt) => formatDate(row.receiptDate),
  },
  {
    key: 'receiptNo',
    label: 'Số phiếu thu',
    render: (row: CustomerReceipt) => (row.receiptNo?.trim() ? row.receiptNo : shortId(row.id)),
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
    key: 'autoAllocateEnabled',
    label: 'Tự phân bổ',
    render: (row: CustomerReceipt) => (
      <span className={resolveAutoAllocationClassName(row.autoAllocateEnabled)}>
        {row.autoAllocateEnabled ? 'Đang bật' : 'Đang tắt'}
      </span>
    ),
  },
  {
    key: 'status',
    label: 'Trạng thái',
    render: (row: CustomerReceipt) => (
      <span className={resolveStatusClassName(row.status)}>
        {receiptStatusLabels[row.status] ?? row.status}
      </span>
    ),
  },
  {
    key: 'sellerTaxCode',
    label: 'Bên bán',
    render: (row: CustomerReceipt) => renderSellerLabel(row.sellerTaxCode, row.sellerShortName),
  },
  {
    key: 'actions',
    label: 'Thao tác',
    width: '320px',
    render: (row: CustomerReceipt) => {
      const toggleBusy = toggleLoadingId === row.id
      const manualBusy = manualLoadingId === row.id
      const manualDisabled =
        !canManageCustomers || row.autoAllocateEnabled || row.unallocatedAmount <= 0 || toggleBusy || manualBusy

      return (
        <div className="table-actions">
          <button className="btn btn-ghost btn-table" type="button" onClick={() => openReceiptModal(row)}>
            Xem
          </button>
          <button
            className="btn btn-outline btn-table"
            type="button"
            onClick={() => onToggleAutoAllocation(row)}
            disabled={!canManageCustomers || toggleBusy || manualBusy}
            title={!canManageCustomers ? 'Bạn không có quyền thao tác' : undefined}
          >
            {toggleBusy
              ? 'Đang cập nhật...'
              : row.autoAllocateEnabled
                ? 'Tắt tự phân bổ'
                : 'Bật tự phân bổ'}
          </button>
          <button
            className="btn btn-primary btn-table"
            type="button"
            onClick={() => onOpenManualAllocation(row)}
            disabled={manualDisabled}
            title={!canManageCustomers ? 'Bạn không có quyền thao tác' : undefined}
          >
            {manualBusy ? 'Đang tải...' : 'Áp tay'}
          </button>
        </div>
      )
    },
  },
]
