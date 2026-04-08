import type { AdvanceHistoryItem, AdvanceListItem } from '../../api/advances'

type AdvanceHistoryModalProps = {
  advance: AdvanceListItem | null
  error: string | null
  items: AdvanceHistoryItem[]
  loading: boolean
  onClose: () => void
  open: boolean
}

function formatPayload(payload?: string | null) {
  if (!payload) return '-'
  try {
    return JSON.stringify(JSON.parse(payload), null, 2)
  } catch {
    return payload
  }
}

export default function AdvanceHistoryModal({
  advance,
  error,
  items,
  loading,
  onClose,
  open,
}: AdvanceHistoryModalProps) {
  if (!open || !advance) return null

  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true" aria-labelledby="advance-history-title">
      <div className="modal-scrim" onClick={onClose} />
      <div className="modal modal--narrow">
        <header className="modal-header">
          <div>
            <h3 id="advance-history-title">Lịch sử điều chỉnh khoản trả hộ</h3>
            <p className="text-muted">{advance.advanceNo ?? advance.id}</p>
          </div>
          <button className="btn btn-ghost btn-table" type="button" onClick={onClose} aria-label="Đóng">
            ×
          </button>
        </header>
        <div className="modal-body">
          {loading && <p className="text-muted">Đang tải lịch sử...</p>}
          {!loading && error && <div className="alert alert--error">{error}</div>}
          {!loading && !error && items.length === 0 && <p className="text-muted">Chưa có lịch sử điều chỉnh.</p>}
          {!loading &&
            !error &&
            items.map((item) => (
              <article className="advanced-panel" key={item.id}>
                <div className="summary-grid">
                  <div>
                    <dt>Hành động</dt>
                    <dd>{item.action}</dd>
                  </div>
                  <div>
                    <dt>Người thực hiện</dt>
                    <dd>{item.userName || '-'}</dd>
                  </div>
                  <div>
                    <dt>Thời gian</dt>
                    <dd>{new Date(item.createdAt).toLocaleString('vi-VN')}</dd>
                  </div>
                </div>
                <div className="field">
                  <label>Trước thay đổi</label>
                  <pre className="advanced-panel">{formatPayload(item.beforeData)}</pre>
                </div>
                <div className="field">
                  <label>Sau thay đổi</label>
                  <pre className="advanced-panel">{formatPayload(item.afterData)}</pre>
                </div>
              </article>
            ))}
        </div>
        <footer className="modal-footer modal-footer--end">
          <button className="btn btn-primary" type="button" onClick={onClose}>
            Đóng
          </button>
        </footer>
      </div>
    </div>
  )
}
