import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useNotificationCenter } from '../../context/useNotificationCenter'

const getToastDuration = (severity: string) => {
  const token = severity.toUpperCase()
  if (token === 'WARN') return 10000
  return 5000
}

export default function NotificationToastHost() {
  const navigate = useNavigate()
  const { toasts, criticalModal, dismissToast, dismissCritical } = useNotificationCenter()

  useEffect(() => {
    if (toasts.length === 0) return
    const timers = toasts.map((toast) =>
      window.setTimeout(() => dismissToast(toast.id), getToastDuration(toast.severity)),
    )
    return () => {
      timers.forEach((timer) => window.clearTimeout(timer))
    }
  }, [dismissToast, toasts])

  return (
    <>
      <div className="toast-stack" aria-live="polite" aria-atomic="true">
        {toasts.map((toast) => (
          <div className={`toast toast--${toast.severity.toLowerCase()}`} key={toast.id}>
            <div>
              <div className="toast__title">{toast.title}</div>
              {toast.body && <div className="toast__body">{toast.body}</div>}
            </div>
            <div className="toast__actions">
              <button
                type="button"
                className="btn btn-ghost btn-table"
                onClick={() => {
                  dismissToast(toast.id)
                  navigate('/notifications')
                }}
              >
                Xem
              </button>
              <button type="button" className="btn btn-ghost btn-table" onClick={() => dismissToast(toast.id)}>
                Đóng
              </button>
            </div>
          </div>
        ))}
      </div>

      {criticalModal && (
        <div className="modal-backdrop">
          <button
            type="button"
            className="modal-scrim"
            aria-label="Đóng hộp thoại"
            onClick={dismissCritical}
          />
          <div
            className="modal modal--narrow"
            role="dialog"
            aria-modal="true"
            aria-labelledby="critical-notification-title"
          >
            <div className="modal-header">
              <div>
                <h3 id="critical-notification-title">{criticalModal.title}</h3>
                <p>Thông báo quan trọng cần được lưu ý.</p>
              </div>
              <button type="button" className="btn btn-ghost" onClick={dismissCritical} aria-label="Đóng">
                ✕
              </button>
            </div>
            <div className="modal-body">
              <div className="muted">{criticalModal.body}</div>
            </div>
            <div className="modal-footer modal-footer--end">
              <button
                type="button"
                className="btn btn-secondary"
                onClick={() => {
                  dismissCritical()
                  navigate('/notifications')
                }}
              >
                Mở chi tiết
              </button>
              <button type="button" className="btn btn-primary" onClick={dismissCritical}>
                Đã hiểu
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}
