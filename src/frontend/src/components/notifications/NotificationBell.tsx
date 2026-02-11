import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useNotificationCenter } from '../../context/useNotificationCenter'
import { formatDateTime } from '../../utils/format'

export default function NotificationBell() {
  const navigate = useNavigate()
  const { unreadCount, unreadItems, refreshUnread, markRead, markAllRead } = useNotificationCenter()
  const [open, setOpen] = useState(false)
  const panelRef = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    if (!open) return
    refreshUnread()
  }, [open, refreshUnread])

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (!panelRef.current) return
      if (!panelRef.current.contains(event.target as Node)) {
        setOpen(false)
      }
    }
    if (open) {
      document.addEventListener('mousedown', handleClickOutside)
    }
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [open])

  return (
    <div className="notification-bell" ref={panelRef}>
      <button
        type="button"
        className="btn btn-ghost notification-bell__button"
        onClick={() => setOpen((prev) => !prev)}
        aria-label="Thông báo"
      >
        <span className="notification-bell__icon">Thông báo</span>
        {unreadCount > 0 && <span className="notification-bell__badge">{unreadCount}</span>}
      </button>
      {open && (
        <div className="notification-panel">
          <div className="notification-panel__header">
            <div>
              <div className="panel-title">Thông báo</div>
              <div className="muted">{unreadCount} chưa đọc</div>
            </div>
            <button type="button" className="btn btn-ghost btn-table" onClick={() => markAllRead()}>
              Đã đọc hết
            </button>
          </div>
          {unreadItems.length === 0 ? (
            <div className="notification-panel__empty">Không có thông báo mới.</div>
          ) : (
            <div className="notification-panel__list">
              {unreadItems.map((item) => (
                <div className="notification-panel__item" key={item.id}>
                  <div>
                    <div className="list-title">{item.title}</div>
                    <div className="muted">{item.body}</div>
                  </div>
                  <div className="notification-panel__meta">
                    <span className="muted">{formatDateTime(item.createdAt)}</span>
                    <button
                      type="button"
                      className="btn btn-ghost btn-table"
                      onClick={() => markRead(item.id)}
                    >
                      Đã đọc
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
          <div className="notification-panel__footer">
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => {
                setOpen(false)
                navigate('/notifications')
              }}
            >
              Xem tất cả
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
