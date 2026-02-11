type ReportsValidationModalProps = {
  open: boolean
  title: string
  message: string
  onClose: () => void
  confirmLabel?: string
  onConfirm?: () => void
}

export function ReportsValidationModal({
  open,
  title,
  message,
  onClose,
  confirmLabel,
  onConfirm,
}: ReportsValidationModalProps) {
  if (!open) return null

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
        aria-labelledby="reports-validation-title"
      >
        <div className="modal-header">
          <div>
            <h3 id="reports-validation-title">{title}</h3>
            <p className="muted">Vui lòng bổ sung thông tin để tiếp tục.</p>
          </div>
        </div>
        <div className="modal-body">
          <div className="alert alert--warn" role="alert">
            {message}
          </div>
        </div>
        <div className="modal-footer modal-footer--end">
          {onConfirm && (
            <button type="button" className="btn btn-primary" onClick={onConfirm}>
              {confirmLabel ?? 'Tiếp tục'}
            </button>
          )}
          <button type="button" className="btn btn-outline" onClick={onClose}>
            Đóng
          </button>
        </div>
      </div>
    </div>
  )
}
