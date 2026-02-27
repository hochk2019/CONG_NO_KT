import { useEffect } from 'react'

type ImportPreviewModalProps = {
  isOpen: boolean
  onClose: () => void
  batchId?: string
  previewStatus: string
  onPreviewStatusChange: (value: string) => void
  previewPageSize: number
  onPreviewPageSizeChange: (value: number) => void
  previewPageSizes: number[]
  previewLoading: boolean
  previewError: string | null
  preview: {
    page: number
    pageSize: number
    totalRows: number
    okCount: number
    warnCount: number
    errorCount: number
    rows: {
      rowNo: number
      validationStatus: string
      rawData: Record<string, unknown>
      validationMessages: string[]
      actionSuggestion?: string
    }[]
  } | null
  previewTotalPages: number
  onPrevPage: () => void
  onNextPage: () => void
  formatValidationMessages: (messages: string[]) => string
  previewStatusLabels: Record<string, string>
  actionSuggestionLabels: Record<string, string>
}

const isInteractiveElement = (target: EventTarget | null) => {
  if (!(target instanceof HTMLElement)) {
    return false
  }
  const tag = target.tagName
  return (
    target.isContentEditable ||
    tag === 'INPUT' ||
    tag === 'TEXTAREA' ||
    tag === 'SELECT' ||
    tag === 'BUTTON' ||
    tag === 'A'
  )
}

export default function ImportPreviewModal({
  isOpen,
  onClose,
  batchId,
  previewStatus,
  onPreviewStatusChange,
  previewPageSize,
  onPreviewPageSizeChange,
  previewPageSizes,
  previewLoading,
  previewError,
  preview,
  previewTotalPages,
  onPrevPage,
  onNextPage,
  formatValidationMessages,
  previewStatusLabels,
  actionSuggestionLabels,
}: ImportPreviewModalProps) {
  useEffect(() => {
    if (!isOpen) {
      return
    }

    const onKeyDown = (event: KeyboardEvent) => {
      if (isInteractiveElement(event.target)) {
        return
      }

      if (event.key === 'Escape') {
        event.preventDefault()
        onClose()
        return
      }

      if (!preview) {
        return
      }

      const canGoPrev = preview.page > 1
      const canGoNext = preview.page < previewTotalPages

      if (event.key === 'ArrowLeft' || (event.key === 'Enter' && event.shiftKey)) {
        if (!canGoPrev) {
          return
        }
        event.preventDefault()
        onPrevPage()
        return
      }

      if (event.key === 'ArrowRight' || (event.key === 'Enter' && !event.shiftKey)) {
        if (!canGoNext) {
          return
        }
        event.preventDefault()
        onNextPage()
      }
    }

    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [isOpen, onClose, onNextPage, onPrevPage, preview, previewTotalPages])

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
        className="modal modal--wide"
        role="dialog"
        aria-modal="true"
        aria-labelledby="import-preview-modal-title"
      >
        <div className="modal-header">
          <div>
            <h3 id="import-preview-modal-title">Xem trước dữ liệu</h3>
            {batchId && <p className="muted">Lô: {batchId}</p>}
          </div>
          <div className="header-actions">
            <label className="field">
              <span>Trạng thái</span>
              <select value={previewStatus} onChange={(event) => onPreviewStatusChange(event.target.value)}>
                <option value="">Tất cả</option>
                <option value="OK">Hợp lệ</option>
                <option value="WARN">Cảnh báo</option>
                <option value="ERROR">Lỗi</option>
              </select>
            </label>
            <label className="field">
              <span>Kích thước</span>
              <select
                value={previewPageSize}
                onChange={(event) => onPreviewPageSizeChange(Number(event.target.value))}
              >
                {previewPageSizes.map((size) => (
                  <option key={size} value={size}>
                    {size}
                  </option>
                ))}
              </select>
            </label>
            <button className="btn btn-ghost" type="button" onClick={onClose}>
              Đóng
            </button>
          </div>
        </div>
        <div className="modal-body">
          {previewError && <div className="alert alert--error" role="alert" aria-live="assertive">{previewError}</div>}
          {previewLoading ? (
            <div className="empty-state">Đang tải xem trước…</div>
          ) : preview ? (
            <>
              <div className="summary-grid">
                <div>
                  <strong>{preview.totalRows}</strong>
                  <span>Tổng dòng</span>
                </div>
                <div>
                  <strong>{preview.okCount}</strong>
                  <span>Hợp lệ</span>
                </div>
                <div>
                  <strong>{preview.warnCount}</strong>
                  <span>Cảnh báo</span>
                </div>
                <div>
                  <strong>{preview.errorCount}</strong>
                  <span>Lỗi</span>
                </div>
              </div>
              <div className="table-scroll">
                <table className="table table-preview">
                  <thead className="table-head">
                    <tr className="table-row">
                      <th scope="col">Dòng</th>
                      <th scope="col">Trạng thái</th>
                      <th scope="col">Hành động</th>
                      <th scope="col">Thông tin</th>
                    </tr>
                  </thead>
                  <tbody>
                    {preview.rows.map((row) => (
                      <tr className="table-row" key={row.rowNo}>
                        <td>{row.rowNo}</td>
                        <td>
                          <span className={`pill pill-${row.validationStatus.toLowerCase()}`}>
                            {previewStatusLabels[row.validationStatus] ?? row.validationStatus}
                          </span>
                        </td>
                        <td>
                          {row.actionSuggestion
                            ? actionSuggestionLabels[row.actionSuggestion] ?? row.actionSuggestion
                            : '-'}
                        </td>
                        <td>
                          <pre className="code-block">{JSON.stringify(row.rawData, null, 0)}</pre>
                          {row.validationMessages.length > 0 && (
                            <div className="muted">
                              Lý do: {formatValidationMessages(row.validationMessages)}
                            </div>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <div className="table-controls">
                <div className="table-page-info">
                  Trang {preview.page} / {previewTotalPages} (Tổng {preview.totalRows})
                  <div className="muted">
                    Phím tắt: <kbd>Esc</kbd> đóng • <kbd>←</kbd>/<kbd>→</kbd> đổi trang •{' '}
                    <kbd>Enter</kbd>/<kbd>Shift</kbd>+<kbd>Enter</kbd>
                  </div>
                </div>
                <div className="table-page-actions">
                  <button className="btn btn-ghost" type="button" onClick={onPrevPage} disabled={preview.page <= 1}>
                    Trước
                  </button>
                  <button
                    className="btn btn-ghost"
                    type="button"
                    onClick={onNextPage}
                    disabled={preview.page >= previewTotalPages}
                  >
                    Sau
                  </button>
                </div>
              </div>
            </>
          ) : (
            <div className="empty-state">Chưa có xem trước. Tải file và bấm "Xem trước" để kiểm tra dữ liệu.</div>
          )}
        </div>
      </div>
    </div>
  )
}
