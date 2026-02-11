import { useCallback, useEffect, useState } from 'react'
import type { CustomerAdvance, CustomerInvoice, CustomerReceiptRef } from '../../api/customers'
import { ApiError } from '../../api/client'
import { fetchReceiptAllocations, type ReceiptAllocationDetail } from '../../api/receipts'
import { formatDate, formatMoney } from '../../utils/format'

type InvoiceModalState = {
  mode: 'view' | 'void'
  row: CustomerInvoice
}

type AdvanceModalState = {
  mode: 'view' | 'void'
  row: CustomerAdvance
}

type ReceiptModalState = {
  id: string
  receiptNo?: string | null
  receiptDate?: string
  allocatedAmount?: number
}

type CustomerTransactionModalsProps = {
  invoiceModal: InvoiceModalState | null
  advanceModal: AdvanceModalState | null
  receiptModal: ReceiptModalState | null
  token: string
  invoiceStatusLabels: Record<string, string>
  advanceStatusLabels: Record<string, string>
  allocationTypeLabels: Record<string, string>
  onCloseInvoice: () => void
  onCloseAdvance: () => void
  onCloseReceipt: () => void
  onVoidInvoice: () => void
  onVoidAdvance: () => void
  shortId: (value: string) => string
  invoiceVoidReason: string
  onInvoiceVoidReasonChange: (value: string) => void
  invoiceReplacementId: string
  onInvoiceReplacementChange: (value: string) => void
  invoiceVoidLoading: boolean
  invoiceVoidError: string | null
  invoiceVoidSuccess: string | null
  advanceVoidReason: string
  onAdvanceVoidReasonChange: (value: string) => void
  advanceOverrideLock: boolean
  onAdvanceOverrideLockChange: (value: boolean) => void
  advanceOverrideReason: string
  onAdvanceOverrideReasonChange: (value: string) => void
  advanceVoidLoading: boolean
  advanceVoidError: string | null
  advanceVoidSuccess: string | null
  receiptAllocations: ReceiptAllocationDetail[]
  receiptAllocLoading: boolean
  receiptAllocError: string | null
}

