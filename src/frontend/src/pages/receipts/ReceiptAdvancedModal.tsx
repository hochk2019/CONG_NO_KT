import { useState } from 'react'

type ReceiptAdvancedModalProps = {
  isOpen: boolean
  overridePeriodLock: boolean
  overrideReason: string
  onSave: (overridePeriodLock: boolean, overrideReason: string) => void
  onClose: () => void
}

export default function ReceiptAdvancedModal({
  isOpen,
  overridePeriodLock,
  overrideReason,
  onSave,
  onClose,
}: ReceiptAdvancedModalProps) {
  const [draftOverride, setDraftOverride] = useState(overridePeriodLock)
  const [draftReason, setDraftReason] = useState(overrideReason)

  if (!isOpen) return null

  const canSave = !draftOverride || draftReason.trim().length > 0

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
        aria-labelledby="receipt-advanced-title"
      >
        <div className="modal-header">
          <div>
            <h3 id="receipt-advanced-title">Tùy chọn nâng cao</h3>
            <p className="muted">Chỉ bật khi cần vượt khóa kỳ.</p>
          </div>
          <button className="btn btn-outline" type="button" onClick={onClose}>
            Đóng
          </button>
        </div>
        <div className="modal-body">
          <label className="field field-inline toggle">
            <input
              type="checkbox"
              checked={draftOverride}
              onChange={(event) => setDraftOverride(event.target.checked)}
            />
            <span>Vượt khóa kỳ khi duyệt</span>
          </label>
          {draftOverride && (
            <label className="field field-span-full field-wide receipt-advanced-reason">
              <span>Lý do vượt khóa kỳ</span>
              <input
                value={draftReason}
                onChange={(event) => setDraftReason(event.target.value)}
                placeholder="Bắt buộc nếu vượt khóa kỳ"
              />
            </label>
          )}
        </div>
        <div className="modal-footer">
          <button className="btn btn-outline" type="button" onClick={onClose}>
            Hủy
          </button>
          <button
            className="btn btn-primary"
            type="button"
            onClick={() => onSave(draftOverride, draftReason.trim())}
            disabled={!canSave}
          >
            Lưu tùy chọn
          </button>
        </div>
      </div>
    </div>
  )
}
