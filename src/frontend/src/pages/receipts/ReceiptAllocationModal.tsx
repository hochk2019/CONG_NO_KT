import { useEffect, useMemo, useState } from 'react'
import { previewReceipt, type ReceiptOpenItem, type ReceiptTargetRef } from '../../api/receipts'
import { allocationPriorityLabels, targetTypeLabels } from './receiptLabels'
import { formatDate, formatMoney } from '../../utils/format'
import { ApiError } from '../../api/client'

type ReceiptAllocationModalProps = {
  isOpen: boolean
  token: string
  sellerTaxCode: string
  customerTaxCode: string
  amount: number
  allocationPriority: string
  onPriorityChange: (value: string) => void
  openItems: ReceiptOpenItem[]
  selectedTargets: ReceiptTargetRef[]
  onApply: (targets: ReceiptTargetRef[]) => void
  confirmLabel?: string
  onClose: () => void
}

export default function ReceiptAllocationModal({
  isOpen,
  token,
  sellerTaxCode,
  customerTaxCode,
  amount,
  allocationPriority,
  onPriorityChange,
  openItems,
  selectedTargets,
  onApply,
  confirmLabel,
  onClose,
}: ReceiptAllocationModalProps) {
  const [selectedSet, setSelectedSet] = useState<Set<string>>(new Set())
  const [previewLoading, setPreviewLoading] = useState(false)
  const [previewError, setPreviewError] = useState<string | null>(null)
  const [preview, setPreview] = useState<{
    lines: { targetId: string; targetType: string; amount: number }[]
    unallocatedAmount: number
  } | null>(null)

  const orderedItems = useMemo(() => {
    const items = [...openItems]
    const useDueDate = allocationPriority === 'DUE_DATE'
    return items.sort((a, b) => {
      const dateA = useDueDate ? a.dueDate : a.issueDate
      const dateB = useDueDate ? b.dueDate : b.issueDate
      const dateCompare = dateA.localeCompare(dateB)
      if (dateCompare !== 0) return dateCompare
      const typeA = a.targetType === 'INVOICE' ? 0 : 1
      const typeB = b.targetType === 'INVOICE' ? 0 : 1
      if (typeA !== typeB) return typeA - typeB
      return (a.documentNo ?? '').localeCompare(b.documentNo ?? '')
    })
  }, [openItems, allocationPriority])

  useEffect(() => {
    if (!isOpen) return
    if (selectedTargets.length > 0) {
      setSelectedSet(new Set(selectedTargets.map((item) => item.id)))
      return
    }
    setSelectedSet(new Set(orderedItems.map((item) => item.targetId)))
  }, [isOpen, orderedItems, selectedTargets])

  const selectedTargetsOrdered = useMemo(() => {
    return orderedItems
      .filter((item) => selectedSet.has(item.targetId))
      .map((item) => ({ id: item.targetId, targetType: item.targetType }))
  }, [orderedItems, selectedSet])

  useEffect(() => {
    if (!isOpen) return
    if (!token || amount <= 0 || selectedTargetsOrdered.length === 0) {
      setPreview(null)
      return
    }

    let isActive = true
    const runPreview = async () => {
      setPreviewLoading(true)
      setPreviewError(null)
      try {
        const result = await previewReceipt(token, {
          sellerTaxCode,
          customerTaxCode,
          amount,
          allocationMode: 'MANUAL',
          selectedTargets: selectedTargetsOrdered,
        })
        if (!isActive) return
        setPreview(result)
      } catch (err) {
        if (!isActive) return
        if (err instanceof ApiError) {
          setPreviewError(err.message)
        } else {
          setPreviewError('Không xem trước được phân bổ.')
        }
      } finally {
        if (isActive) setPreviewLoading(false)
      }
    }

    runPreview()
    return () => {
      isActive = false
    }
  }, [isOpen, token, sellerTaxCode, customerTaxCode, amount, selectedTargetsOrdered])

  if (!isOpen) return null

  const handleToggle = (targetId: string) => {
    setSelectedSet((prev) => {
      const next = new Set(prev)
      if (next.has(targetId)) {
        next.delete(targetId)
      } else {
        next.add(targetId)
      }
      return next
    })
  }

  const handleSelectAll = () => {
    setSelectedSet(new Set(orderedItems.map((item) => item.targetId)))
  }

  const handleClear = () => {
    setSelectedSet(new Set())
  }

  const handleApply = () => {
    onApply(selectedTargetsOrdered)
  }

  return (
    <div className="modal-backdrop">
      <button
        type="button"
        className="modal-scrim"
        aria-label="Đóng hộp thoại"
        onClick={onClose}
      />
      <div
        className="modal modal-wide"
        role="dialog"
        aria-modal="true"
        aria-labelledby="receipt-allocation-modal-title"
      >
        <div className="modal-header">
          <div>
            <h3 id="receipt-allocation-modal-title">Phân bổ phiếu thu</h3>
            <p className="muted">Chọn chứng từ sẽ được phân bổ theo thứ tự ưu tiên.</p>
          </div>
          <button className="btn btn-ghost btn-table" type="button" onClick={onClose}>
            Đóng
          </button>
        </div>
        <div className="modal-body">
          <div className="modal-toolbar">
            <label className="field field-inline">
              <span>Ưu tiên theo</span>
              <select
                value={allocationPriority}
                onChange={(event) => onPriorityChange(event.target.value)}
              >
                <option value="ISSUE_DATE">{allocationPriorityLabels.ISSUE_DATE}</option>
                <option value="DUE_DATE">{allocationPriorityLabels.DUE_DATE}</option>
              </select>
            </label>
            <div className="inline-actions">
              <button className="btn btn-outline btn-sm" type="button" onClick={handleSelectAll}>
                Chọn theo ưu tiên
              </button>
              <button className="btn btn-ghost btn-sm" type="button" onClick={handleClear}>
                Bỏ chọn
              </button>
            </div>
          </div>

          <div className="table-scroll">
            <table className="table">
              <thead className="table-head">
                <tr className="table-row">
                  <th scope="col">Chọn</th>
                  <th scope="col">Loại</th>
                  <th scope="col">Số chứng từ</th>
                  <th scope="col">Ngày chứng từ</th>
                  <th scope="col">Ngày đến hạn</th>
                  <th scope="col" className="text-right">
                    Còn lại
                  </th>
                </tr>
              </thead>
              <tbody>
                {orderedItems.map((item) => {
                  const checked = selectedSet.has(item.targetId)
                  return (
                    <tr className="table-row" key={item.targetId}>
                      <td>
                        <input
                          type="checkbox"
                          checked={checked}
                          onChange={() => handleToggle(item.targetId)}
                        />
                      </td>
                      <td>{targetTypeLabels[item.targetType] ?? item.targetType}</td>
                      <td>{item.documentNo ?? item.targetId}</td>
                      <td>{formatDate(item.issueDate)}</td>
                      <td>{formatDate(item.dueDate)}</td>
                      <td className="text-right">{formatMoney(item.outstandingAmount)}</td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>

          <div className="summary-grid">
            <div>
              <strong>{selectedTargetsOrdered.length}</strong>
              <span>Chứng từ đã chọn</span>
            </div>
            <div>
              <strong>{formatMoney(amount)}</strong>
              <span>Số tiền phiếu thu</span>
            </div>
          </div>

          {previewLoading && <p className="muted">Đang xem trước phân bổ...</p>}
          {previewError && <div className="alert alert--error">{previewError}</div>}
          {preview && (
            <div className="table-scroll">
              <table className="table">
                <thead className="table-head">
                  <tr className="table-row">
                    <th scope="col">Chứng từ</th>
                    <th scope="col">Loại</th>
                    <th scope="col" className="text-right">
                      Số tiền phân bổ
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {preview.lines.map((line) => (
                    <tr className="table-row" key={`${line.targetType}-${line.targetId}`}>
                      <td>{line.targetId}</td>
                      <td>{targetTypeLabels[line.targetType] ?? line.targetType}</td>
                      <td className="text-right">{formatMoney(line.amount)}</td>
                    </tr>
                  ))}
                  <tr className="table-row table-row--summary">
                    <td colSpan={2}>Chưa phân bổ</td>
                    <td className="text-right">{formatMoney(preview.unallocatedAmount)}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          )}
        </div>
        <div className="modal-footer">
          <button className="btn btn-ghost" type="button" onClick={onClose}>
            Đóng
          </button>
          <button
            className="btn btn-primary"
            type="button"
            onClick={handleApply}
            disabled={selectedTargetsOrdered.length === 0}
          >
            {confirmLabel ?? 'Lưu phân bổ'}
          </button>
        </div>
      </div>
    </div>
  )
}
