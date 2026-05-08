import type { FormEvent } from 'react'
import { useState } from 'react'
import type { ReceiptCorrectionRequest, ReceiptDto } from '../../api/receipts'
import { methodLabels } from './receiptLabels'

type ReceiptCorrectionModalProps = {
  error: string | null
  loading: boolean
  onClose: () => void
  onSubmit: (payload: ReceiptCorrectionRequest) => void
  open: boolean
  receipt: ReceiptDto | null
}

export default function ReceiptCorrectionModal({
  error,
  loading,
  onClose,
  onSubmit,
  open,
  receipt,
}: ReceiptCorrectionModalProps) {
  if (!open) return null

  if (!receipt) {
    return (
      <div className="modal-backdrop" role="dialog" aria-modal="true" aria-labelledby="receipt-correction-title">
        <div className="modal-scrim" onClick={loading ? undefined : onClose} />
        <div className="modal modal--narrow">
          <header className="modal-header">
            <div>
              <h3 id="receipt-correction-title">Điều chỉnh phiếu thu</h3>
              <p className="text-muted">Đang tải...</p>
            </div>
            <button
              className="btn btn-ghost btn-table"
              type="button"
              onClick={onClose}
              aria-label="Đóng"
              disabled={loading}
            >
              ×
            </button>
          </header>
          <div className="modal-body">
            <p className="text-muted">Đang tải thông tin phiếu thu...</p>
            {error && <div className="alert alert--error">{error}</div>}
          </div>
          <footer className="modal-footer modal-footer--end">
            <button className="btn btn-ghost" type="button" onClick={onClose} disabled={loading}>
              Đóng
            </button>
          </footer>
        </div>
      </div>
    )
  }

  return (
    <ReceiptCorrectionModalForm
      key={`${receipt.id}:${receipt.version}`}
      error={error}
      loading={loading}
      onClose={onClose}
      onSubmit={onSubmit}
      receipt={receipt}
    />
  )
}

type ReceiptCorrectionModalFormProps = {
  error: string | null
  loading: boolean
  onClose: () => void
  onSubmit: (payload: ReceiptCorrectionRequest) => void
  receipt: ReceiptDto
}

function ReceiptCorrectionModalForm({
  error,
  loading,
  onClose,
  onSubmit,
  receipt,
}: ReceiptCorrectionModalFormProps) {
  const [receiptNo, setReceiptNo] = useState(() => receipt.receiptNo ?? '')
  const [receiptDate, setReceiptDate] = useState(() => receipt.receiptDate ?? '')
  const [amount, setAmount] = useState(() => (Number.isFinite(receipt.amount) ? String(receipt.amount) : ''))
  const [method, setMethod] = useState(() => receipt.method || 'BANK')
  const [description, setDescription] = useState(() => receipt.description ?? '')
  const [reason, setReason] = useState('')
  const [validationError, setValidationError] = useState<string | null>(null)

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()

    const trimmedReason = reason.trim()
    if (!trimmedReason) {
      setValidationError('Vui lòng nhập lý do điều chỉnh.')
      return
    }

    const trimmedAmount = amount.trim()
    let parsedAmount: number | null = null
    if (trimmedAmount) {
      parsedAmount = Number(trimmedAmount)
      if (!Number.isFinite(parsedAmount)) {
        setValidationError('Số tiền không hợp lệ.')
        return
      }
    }

    setValidationError(null)
    onSubmit({
      receiptNo: receiptNo.trim() || null,
      receiptDate: receiptDate.trim() || null,
      amount: parsedAmount,
      method: method || null,
      description: description.trim() || null,
      allocationMode: receipt.allocationMode ?? null,
      appliedPeriodStart: receipt.appliedPeriodStart ?? null,
      allocationPriority: receipt.allocationPriority ?? null,
      selectedTargets: receipt.selectedTargets ?? null,
      reason: trimmedReason,
      version: receipt.version,
    })
  }

  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true" aria-labelledby="receipt-correction-title">
      <div className="modal-scrim" onClick={loading ? undefined : onClose} />
      <form className="modal modal--narrow" onSubmit={handleSubmit}>
        <header className="modal-header">
          <div>
            <h3 id="receipt-correction-title">Điều chỉnh phiếu thu</h3>
            <p className="text-muted">{receipt?.receiptNo?.trim() || receipt?.id || 'Đang tải...'}</p>
          </div>
          <button
            className="btn btn-ghost btn-table"
            type="button"
            onClick={onClose}
            aria-label="Đóng"
            disabled={loading}
          >
            ×
          </button>
        </header>
        <div className="modal-body">
          <div className="field">
            <label htmlFor="receipt-correction-no">Số chứng từ</label>
            <input
              id="receipt-correction-no"
              className="input"
              value={receiptNo}
              onChange={(event) => setReceiptNo(event.target.value)}
              disabled={loading}
            />
          </div>
          <div className="field">
            <label htmlFor="receipt-correction-date">Ngày chứng từ</label>
            <input
              id="receipt-correction-date"
              className="input"
              type="date"
              value={receiptDate}
              onChange={(event) => setReceiptDate(event.target.value)}
              disabled={loading}
            />
          </div>
          <div className="field">
            <label htmlFor="receipt-correction-amount">Số tiền</label>
            <input
              id="receipt-correction-amount"
              className="input"
              type="number"
              min="0"
              step="1"
              value={amount}
              onChange={(event) => setAmount(event.target.value)}
              disabled={loading}
            />
          </div>
          <div className="field">
            <label htmlFor="receipt-correction-method">Hình thức</label>
            <select
              id="receipt-correction-method"
              className="input"
              value={method}
              onChange={(event) => setMethod(event.target.value)}
              disabled={loading}
            >
              <option value="BANK">{methodLabels.BANK}</option>
              <option value="CASH">{methodLabels.CASH}</option>
              <option value="OTHER">{methodLabels.OTHER}</option>
            </select>
          </div>
          <div className="field">
            <label htmlFor="receipt-correction-description">Ghi chú</label>
            <textarea
              id="receipt-correction-description"
              className="input"
              rows={3}
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              disabled={loading}
            />
          </div>
          <div className="field">
            <label htmlFor="receipt-correction-reason">Lý do điều chỉnh</label>
            <textarea
              id="receipt-correction-reason"
              className="input"
              rows={3}
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              disabled={loading}
              required
            />
          </div>
          {(validationError || error) && <div className="alert alert--error">{validationError ?? error}</div>}
        </div>
        <footer className="modal-footer modal-footer--end">
          <button className="btn btn-ghost" type="button" onClick={onClose} disabled={loading}>
            Đóng
          </button>
          <button className="btn btn-primary" type="submit" disabled={loading}>
            {loading ? 'Đang lưu...' : 'Lưu điều chỉnh'}
          </button>
        </footer>
      </form>
    </div>
  )
}
