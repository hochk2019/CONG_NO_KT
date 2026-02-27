import type { Dispatch, SetStateAction } from 'react'
import type { AdvanceListItem } from '../../api/advances'
import { formatDate, formatMoney } from '../../utils/format'

export const advanceStatusLabels: Record<string, string> = {
  DRAFT: 'Nháp',
  APPROVED: 'Đã phê duyệt',
  PAID: 'Đã tất toán',
  VOID: 'Đã hủy',
}

export const formatAdvanceStatus = (status: string) => {
  const normalized = status.toUpperCase()
  const className =
    normalized === 'APPROVED' || normalized === 'PAID'
      ? 'pill pill-ok'
      : normalized === 'DRAFT'
        ? 'pill pill-warn'
        : 'pill pill-info'
  return <span className={className}>{advanceStatusLabels[normalized] ?? status}</span>
}

export const shortAdvanceId = (id: string) => (id.length > 8 ? id.slice(0, 8) : id)

type ManualAdvanceColumnsOptions = {
  editingId: string | null
  editingDescription: string
  setEditingDescription: Dispatch<SetStateAction<string>>
  onStartEdit: (row: AdvanceListItem) => void
  onSaveEdit: (row: AdvanceListItem) => void
  onCancelEdit: () => void
  onApprove: (row: AdvanceListItem) => void
  onVoid: (row: AdvanceListItem) => void
  onUnvoid: (row: AdvanceListItem) => void
  loadingAction: string
}

export const buildManualAdvanceColumns = ({
  editingId,
  editingDescription,
  setEditingDescription,
  onStartEdit,
  onSaveEdit,
  onCancelEdit,
  onApprove,
  onVoid,
  onUnvoid,
  loadingAction,
}: ManualAdvanceColumnsOptions) => [
  {
    key: 'id',
    label: 'Mã',
    render: (row: AdvanceListItem) => (
      <span title={row.id} className="muted">
        {shortAdvanceId(row.id)}
      </span>
    ),
  },
  {
    key: 'advanceNo',
    label: 'Số chứng từ',
    render: (row: AdvanceListItem) =>
      row.advanceNo?.trim() ? row.advanceNo : <span className="muted">-</span>,
  },
  {
    key: 'advanceDate',
    label: 'Ngày',
    render: (row: AdvanceListItem) => formatDate(row.advanceDate),
  },
  {
    key: 'customer',
    label: 'Khách hàng',
    render: (row: AdvanceListItem) =>
      row.customerName ? `${row.customerName} (${row.customerTaxCode})` : row.customerTaxCode,
  },
  {
    key: 'ownerName',
    label: 'Phụ trách',
    render: (row: AdvanceListItem) => row.ownerName ?? '-',
  },
  {
    key: 'sellerTaxCode',
    label: 'Bên bán',
  },
  {
    key: 'source',
    label: 'Nguồn',
    render: (row: AdvanceListItem) => {
      const sourceType = row.sourceType?.toUpperCase()
      if (sourceType === 'IMPORT' || row.sourceBatchId) {
        const batchLabel = row.sourceBatchId ? shortAdvanceId(row.sourceBatchId) : '—'
        return (
          <span className="pill pill-info">
            Import · {batchLabel}
          </span>
        )
      }
      return <span className="pill pill-ok">Thủ công</span>
    },
  },
  {
    key: 'description',
    label: 'Ghi chú',
    render: (row: AdvanceListItem) => {
      if (editingId === row.id) {
        return (
          <input
            value={editingDescription}
            onChange={(event) => setEditingDescription(event.target.value)}
            placeholder="Nhập ghi chú"
            style={{ width: '100%' }}
          />
        )
      }
      return row.description?.trim() ? row.description : <span className="muted">-</span>
    },
  },
  {
    key: 'amount',
    label: 'Số tiền',
    align: 'right' as const,
    render: (row: AdvanceListItem) => formatMoney(row.amount),
  },
  {
    key: 'outstandingAmount',
    label: 'Còn lại',
    align: 'right' as const,
    render: (row: AdvanceListItem) => formatMoney(row.outstandingAmount),
  },
  {
    key: 'status',
    label: 'Trạng thái',
    render: (row: AdvanceListItem) => formatAdvanceStatus(row.status),
  },
  {
    key: 'actions',
    label: 'Thao tác',
    render: (row: AdvanceListItem) => {
      if (!row.canManage) {
        return <span className="muted">-</span>
      }
      const isEditing = editingId === row.id
      const status = row.status.toUpperCase()
      const canEdit = status !== 'VOID'
      const canApprove = row.status.toUpperCase() === 'DRAFT'
      const canVoid = row.status.toUpperCase() !== 'VOID'
      const canUnvoid = row.status.toUpperCase() === 'VOID'
      return (
        <div className="input-row">
          {canEdit && (
            <button
              className="btn btn-ghost"
              type="button"
              onClick={() => (isEditing ? onSaveEdit(row) : onStartEdit(row))}
              disabled={loadingAction === `update:${row.id}`}
            >
              {loadingAction === `update:${row.id}` ? 'Đang lưu...' : isEditing ? 'Lưu' : 'Sửa'}
            </button>
          )}
          {isEditing && (
            <button className="btn btn-ghost" type="button" onClick={onCancelEdit}>
              Bỏ
            </button>
          )}
          <button
            className="btn btn-outline"
            type="button"
            onClick={() => onApprove(row)}
            disabled={!canApprove || loadingAction === `approve:${row.id}`}
          >
            {loadingAction === `approve:${row.id}` ? 'Đang phê duyệt...' : 'Phê duyệt'}
          </button>
          {canUnvoid ? (
            <button
              className="btn btn-outline"
              type="button"
              onClick={() => onUnvoid(row)}
              disabled={loadingAction === `unvoid:${row.id}`}
            >
              {loadingAction === `unvoid:${row.id}` ? 'Đang bỏ hủy...' : 'Bỏ hủy'}
            </button>
          ) : (
            <button
              className="btn btn-outline-danger"
              type="button"
              onClick={() => onVoid(row)}
              disabled={!canVoid || loadingAction === `void:${row.id}`}
            >
              {loadingAction === `void:${row.id}` ? 'Đang hủy...' : 'Hủy'}
            </button>
          )}
        </div>
      )
    },
  },
]
