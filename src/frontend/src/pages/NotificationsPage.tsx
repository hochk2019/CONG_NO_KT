import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { ApiError } from '../api/client'
import {
  fetchNotifications,
  markNotificationRead,
  type NotificationItem,
  type NotificationPreferences,
} from '../api/notifications'
import { useAuth } from '../context/AuthStore'
import { useNotificationCenter } from '../context/useNotificationCenter'
import { formatDateTime } from '../utils/format'

type SourceOption = {
  value: string
  label: string
  link: string
}

const SOURCE_OPTIONS: SourceOption[] = [
  { value: 'RISK', label: 'Rủi ro công nợ', link: '/risk' },
  { value: 'RECEIPT', label: 'Phiếu thu', link: '/receipts' },
  { value: 'IMPORT', label: 'Nhập liệu', link: '/imports' },
  { value: 'SYSTEM', label: 'Hệ thống', link: '/dashboard' },
]

const SEVERITY_OPTIONS = [
  { value: 'INFO', label: 'Thông tin' },
  { value: 'WARN', label: 'Cảnh báo' },
  { value: 'ALERT', label: 'Quan trọng' },
]

const normalize = (value: string) => value.toUpperCase()

export default function NotificationsPage() {
  const { state } = useAuth()
  const token = state.accessToken
  const { preferences, updatePreferences, refreshUnread } = useNotificationCenter()

  const [items, setItems] = useState<NotificationItem[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [selected, setSelected] = useState<NotificationItem | null>(null)
  const [unreadOnly, setUnreadOnly] = useState(false)
  const [source, setSource] = useState('')
  const [severity, setSeverity] = useState('')
  const [query, setQuery] = useState('')
  const [settingsDraft, setSettingsDraft] = useState<NotificationPreferences | null>(null)
  const [settingsSaving, setSettingsSaving] = useState(false)
  const [detailItem, setDetailItem] = useState<NotificationItem | null>(null)

  useEffect(() => {
    if (preferences) {
      setSettingsDraft(preferences)
    }
  }, [preferences])

  const sourceMap = useMemo(() => new Map(SOURCE_OPTIONS.map((item) => [item.value, item])), [])
  const selectedLink = selected
    ? sourceMap.get(normalize(selected.source))?.link ?? '/dashboard'
    : '/dashboard'

  const loadNotifications = useCallback(async () => {
    if (!token) return
    setLoading(true)
    setError(null)
    try {
      const result = await fetchNotifications({
        token,
        unreadOnly,
        source: source || undefined,
        severity: severity || undefined,
        query: query || undefined,
        page,
        pageSize,
      })
      setItems(result.items)
      setTotal(result.total)
      setSelected((prev) => {
        if (result.items.length === 0) {
          return null
        }
        if (prev && result.items.some((item) => item.id === prev.id)) {
          return prev
        }
        return result.items[0]
      })
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else {
        setError('Không tải được thông báo.')
      }
    } finally {
      setLoading(false)
    }
  }, [page, pageSize, query, severity, source, token, unreadOnly])

  useEffect(() => {
    loadNotifications()
  }, [loadNotifications])

  const totalPages = Math.max(1, Math.ceil(total / pageSize))

  const markReadAndRefresh = useCallback(
    async (id: string) => {
      if (!token) return
      await markNotificationRead(token, id)
      await loadNotifications()
      await refreshUnread()
    },
    [loadNotifications, refreshUnread, token],
  )

  const handleMarkRead = async (id: string) => {
    await markReadAndRefresh(id)
  }

  const handleSelect = (item: NotificationItem) => {
    setSelected(item)
    if (!item.readAt) {
      void markReadAndRefresh(item.id)
    }
  }

  const handleViewDetail = (item: NotificationItem) => {
    setSelected(item)
    setDetailItem(item)
    if (!item.readAt) {
      void markReadAndRefresh(item.id)
    }
  }

  const handleSettingsToggle = (key: keyof NotificationPreferences, value: boolean) => {
    if (!settingsDraft) return
    setSettingsDraft({ ...settingsDraft, [key]: value })
  }

  const toggleListValue = (key: 'popupSeverities' | 'popupSources', value: string) => {
    if (!settingsDraft) return
    const list = settingsDraft[key]
    const normalized = normalize(value)
    const next = list.includes(normalized) ? list.filter((item) => item !== normalized) : [...list, normalized]
    setSettingsDraft({ ...settingsDraft, [key]: next })
  }

  const handleSaveSettings = async () => {
    if (!settingsDraft) return
    setSettingsSaving(true)
    try {
      await updatePreferences(settingsDraft)
    } finally {
      setSettingsSaving(false)
    }
  }

  return (
    <div className="page notifications-page">
      <header className="page-header">
        <div>
          <h1>Trung tâm thông báo</h1>
          <p className="muted">Theo dõi cảnh báo quan trọng và lịch sử thông báo nội bộ.</p>
        </div>
        <div className="header-actions">
          <button type="button" className="btn btn-secondary" onClick={() => loadNotifications()}>
            Làm mới
          </button>
        </div>
      </header>

      <div className="notification-center">
        <section className="notification-filters card">
          <h3>Bộ lọc</h3>
          <div className="field">
            <label>
              <input
                type="checkbox"
                checked={unreadOnly}
                onChange={(event) => {
                  setUnreadOnly(event.target.checked)
                  setPage(1)
                }}
              />
              Chỉ hiển thị chưa đọc
            </label>
          </div>
          <div className="field">
            <label htmlFor="notification-query">Tìm kiếm</label>
            <input
              id="notification-query"
              type="text"
              placeholder="Nhập từ khóa tiêu đề hoặc nội dung..."
              value={query}
              onChange={(event) => {
                setQuery(event.target.value)
                setPage(1)
              }}
            />
          </div>
          <div className="field">
            <label htmlFor="notification-source">Nguồn</label>
            <select
              id="notification-source"
              value={source}
              onChange={(event) => {
                setSource(event.target.value)
                setPage(1)
              }}
            >
              <option value="">Tất cả</option>
              {SOURCE_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label htmlFor="notification-severity">Mức độ</label>
            <select
              id="notification-severity"
              value={severity}
              onChange={(event) => {
                setSeverity(event.target.value)
                setPage(1)
              }}
            >
              <option value="">Tất cả</option>
              {SEVERITY_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </div>
          <div className="muted">
            Thông báo rủi ro phụ thuộc cấu hình trong mục “Cảnh báo rủi ro”.
          </div>
        </section>

        <section className="notification-list-panel card">
          <div className="notification-list-panel__header">
            <div>
              <h3>Danh sách thông báo</h3>
              <div className="muted">
                Tổng {total} thông báo · Trang {page}/{totalPages}
              </div>
            </div>
            <div className="field-inline">
              <label htmlFor="notification-page-size">Kích thước trang</label>
              <select
                id="notification-page-size"
                value={pageSize}
                onChange={(event) => {
                  setPageSize(Number(event.target.value))
                  setPage(1)
                }}
              >
                {[10, 20, 50].map((size) => (
                  <option key={size} value={size}>
                    {size}
                  </option>
                ))}
              </select>
            </div>
          </div>

          {loading ? (
            <div className="empty-state">Đang tải thông báo...</div>
          ) : error ? (
            <div className="empty-state">{error}</div>
          ) : items.length === 0 ? (
            <div className="empty-state">Không có thông báo phù hợp.</div>
          ) : (
            <div className="notification-list">
              {items.map((item) => (
                <div
                  key={item.id}
                  className={`notification-row${selected?.id === item.id ? ' notification-row--active' : ''}`}
                >
                  <button
                    type="button"
                    className="notification-row__body"
                    onClick={() => handleSelect(item)}
                  >
                    <div className="list-title">{item.title}</div>
                    <div className="muted">{item.body}</div>
                    <div className="notification-row__meta">
                      <span>{item.severity}</span>
                      <span>{item.source}</span>
                    </div>
                  </button>
                  <div className="notification-row__actions">
                    <span className="muted">{formatDateTime(item.createdAt)}</span>
                    <button
                      type="button"
                      className="btn btn-ghost btn-table"
                      onClick={() => handleViewDetail(item)}
                    >
                      Xem chi tiết
                    </button>
                    {item.readAt ? (
                      <span className="pill pill--success">Đã đọc</span>
                    ) : (
                      <button
                        type="button"
                        className="btn btn-ghost btn-table"
                        onClick={() => handleMarkRead(item.id)}
                      >
                        Đã đọc
                      </button>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}

          <div className="pagination">
            <button
              type="button"
              className="btn btn-ghost"
              disabled={page <= 1}
              onClick={() => setPage((prev) => Math.max(1, prev - 1))}
            >
              Trước
            </button>
            <span className="muted">
              Trang {page} / {totalPages}
            </span>
            <button
              type="button"
              className="btn btn-ghost"
              disabled={page >= totalPages}
              onClick={() => setPage((prev) => Math.min(totalPages, prev + 1))}
            >
              Sau
            </button>
          </div>
        </section>

        <section className="notification-detail card">
          <h3>Cài đặt thông báo</h3>
          <div className="notification-settings">
            {!settingsDraft ? (
              <div className="muted">Đang tải cài đặt...</div>
            ) : (
              <>
                <label className="field-inline">
                  <input
                    type="checkbox"
                    checked={settingsDraft.receiveNotifications}
                    onChange={(event) => handleSettingsToggle('receiveNotifications', event.target.checked)}
                  />
                  Nhận thông báo nội bộ
                </label>
                <label className="field-inline">
                  <input
                    type="checkbox"
                    checked={settingsDraft.popupEnabled}
                    onChange={(event) => handleSettingsToggle('popupEnabled', event.target.checked)}
                  />
                  Hiển thị popup/toast
                </label>
                <div className="settings-group">
                  <div className="settings-label">Popup theo mức độ</div>
                  {SEVERITY_OPTIONS.map((option) => (
                    <label key={option.value}>
                      <input
                        type="checkbox"
                        checked={settingsDraft.popupSeverities.includes(option.value)}
                        onChange={() => toggleListValue('popupSeverities', option.value)}
                      />
                      {option.label}
                    </label>
                  ))}
                </div>
                <div className="settings-group">
                  <div className="settings-label">Popup theo nguồn</div>
                  {SOURCE_OPTIONS.map((option) => (
                    <label key={option.value}>
                      <input
                        type="checkbox"
                        checked={settingsDraft.popupSources.includes(option.value)}
                        onChange={() => toggleListValue('popupSources', option.value)}
                      />
                      {option.label}
                    </label>
                  ))}
                </div>
                <button
                  type="button"
                  className="btn btn-primary"
                  disabled={settingsSaving}
                  onClick={handleSaveSettings}
                >
                  {settingsSaving ? 'Đang lưu...' : 'Lưu cài đặt'}
                </button>
              </>
            )}
          </div>
        </section>
      </div>

      {detailItem && (
        <div className="modal-backdrop">
          <button
            type="button"
            className="modal-scrim"
            aria-label="Đóng hộp thoại"
            onClick={() => setDetailItem(null)}
          />
          <div
            className="modal modal--wide"
            role="dialog"
            aria-modal="true"
            aria-labelledby="notification-detail-title"
          >
            <div className="modal-header">
              <div>
                <h3 id="notification-detail-title">{detailItem.title}</h3>
                <p className="muted">Chi tiết thông báo</p>
              </div>
              <button type="button" className="btn btn-ghost" onClick={() => setDetailItem(null)} aria-label="Đóng">
                ✕
              </button>
            </div>
            <div className="modal-body">
              <div className="notification-detail">
                <div className="muted">{detailItem.body}</div>
                <div className="detail-meta">
                  <span>{formatDateTime(detailItem.createdAt)}</span>
                  <span>{detailItem.severity}</span>
                  <span>{detailItem.source}</span>
                </div>
              </div>
            </div>
            <div className="modal-footer modal-footer--end">
              <Link to={selectedLink} className="btn btn-secondary" onClick={() => setDetailItem(null)}>
                Mở liên quan
              </Link>
              <button type="button" className="btn btn-primary" onClick={() => setDetailItem(null)}>
                Đã hiểu
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
