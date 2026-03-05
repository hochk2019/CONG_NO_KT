import { useState } from 'react'

export type ActionConfirmPayload = {
  reason: string
  overridePeriodLock: boolean
  overrideReason?: string
}

type ActionConfirmModalProps = {
  isOpen: boolean
  title: string
  description?: string
  confirmLabel?: string
  cancelLabel?: string
  reasonLabel?: string
  reasonPlaceholder?: string
  reasonRequired?: boolean
  showOverrideOption?: boolean
  overrideLabel?: string
  overrideReasonLabel?: string
  overrideReasonPlaceholder?: string
  loading?: boolean
  error?: string | null
  tone?: 'primary' | 'danger'
  onClose: () => void
  onConfirm: (payload: ActionConfirmPayload) => void
}

export default function ActionConfirmModal({
  isOpen,
  ...props
}: ActionConfirmModalProps) {
  if (!isOpen) return null

  return <ActionConfirmModalContent {...props} />
}

type ActionConfirmModalContentProps = Omit<ActionConfirmModalProps, 'isOpen'>

function ActionConfirmModalContent({
  title,
  description,
  confirmLabel = 'Xác nhận',
  cancelLabel = 'Đóng',
  reasonLabel = 'Lý do',
  reasonPlaceholder = 'Nhập lý do',
  reasonRequired = false,
  showOverrideOption = false,
  overrideLabel = 'Vượt khóa kỳ',
  overrideReasonLabel = 'Lý do vượt khóa kỳ',
  overrideReasonPlaceholder = 'Nhập lý do vượt khóa kỳ',
  loading = false,
  error,
  tone = 'primary',
  onClose,
  onConfirm,
}: ActionConfirmModalContentProps) {
  const [reason, setReason] = useState('')
  const [overridePeriodLock, setOverridePeriodLock] = useState(false)
  const [overrideReason, setOverrideReason] = useState('')

  const needsOverrideReason = showOverrideOption && overridePeriodLock
  const canSubmit =
    !loading &&
    (!reasonRequired || reason.trim().length > 0) &&
    (!needsOverrideReason || overrideReason.trim().length > 0)
  const confirmClassName = tone === 'danger' ? 'btn btn-outline-danger' : 'btn btn-primary'

  return (
    <div className="modal-backdrop">
      <button
        type="button"
        className="modal-scrim"
        aria-label="Đóng hộp thoại"
        onClick={onClose}
      />
      <div className="modal modal--narrow" role="dialog" aria-modal="true" aria-labelledby="action-confirm-title">
        <div className="modal-header">
          <div>
            <h3 id="action-confirm-title">{title}</h3>
            {description && <p className="muted">{description}</p>}
          </div>
          <button className="btn btn-ghost btn-table" type="button" onClick={onClose}>
            {cancelLabel}
          </button>
        </div>
        <div className="modal-body">
          {reasonRequired && (
            <label className="field">
              <span>{reasonLabel}</span>
              <input
                value={reason}
                onChange={(event) => setReason(event.target.value)}
                placeholder={reasonPlaceholder}
              />
            </label>
          )}

          {showOverrideOption && (
            <details className="advanced-panel">
              <summary>Tùy chọn nâng cao</summary>
              <div className="advanced-panel__content">
                <label className="field field-inline">
                  <input
                    type="checkbox"
                    checked={overridePeriodLock}
                    onChange={(event) => setOverridePeriodLock(event.target.checked)}
                  />
                  <span>{overrideLabel}</span>
                </label>
                {overridePeriodLock && (
                  <label className="field">
                    <span>{overrideReasonLabel}</span>
                    <input
                      value={overrideReason}
                      onChange={(event) => setOverrideReason(event.target.value)}
                      placeholder={overrideReasonPlaceholder}
                    />
                  </label>
                )}
              </div>
            </details>
          )}

          {error && <div className="alert alert--error">{error}</div>}
        </div>
        <div className="modal-footer modal-footer--end">
          <button
            className={confirmClassName}
            type="button"
            onClick={() =>
              onConfirm({
                reason: reason.trim(),
                overridePeriodLock: showOverrideOption ? overridePeriodLock : false,
                overrideReason: needsOverrideReason ? overrideReason.trim() : undefined,
              })
            }
            disabled={!canSubmit}
          >
            {loading ? 'Đang xử lý...' : confirmLabel}
          </button>
        </div>
      </div>
    </div>
  )
}
