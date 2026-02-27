import { useEffect, useState } from 'react'
import {
  dashboardWidgetLabels,
  type DashboardWidgetId,
} from './dashboardWidgetPreferences'

type DashboardWidgetSettingsProps = {
  order: DashboardWidgetId[]
  hiddenWidgets: DashboardWidgetId[]
  onToggleVisibility: (widgetId: DashboardWidgetId, visible: boolean) => void
  onMove: (widgetId: DashboardWidgetId, direction: 'up' | 'down') => void
  triggerClassName?: string
}

export default function DashboardWidgetSettings({
  order,
  hiddenWidgets,
  onToggleVisibility,
  onMove,
  triggerClassName,
}: DashboardWidgetSettingsProps) {
  const [isOpen, setIsOpen] = useState(false)

  useEffect(() => {
    if (!isOpen) return

    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setIsOpen(false)
      }
    }

    window.addEventListener('keydown', handleEscape)
    return () => window.removeEventListener('keydown', handleEscape)
  }, [isOpen])

  return (
    <>
      <button
        type="button"
        className={`btn btn-outline${triggerClassName ? ` ${triggerClassName}` : ''}`}
        aria-haspopup="dialog"
        aria-expanded={isOpen}
        onClick={() => setIsOpen(true)}
      >
        Tùy chỉnh Dashboard
      </button>
      {isOpen && (
        <div className="modal-backdrop">
          <button
            type="button"
            className="modal-scrim"
            aria-label="Đóng tùy chỉnh Dashboard"
            onClick={() => setIsOpen(false)}
          />
          <section
            className="modal modal--narrow dashboard-widget-settings-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="dashboard-widget-settings-title"
          >
            <div className="modal-header">
              <div>
                <h3 id="dashboard-widget-settings-title">Tùy chỉnh Dashboard</h3>
                <p className="muted">Ẩn/hiện widget và đổi thứ tự hiển thị.</p>
              </div>
              <button type="button" className="btn btn-ghost" onClick={() => setIsOpen(false)}>
                Đóng
              </button>
            </div>
            <div className="modal-body">
              <div className="dashboard-widget-settings__list">
                {order.map((widgetId, index) => {
                  const item = dashboardWidgetLabels[widgetId]
                  const visible = !hiddenWidgets.includes(widgetId)
                  return (
                    <div className="dashboard-widget-settings__item" key={widgetId}>
                      <label className="dashboard-widget-settings__toggle">
                        <input
                          type="checkbox"
                          checked={visible}
                          onChange={(event) => onToggleVisibility(widgetId, event.target.checked)}
                        />
                        <span>
                          <strong>{item.title}</strong>
                          <small>{item.description}</small>
                        </span>
                      </label>
                      <div className="dashboard-widget-settings__actions">
                        <button
                          type="button"
                          className="btn btn-ghost"
                          onClick={() => onMove(widgetId, 'up')}
                          disabled={index === 0}
                          aria-label={`Đưa ${item.title} lên trên`}
                        >
                          ↑
                        </button>
                        <button
                          type="button"
                          className="btn btn-ghost"
                          onClick={() => onMove(widgetId, 'down')}
                          disabled={index === order.length - 1}
                          aria-label={`Đưa ${item.title} xuống dưới`}
                        >
                          ↓
                        </button>
                      </div>
                    </div>
                  )
                })}
              </div>
            </div>
            <div className="modal-footer modal-footer--end">
              <span className="muted text-caption">Tự động lưu theo tài khoản</span>
            </div>
          </section>
        </div>
      )}
    </>
  )
}
