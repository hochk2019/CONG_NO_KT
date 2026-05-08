import type { ReceiptHistoryItem, ReceiptListItem } from '../../api/receipts'

type ReceiptHistoryModalProps = {
  error: string | null
  items: ReceiptHistoryItem[]
  loading: boolean
  onClose: () => void
  open: boolean
  receipt: ReceiptListItem | null
}

function formatPayload(payload?: string | null) {
  if (!payload) return '-'
  try {
    return JSON.stringify(JSON.parse(payload), null, 2)
  } catch {
    return payload
  }
}

export default function ReceiptHistoryModal({
  error,
  items,
  loading,
  onClose,
  open,
  receipt,
}: ReceiptHistoryModalProps) {
  if (!open || !receipt) return null

  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true" aria-labelledby="receipt-history-title">
      <div className="modal-scrim" onClick={onClose} />
      <div className="modal modal--narrow">
        <header className="modal-header">
          <div>
            <h3 id="receipt-history-title">Lịch sử điều chỉnh phiếu thu</h3>
            <p className="text-muted">{receipt.receiptNo?.trim() || receipt.id}</p>
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
                    <div className="text-muted">Hành động</div>
                    <div>{item.action}</div>
                  </div>
                  <div>
                    <div className="text-muted">Người thực hiện</div>
                    <div>{item.userName || '-'}</div>
                  </div>
                  <div>
                    <div className="text-muted">Thời gian</div>
                    <div>{new Date(item.createdAt).toLocaleString('vi-VN')}</div>
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
