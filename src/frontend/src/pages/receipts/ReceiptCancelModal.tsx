import { useState } from 'react'

type ReceiptCancelModalProps = {
  isOpen: boolean
  onClose: () => void
  onConfirm: (payload: { reason: string; overridePeriodLock: boolean; overrideReason?: string }) => void
  loading?: boolean
  error?: string | null
}

export default function ReceiptCancelModal({
  isOpen,
  onClose,
  onConfirm,
  loading = false,
  error,
}: ReceiptCancelModalProps) {
  const [reason, setReason] = useState('')
  const [overridePeriodLock, setOverridePeriodLock] = useState(false)
  const [overrideReason, setOverrideReason] = useState('')

  if (!isOpen) return null

  return (
    <div className="modal-backdrop">
      <button
        type="button"
        className="modal-scrim"
        aria-label="Đóng hộp thoại"
        onClick={onClose}
      />
      <div
        className="modal modal--narrow"
        role="dialog"
        aria-modal="true"
        aria-labelledby="receipt-cancel-modal-title"
      >
        <div className="modal-header">
          <div>
            <h3 id="receipt-cancel-modal-title">Hủy phiếu thu</h3>
            <p className="muted">Lý do hủy là bắt buộc.</p>
          </div>
          <button className="btn btn-ghost btn-table" type="button" onClick={onClose}>
            Đóng
          </button>
        </div>
        <div className="modal-body">
          <label className="field">
            <span>Lý do hủy</span>
            <input
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              placeholder="Nhập lý do hủy"
            />
          </label>
          <details className="advanced-panel">
            <summary>Tùy chọn nâng cao</summary>
            <div className="advanced-panel__content">
              <label className="field field-inline">
                <input
                  type="checkbox"
                  checked={overridePeriodLock}
                  onChange={(event) => setOverridePeriodLock(event.target.checked)}
                />
                <span>Vượt khóa kỳ</span>
              </label>
              {overridePeriodLock && (
                <label className="field">
                  <span>Lý do vượt khóa kỳ</span>
                  <input
                    value={overrideReason}
                    onChange={(event) => setOverrideReason(event.target.value)}
                    placeholder="Bắt buộc nếu vượt khóa kỳ"
                  />
                </label>
              )}
            </div>
          </details>
          {error && <div className="alert alert--error">{error}</div>}
        </div>
        <div className="modal-footer modal-footer--end">
          <button
            className="btn btn-outline-danger"
            type="button"
            onClick={() =>
              onConfirm({
                reason,
                overridePeriodLock,
                overrideReason: overrideReason || undefined,
              })
            }
            disabled={loading || reason.trim().length === 0}
          >
            {loading ? 'Đang hủy...' : 'Xác nhận hủy'}
          </button>
        </div>
      </div>
    </div>
  )
}
