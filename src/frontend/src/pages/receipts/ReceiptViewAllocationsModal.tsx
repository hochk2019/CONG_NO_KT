import type { CSSProperties } from 'react'
import { formatDate, formatMoney } from '../../utils/format'
import type { ReceiptAllocationDetail, ReceiptListItem } from '../../api/receipts'
import { targetTypeLabels } from './receiptLabels'

type ReceiptViewAllocationsModalProps = {
  isOpen: boolean
  receipt: ReceiptListItem | null
  allocations: ReceiptAllocationDetail[]
  loading?: boolean
  error?: string | null
  onClose: () => void
}

export default function ReceiptViewAllocationsModal({
  isOpen,
  receipt,
  allocations,
  loading = false,
  error,
  onClose,
}: ReceiptViewAllocationsModalProps) {
  if (!isOpen || !receipt) return null

  const allocatedTotal = allocations.reduce((sum, line) => sum + line.amount, 0)
  const unallocatedAmount = receipt.unallocatedAmount ?? Math.max(0, receipt.amount - allocatedTotal)
  const tableStyle: CSSProperties & { ['--table-min-width']?: string } = {
    '--table-min-width': '720px',
  }

  return (
    <div className="modal-backdrop">
      <button
        type="button"
        className="modal-scrim"
        aria-label="Đóng hộp thoại"
        onClick={onClose}
      />
      <div
        className="modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="receipt-view-modal-title"
      >
        <div className="modal-header">
          <div>
            <h3 id="receipt-view-modal-title">Chi tiết phân bổ phiếu thu</h3>
            <p className="muted">
              {receipt.receiptNo?.trim() ? receipt.receiptNo : 'Phiếu thu'} • {formatDate(receipt.receiptDate)}
            </p>
          </div>
          <button className="btn btn-ghost btn-table" type="button" onClick={onClose}>
            Đóng
          </button>
        </div>

        <div className="modal-body">
          <div className="summary-grid">
            <div>
              <strong>{formatMoney(receipt.amount)}</strong>
              <span>Tổng phiếu thu</span>
            </div>
            <div>
              <strong>{formatMoney(allocatedTotal)}</strong>
              <span>Đã phân bổ</span>
            </div>
            <div>
              <strong>{formatMoney(unallocatedAmount)}</strong>
              <span>Chưa phân bổ</span>
            </div>
          </div>

          {loading && <div className="muted">Đang tải phân bổ...</div>}
          {error && <div className="alert alert--error">{error}</div>}

          {!loading && allocations.length === 0 && !error && (
            <div className="empty-state">Chưa có chứng từ được phân bổ.</div>
          )}

          {!loading && allocations.length > 0 && (
            <div className="table-scroll">
              <table className="table" style={tableStyle}>
                <thead className="table-head">
                  <tr className="table-row">
                    <th scope="col">Loại</th>
                    <th scope="col">Số chứng từ</th>
                    <th scope="col">Ngày chứng từ</th>
                    <th scope="col" className="align-right" style={{ textAlign: 'right' }}>
                      Số tiền phân bổ
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {allocations.map((line) => (
                    <tr className="table-row" key={`${line.targetType}-${line.targetId}`}>
                      <td>{targetTypeLabels[line.targetType] ?? line.targetType}</td>
                      <td>{line.targetNo ?? line.targetId}</td>
                      <td>{formatDate(line.targetDate)}</td>
                      <td className="align-right">{formatMoney(line.amount)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