export default function CustomerTransactionModals({
  invoiceModal,
  advanceModal,
  receiptModal,
  token,
  invoiceStatusLabels,
  advanceStatusLabels,
  allocationTypeLabels,
  onCloseInvoice,
  onCloseAdvance,
  onCloseReceipt,
  onVoidInvoice,
  onVoidAdvance,
  shortId,
  invoiceVoidReason,
  onInvoiceVoidReasonChange,
  invoiceReplacementId,
  onInvoiceReplacementChange,
  invoiceVoidLoading,
  invoiceVoidError,
  invoiceVoidSuccess,
  advanceVoidReason,
  onAdvanceVoidReasonChange,
  advanceOverrideLock,
  onAdvanceOverrideLockChange,
  advanceOverrideReason,
  onAdvanceOverrideReasonChange,
  advanceVoidLoading,
  advanceVoidError,
  advanceVoidSuccess,
  receiptAllocations,
  receiptAllocLoading,
  receiptAllocError,
}: CustomerTransactionModalsProps) {
  const [expandedReceiptId, setExpandedReceiptId] = useState<string | null>(null)
  const [inlineAllocations, setInlineAllocations] = useState<Record<string, ReceiptAllocationDetail[]>>({})
  const [inlineLoadingId, setInlineLoadingId] = useState<string | null>(null)
  const [inlineError, setInlineError] = useState<Record<string, string>>({})

  useEffect(() => {
    setExpandedReceiptId(null)
    setInlineAllocations({})
    setInlineLoadingId(null)
    setInlineError({})
  }, [invoiceModal?.row.id, advanceModal?.row.id])

  const loadAllocations = useCallback(
    async (receiptId: string) => {
      if (!token) {
        setInlineError((prev) => ({ ...prev, [receiptId]: 'Phiên đăng nhập đã hết hạn.' }))
        return
      }
      setInlineLoadingId(receiptId)
      setInlineError((prev) => ({ ...prev, [receiptId]: '' }))
      try {
        const result = await fetchReceiptAllocations(token, receiptId)
        setInlineAllocations((prev) => ({ ...prev, [receiptId]: result }))
      } catch (err) {
        if (err instanceof ApiError) {
          setInlineError((prev) => ({ ...prev, [receiptId]: err.message }))
        } else {
          setInlineError((prev) => ({
            ...prev,
            [receiptId]: 'Không tải được chi tiết phân bổ phiếu thu.',
          }))
        }
      } finally {
        setInlineLoadingId((prev) => (prev === receiptId ? null : prev))
      }
    },
    [token],
  )

  const handleToggleReceipt = useCallback(
    (receiptId: string) => {
      const nextExpanded = expandedReceiptId === receiptId ? null : receiptId
      setExpandedReceiptId(nextExpanded)
      if (!nextExpanded || inlineAllocations[nextExpanded] || inlineLoadingId === nextExpanded) {
        return
      }
      void loadAllocations(nextExpanded)
    },
    [expandedReceiptId, inlineAllocations, inlineLoadingId, loadAllocations],
  )

  const renderReceiptRefsInline = useCallback(
    (refs: CustomerReceiptRef[]) => {
      if (!refs || refs.length === 0) {
        return <div className="muted">Chưa có phiếu thu liên quan.</div>
      }

      return (
        <div className="stack-section">
          {refs.map((ref) => {
            const isExpanded = expandedReceiptId === ref.id
            const allocationItems = inlineAllocations[ref.id] ?? []
            const errorMessage = inlineError[ref.id]
            const isLoading = inlineLoadingId === ref.id
            const detailId = `receipt-detail-${ref.id}`
            const displayNo = ref.receiptNo?.trim() ? ref.receiptNo : shortId(ref.id)

            return (
              <div key={ref.id}>
                <div className="list-row">
                  <div className="stacked-text">
                    <span className="list-title">
                      {displayNo} · {formatMoney(ref.amount)}
                    </span>
                    <span className="text-caption">Ngày thu: {formatDate(ref.receiptDate)}</span>
                  </div>
                  <button
                    className="btn btn-ghost btn-table"
                    type="button"
                    aria-expanded={isExpanded}
                    aria-controls={detailId}
                    onClick={() => handleToggleReceipt(ref.id)}
                  >
                    {isExpanded ? 'Thu gọn' : 'Xem'}
                  </button>
                </div>
                {isExpanded && (
                  <div className="receipt-summary" id={detailId}>
                    <div className="receipt-allocation-row">
                      <span className="muted">Phân bổ cho chứng từ này</span>
                      <strong>{formatMoney(ref.amount)}</strong>
                    </div>
                    {isLoading && <div className="muted">Đang tải phân bổ...</div>}
                    {errorMessage && (
                      <div className="alert alert--error" role="alert">
                        {errorMessage}
                      </div>
                    )}
                    {!isLoading && !errorMessage && allocationItems.length === 0 && (
                      <div className="muted">Chưa có phân bổ.</div>
                    )}
                    {allocationItems.length > 0 && (
                      <div className="table-wrapper">
                        <table className="table">
                          <thead>
                            <tr>
                              <th>Loại</th>
                              <th>Số chứng từ</th>
                              <th>Ngày</th>
                              <th className="align-right">Số tiền</th>
                            </tr>
                          </thead>
                          <tbody>
                            {allocationItems.map((item) => (
                              <tr key={item.targetId}>
                                <td>{allocationTypeLabels[item.targetType] ?? item.targetType}</td>
                                <td>{item.targetNo?.trim() ? item.targetNo : shortId(item.targetId)}</td>
                                <td>{formatDate(item.targetDate)}</td>
                                <td className="align-right">{formatMoney(item.amount)}</td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    )}
                  </div>
                )}
              </div>
            )
          })}
        </div>
      )
    },
    [
      allocationTypeLabels,
      expandedReceiptId,
      handleToggleReceipt,
      inlineAllocations,
      inlineError,
      inlineLoadingId,
      shortId,
    ],
  )

  return (
    <>
      {invoiceModal && (
        <div className="modal-backdrop">
          <button
            type="button"
            className="modal-scrim"
            aria-label="Đóng hộp thoại"
            onClick={onCloseInvoice}
          />
          <div
            className="modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="customer-invoice-modal-title"
          >
            <div className="modal-header">
              <div>
                <p className="eyebrow">
                  {invoiceModal.mode === 'void' ? 'Hủy hóa đơn' : 'Chi tiết hóa đơn'}
                </p>
                <h3 id="customer-invoice-modal-title">
                  {invoiceModal.row.invoiceNo?.trim()
                    ? invoiceModal.row.invoiceNo
                    : shortId(invoiceModal.row.id)}
                </h3>
                <p className="muted">Ngày phát hành: {formatDate(invoiceModal.row.issueDate)}</p>
              </div>
              <div className="customer-actions">
                <button className="btn btn-ghost btn-table" type="button" onClick={onCloseInvoice}>
                  Đóng
                </button>
              </div>
            </div>
            <div className="modal-body">
              {invoiceModal.mode === 'view' ? (
                <>
                  <div className="summary-grid summary-grid--emphasis">
                    <div>
                      <strong>{formatMoney(invoiceModal.row.totalAmount)}</strong>
                      <span>Tổng tiền</span>
                    </div>
                    <div>
                      <strong>{formatMoney(invoiceModal.row.outstandingAmount)}</strong>
                      <span>Còn lại</span>
                    </div>
                    <div>
                      <strong>
                        {invoiceStatusLabels[invoiceModal.row.status] ?? invoiceModal.row.status}
                      </strong>
                      <span>Trạng thái</span>
                    </div>
                    <div>
                      <strong>{invoiceModal.row.sellerTaxCode}</strong>
                      <span>
                        Bên bán
                        {invoiceModal.row.sellerShortName ? ` (${invoiceModal.row.sellerShortName})` : ''}
                      </span>
                    </div>
                  </div>
                  <div className="section">
                    <h4>Phiếu thu liên quan</h4>
                    {renderReceiptRefsInline(invoiceModal.row.receiptRefs)}
                  </div>
                </>
              ) : (
                <>
                  <div className="summary-grid summary-grid--emphasis">
                    <div>
                      <strong>{formatMoney(invoiceModal.row.totalAmount)}</strong>
                      <span>Tổng tiền</span>
                    </div>
                    <div>
                      <strong>{formatMoney(invoiceModal.row.outstandingAmount)}</strong>
                      <span>Còn lại</span>
                    </div>
                    <div>
                      <strong>
                        {invoiceStatusLabels[invoiceModal.row.status] ?? invoiceModal.row.status}
                      </strong>
                      <span>Trạng thái</span>
                    </div>
                  </div>
                  <div className="form-grid">
                    <label className="field field--full">
                      <span>Lý do hủy</span>
                      <textarea
                        rows={3}
                        value={invoiceVoidReason}
                        onChange={(event) => onInvoiceVoidReasonChange(event.target.value)}
                        placeholder="Nhập lý do hủy hóa đơn"
                      />
                    </label>
                    {(invoiceModal.row.status.toUpperCase() === 'PAID' ||
                      invoiceModal.row.status.toUpperCase() === 'PARTIAL') && (
                      <label className="field field--full">
                        <span>Hóa đơn thay thế (bắt buộc nếu đã thu tiền)</span>
                        <input
                          value={invoiceReplacementId}
                          onChange={(event) => onInvoiceReplacementChange(event.target.value)}
                          placeholder="Nhập ID hóa đơn thay thế"
                        />
                      </label>
                    )}
                  </div>
                  {invoiceVoidError && (
                    <div className="alert alert--error" role="alert">
                      {invoiceVoidError}
                    </div>
                  )}
                  {invoiceVoidSuccess && (
                    <div className="alert alert--success" role="status" aria-live="polite">
                      {invoiceVoidSuccess}
                    </div>
                  )}
                  <div className="inline-actions">
                    <button
                      className="btn btn-outline-danger"
                      type="button"
                      onClick={onVoidInvoice}
                      disabled={invoiceVoidLoading}
                    >
                      {invoiceVoidLoading ? 'Đang hủy...' : 'Xác nhận hủy'}
                    </button>
                    <button className="btn btn-ghost" type="button" onClick={onCloseInvoice}>
                      Đóng
                    </button>
                  </div>
                </>
              )}
            </div>
          </div>
        </div>
      )}

      {advanceModal && (
        <div className="modal-backdrop">
          <button
            type="button"
            className="modal-scrim"
            aria-label="Đóng hộp thoại"
            onClick={onCloseAdvance}
          />
          <div
            className="modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="customer-advance-modal-title"
          >
            <div className="modal-header">
              <div>
                <p className="eyebrow">
                  {advanceModal.mode === 'void' ? 'Hủy khoản trả hộ KH' : 'Chi tiết khoản trả hộ KH'}
                </p>
                <h3 id="customer-advance-modal-title">
                  {advanceModal.row.advanceNo?.trim()
                    ? advanceModal.row.advanceNo
                    : shortId(advanceModal.row.id)}
                </h3>
                <p className="muted">Ngày trả hộ: {formatDate(advanceModal.row.advanceDate)}</p>
              </div>
              <div className="customer-actions">
                <button className="btn btn-ghost btn-table" type="button" onClick={onCloseAdvance}>
                  Đóng
                </button>
              </div>
            </div>
            <div className="modal-body">
              {advanceModal.mode === 'view' ? (
                <>
                  <div className="summary-grid summary-grid--emphasis">
                    <div>
                      <strong>{formatMoney(advanceModal.row.amount)}</strong>
                      <span>Tổng tiền</span>
                    </div>
                    <div>
                      <strong>{formatMoney(advanceModal.row.outstandingAmount)}</strong>
                      <span>Còn lại</span>
                    </div>
                    <div>
                      <strong>
                        {advanceStatusLabels[advanceModal.row.status] ?? advanceModal.row.status}
                      </strong>
                      <span>Trạng thái</span>
                    </div>
                    <div>
                      <strong>{advanceModal.row.sellerTaxCode}</strong>
                      <span>
                        Bên bán
                        {advanceModal.row.sellerShortName ? ` (${advanceModal.row.sellerShortName})` : ''}
                      </span>
                    </div>
                  </div>
                  <div className="section">
                    <h4>Phiếu thu liên quan</h4>
                    {renderReceiptRefsInline(advanceModal.row.receiptRefs)}
                  </div>
                </>
              ) : (
                <>
                  <div className="summary-grid summary-grid--emphasis">
                    <div>
                      <strong>{formatMoney(advanceModal.row.amount)}</strong>
                      <span>Tổng tiền</span>
                    </div>
                    <div>
                      <strong>{formatMoney(advanceModal.row.outstandingAmount)}</strong>
                      <span>Còn lại</span>
                    </div>
                    <div>
                      <strong>
                        {advanceStatusLabels[advanceModal.row.status] ?? advanceModal.row.status}
                      </strong>
                      <span>Trạng thái</span>
                    </div>
                  </div>
                  <div className="form-grid">
                    <label className="field field--full">
                      <span>Lý do hủy</span>
                      <textarea
                        rows={3}
                        value={advanceVoidReason}
                        onChange={(event) => onAdvanceVoidReasonChange(event.target.value)}
                        placeholder="Nhập lý do hủy khoản trả hộ"
                      />
                    </label>
                    <label className="field field--checkbox">
                      <input
                        type="checkbox"
                        checked={advanceOverrideLock}
                        onChange={(event) => onAdvanceOverrideLockChange(event.target.checked)}
                      />
                      <span>Cho phép hủy ngoài kỳ khóa</span>
                    </label>
                    {advanceOverrideLock && (
                      <label className="field field--full">
                        <span>Lý do mở khóa kỳ</span>
                        <input
                          value={advanceOverrideReason}
                          onChange={(event) => onAdvanceOverrideReasonChange(event.target.value)}
                          placeholder="Nhập lý do mở khóa kỳ"
                        />
                      </label>
                    )}
                  </div>
                  {advanceVoidError && (
                    <div className="alert alert--error" role="alert">
                      {advanceVoidError}
                    </div>
                  )}
                  {advanceVoidSuccess && (
                    <div className="alert alert--success" role="status" aria-live="polite">
                      {advanceVoidSuccess}
                    </div>
                  )}
                  <div className="inline-actions">
                    <button
                      className="btn btn-outline-danger"
                      type="button"
                      onClick={onVoidAdvance}
                      disabled={advanceVoidLoading}
                    >
                      {advanceVoidLoading ? 'Đang hủy...' : 'Xác nhận hủy'}
                    </button>
                    <button className="btn btn-ghost" type="button" onClick={onCloseAdvance}>
                      Đóng
                    </button>
                  </div>
                </>
              )}
            </div>
          </div>
        </div>
      )}

      {receiptModal && (
        <div className="modal-backdrop">
          <button
            type="button"
            className="modal-scrim"
            aria-label="Đóng hộp thoại"
            onClick={onCloseReceipt}
          />
          <div
            className="modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="customer-receipt-modal-title"
          >
            <div className="modal-header">
              <div>
                <p className="eyebrow">Chi tiết phiếu thu</p>
                <h3 id="customer-receipt-modal-title">
                  {receiptModal.receiptNo?.trim() ? receiptModal.receiptNo : shortId(receiptModal.id)}
                </h3>
                {receiptModal.receiptDate && (
                  <p className="muted">Ngày thu: {formatDate(receiptModal.receiptDate)}</p>
                )}
              </div>
              <div className="customer-actions">
                <button className="btn btn-ghost btn-table" type="button" onClick={onCloseReceipt}>
                  Đóng
                </button>
              </div>
            </div>
            <div className="modal-body">
              {receiptModal.allocatedAmount !== undefined && (
                <div className="alert alert--info" role="status" aria-live="polite">
                  Số tiền phân bổ: {formatMoney(receiptModal.allocatedAmount)}
                </div>
              )}
              {receiptAllocLoading && <div className="muted">Đang tải phân bổ...</div>}
              {receiptAllocError && (
                <div className="alert alert--error" role="alert">
                  {receiptAllocError}
                </div>
              )}
              {!receiptAllocLoading && receiptAllocations.length === 0 && (
                <div className="muted">Chưa có phân bổ.</div>
              )}
              {receiptAllocations.length > 0 && (
                <div className="table-wrapper">
                  <table className="table">
                    <thead>
                      <tr>
                        <th>Loại</th>
                        <th>Số chứng từ</th>
                        <th>Ngày</th>
                        <th className="align-right">Số tiền</th>
                      </tr>
                    </thead>
                    <tbody>
                      {receiptAllocations.map((item) => (
                        <tr key={item.targetId}>
                          <td>{allocationTypeLabels[item.targetType] ?? item.targetType}</td>
                          <td>{item.targetNo?.trim() ? item.targetNo : shortId(item.targetId)}</td>
                          <td>{formatDate(item.targetDate)}</td>
                          <td className="align-right">{formatMoney(item.amount)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </>
  )
}
