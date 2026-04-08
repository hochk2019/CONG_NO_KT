import type { FormEvent } from 'react'
import { useState } from 'react'
import type { AdvanceCorrectionRequest, AdvanceListItem } from '../../api/advances'

type AdvanceCorrectionModalProps = {
  advance: AdvanceListItem | null
  error: string | null
  loading: boolean
  onClose: () => void
  onSubmit: (payload: AdvanceCorrectionRequest) => void
  open: boolean
}

export default function AdvanceCorrectionModal({
  advance,
  error,
  loading,
  onClose,
  onSubmit,
  open,
}: AdvanceCorrectionModalProps) {
  if (!open || !advance) return null

  return (
    <AdvanceCorrectionModalForm
      key={`${advance.id}:${advance.version}`}
      advance={advance}
      error={error}
      loading={loading}
      onClose={onClose}
      onSubmit={onSubmit}
    />
  )
}

type AdvanceCorrectionModalFormProps = {
  advance: AdvanceListItem
  error: string | null
  loading: boolean
  onClose: () => void
  onSubmit: (payload: AdvanceCorrectionRequest) => void
}

function AdvanceCorrectionModalForm({
  advance,
  error,
  loading,
  onClose,
  onSubmit,
}: AdvanceCorrectionModalFormProps) {
  const [advanceNo, setAdvanceNo] = useState(() => advance.advanceNo ?? '')
  const [advanceDate, setAdvanceDate] = useState(() => advance.advanceDate ?? '')
  const [amount, setAmount] = useState(() => (Number.isFinite(advance.amount) ? String(advance.amount) : ''))
  const [description, setDescription] = useState(() => advance.description ?? '')
  const [reason, setReason] = useState('')
  const [validationError, setValidationError] = useState<string | null>(null)

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const trimmedReason = reason.trim()
    if (!trimmedReason) {
      setValidationError('Vui lòng nhập lý do điều chỉnh.')
      return
    }

    let parsedAmount: number | null = null
    const trimmedAmount = amount.trim()
    if (trimmedAmount) {
      parsedAmount = Number(trimmedAmount)
      if (!Number.isFinite(parsedAmount)) {
        setValidationError('Số tiền không hợp lệ.')
        return
      }
    }

    setValidationError(null)
    onSubmit({
      advanceNo: advanceNo.trim() || null,
      advanceDate: advanceDate.trim() || null,
      amount: parsedAmount,
      description: description.trim() || null,
      reason: trimmedReason,
      version: advance.version,
    })
  }

  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true" aria-labelledby="advance-correction-title">
      <div className="modal-scrim" onClick={loading ? undefined : onClose} />
      <form className="modal modal--narrow" onSubmit={handleSubmit}>
        <header className="modal-header">
          <div>
            <h3 id="advance-correction-title">Điều chỉnh khoản trả hộ</h3>
            <p className="text-muted">{advance.advanceNo ?? advance.id}</p>
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
            <label htmlFor="advance-correction-no">Số chứng từ</label>
            <input
              id="advance-correction-no"
              className="input"
              value={advanceNo}
              onChange={(event) => setAdvanceNo(event.target.value)}
              disabled={loading}
            />
          </div>
          <div className="field">
            <label htmlFor="advance-correction-date">Ngày chứng từ</label>
            <input
              id="advance-correction-date"
              className="input"
              type="date"
              value={advanceDate}
              onChange={(event) => setAdvanceDate(event.target.value)}
              disabled={loading}
            />
          </div>
          <div className="field">
            <label htmlFor="advance-correction-amount">Số tiền</label>
            <input
              id="advance-correction-amount"
              className="input"
              type="number"
              min="0"
              step="1000"
              value={amount}
              onChange={(event) => setAmount(event.target.value)}
              disabled={loading}
            />
          </div>
          <div className="field">
            <label htmlFor="advance-correction-description">Ghi chú</label>
            <textarea
              id="advance-correction-description"
              className="input"
              rows={3}
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              disabled={loading}
            />
          </div>
          <div className="field">
            <label htmlFor="advance-correction-reason">Lý do điều chỉnh</label>
            <textarea
              id="advance-correction-reason"
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
