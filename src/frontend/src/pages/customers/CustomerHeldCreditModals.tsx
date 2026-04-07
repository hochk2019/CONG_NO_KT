import { startTransition, useDeferredValue, useEffect, useMemo, useState } from 'react'
import type { CustomerHeldCredit, CustomerInvoice } from '../../api/customers'
import { fetchCustomerInvoices } from '../../api/customers'
import { ApiError } from '../../api/client'
import { applyHeldCredit, releaseHeldCredit } from '../../api/heldCredits'
import ActionConfirmModal from '../../components/modals/ActionConfirmModal'
import { formatDate, formatMoney } from '../../utils/format'
import { invoiceStatusLabels } from './transactions/constants'
import { shortId } from './transactions/utils'

type CustomerHeldCreditModalsProps = {
  token: string
  selectedTaxCode: string | null
  applyTarget: CustomerHeldCredit | null
  releaseTarget: CustomerHeldCredit | null
  onCloseApply: () => void
  onCloseRelease: () => void
  onApplied: (message: string) => void
  onReleased: (message: string) => void
}

const INVOICE_PAGE_SIZE = 20

const resolveInvoiceNo = (row: CustomerInvoice) => row.invoiceNo?.trim() || shortId(row.id)

const filterReplacementInvoices = (
  items: CustomerInvoice[],
  target: CustomerHeldCredit,
) =>
  items.filter((item) => {
    if (item.id === target.originalInvoiceId) return false
    if (item.status.toUpperCase() === 'VOID') return false
    return item.outstandingAmount > 0
  })

export default function CustomerHeldCreditModals({
  token,
  selectedTaxCode,
  applyTarget,
  releaseTarget,
  onCloseApply,
  onCloseRelease,
  onApplied,
  onReleased,
}: CustomerHeldCreditModalsProps) {
  const [invoiceSearch, setInvoiceSearch] = useState('')
  const deferredInvoiceSearch = useDeferredValue(invoiceSearch)
  const [invoicePage, setInvoicePage] = useState(1)
  const [invoiceRows, setInvoiceRows] = useState<CustomerInvoice[]>([])
  const [invoiceLoading, setInvoiceLoading] = useState(false)
  const [invoiceError, setInvoiceError] = useState<string | null>(null)
  const [selectedInvoiceId, setSelectedInvoiceId] = useState('')
  const [useGeneralCreditTopUp, setUseGeneralCreditTopUp] = useState(true)
  const [applyLoading, setApplyLoading] = useState(false)
  const [applyError, setApplyError] = useState<string | null>(null)
  const [releaseLoading, setReleaseLoading] = useState(false)
  const [releaseError, setReleaseError] = useState<string | null>(null)

  useEffect(() => {
    if (!applyTarget) return
    setInvoiceSearch('')
    setInvoicePage(1)
    setInvoiceRows([])
    setInvoiceError(null)
    setSelectedInvoiceId('')
    setUseGeneralCreditTopUp(true)
    setApplyError(null)
  }, [applyTarget])

  useEffect(() => {
    if (!applyTarget || !token || !selectedTaxCode) return

    let isActive = true

    const loadInvoices = async () => {
      setInvoiceLoading(true)
      setInvoiceError(null)
      try {
        const result = await fetchCustomerInvoices({
          token,
          taxCode: selectedTaxCode,
          search: deferredInvoiceSearch.trim() || undefined,
          page: invoicePage,
          pageSize: INVOICE_PAGE_SIZE,
        })
        if (!isActive) return

        const nextRows = filterReplacementInvoices(result.items, applyTarget)
        setInvoiceRows(nextRows)
        setSelectedInvoiceId((current) =>
          nextRows.some((item) => item.id === current) ? current : '',
        )
      } catch (err) {
        if (!isActive) return
        if (err instanceof ApiError) {
          setInvoiceError(err.message)
        } else {
          setInvoiceError('Không tải được danh sách hóa đơn thay thế.')
        }
      } finally {
        if (isActive) {
          setInvoiceLoading(false)
        }
      }
    }

    void loadInvoices()
    return () => {
      isActive = false
    }
  }, [applyTarget, deferredInvoiceSearch, invoicePage, selectedTaxCode, token])

  const selectedInvoice = useMemo(
    () => invoiceRows.find((item) => item.id === selectedInvoiceId) ?? null,
    [invoiceRows, selectedInvoiceId],
  )

  const handleApplySearchChange = (value: string) => {
    startTransition(() => {
      setInvoiceSearch(value)
      setInvoicePage(1)
    })
  }

  const handleApply = async () => {
    if (!token || !applyTarget) {
      setApplyError('Phiên làm việc không hợp lệ.')
      return
    }
    if (!selectedInvoiceId) {
      setApplyError('Vui lòng chọn hóa đơn thay thế.')
      return
    }

    setApplyLoading(true)
    setApplyError(null)
    try {
      const result = await applyHeldCredit(token, applyTarget.id, {
        invoiceId: selectedInvoiceId,
        useGeneralCreditTopUp,
        version: applyTarget.version,
      })

      const summary = [
        `Đã áp ${formatMoney(result.appliedHeldAmount)} từ tiền thừa do hủy HĐ.`,
      ]
      if (result.appliedGeneralCreditAmount > 0) {
        summary.push(
          `Đã dùng thêm ${formatMoney(result.appliedGeneralCreditAmount)} credit chung chưa phân bổ.`,
        )
      }
      if (result.invoiceOutstandingAmount > 0) {
        summary.push(`Hóa đơn thay thế còn thiếu ${formatMoney(result.invoiceOutstandingAmount)}.`)
      }

      onApplied(summary.join(' '))
    } catch (err) {
      if (err instanceof ApiError) {
        setApplyError(err.message)
      } else {
        setApplyError('Không áp được tiền thừa do hủy HĐ.')
      }
    } finally {
      setApplyLoading(false)
    }
  }

  const handleRelease = async () => {
    if (!token || !releaseTarget) {
      setReleaseError('Phiên làm việc không hợp lệ.')
      return
    }

    setReleaseLoading(true)
    setReleaseError(null)
    try {
      const result = await releaseHeldCredit(token, releaseTarget.id, {
        version: releaseTarget.version,
      })
      onReleased(
        `Đã chuyển ${formatMoney(result.releasedAmount)} về credit chung của khách hàng.`,
      )
    } catch (err) {
      if (err instanceof ApiError) {
        setReleaseError(err.message)
      } else {
        setReleaseError('Không chuyển được tiền về credit chung.')
      }
    } finally {
      setReleaseLoading(false)
    }
  }

  return (
    <>
      {applyTarget && (
        <div className="modal-backdrop">
          <button
            type="button"
            className="modal-scrim"
            aria-label="Đóng hộp thoại"
            onClick={onCloseApply}
          />
          <div
            className="modal modal--wide"
            role="dialog"
            aria-modal="true"
            aria-labelledby="held-credit-apply-modal-title"
          >
            <div className="modal-header">
              <div>
                <p className="eyebrow">Áp tiền thừa do hủy HĐ</p>
                <h3 id="held-credit-apply-modal-title">Áp tiền thừa do hủy HĐ</h3>
                <p className="muted">
                  Chọn hóa đơn thay thế để dùng tiền đang treo từ phiếu thu nguồn.
                </p>
              </div>
              <button className="btn btn-ghost btn-table" type="button" onClick={onCloseApply}>
                Đóng
              </button>
            </div>
            <div className="modal-body">
              <div className="summary-grid summary-grid--emphasis">
                <div>
                  <strong>{formatMoney(applyTarget.amountRemaining)}</strong>
                  <span>Còn treo</span>
                </div>
                <div>
                  <strong>{applyTarget.receiptNo?.trim() || shortId(applyTarget.receiptId)}</strong>
                  <span>Phiếu thu nguồn</span>
                </div>
                <div>
                  <strong>
                    {applyTarget.originalInvoiceNo?.trim() || shortId(applyTarget.originalInvoiceId)}
                  </strong>
                  <span>Hóa đơn gốc</span>
                </div>
              </div>

              <div className="alert alert--info" role="status">
                Hệ thống luôn áp tiền thừa do hủy HĐ trước. Nếu hóa đơn mới tăng tiền, có thể bật
                tùy chọn dùng thêm credit chung chưa phân bổ để bù phần chênh lệch theo FIFO ngày
                phiếu thu.
              </div>

              <div className="form-grid">
                <label className="field field--full">
                  <span>Tìm hóa đơn thay thế</span>
                  <input
                    value={invoiceSearch}
                    onChange={(event) => handleApplySearchChange(event.target.value)}
                    placeholder="Nhập số hóa đơn thay thế"
                  />
                </label>
                <label className="field field--checkbox">
                  <input
                    type="checkbox"
                    checked={useGeneralCreditTopUp}
                    onChange={(event) => setUseGeneralCreditTopUp(event.target.checked)}
                  />
                  <span>
                    Dùng thêm credit chung chưa phân bổ để bù phần tăng thêm nếu cần.
                  </span>
                </label>
              </div>

              <div className="table-scroll">
                <table className="table">
                  <thead className="table-head">
                    <tr className="table-row">
                      <th scope="col">Chọn</th>
                      <th scope="col">Hóa đơn</th>
                      <th scope="col">Ngày HĐ</th>
                      <th scope="col">Trạng thái</th>
                      <th scope="col" className="text-right">
                        Còn thiếu
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {invoiceRows.length === 0 ? (
                      <tr className="table-row table-empty">
                        <td colSpan={5}>
                          <div className="empty-state">
                            {invoiceLoading
                              ? 'Đang tải hóa đơn thay thế...'
                              : 'Không có hóa đơn thay thế phù hợp.'}
                          </div>
                        </td>
                      </tr>
                    ) : (
                      invoiceRows.map((row) => (
                        <tr className="table-row" key={row.id}>
                          <td>
                            <input
                              type="radio"
                              name="held-credit-replacement-invoice"
                              aria-label={`Chọn hóa đơn ${resolveInvoiceNo(row)}`}
                              checked={selectedInvoiceId === row.id}
                              onChange={() => setSelectedInvoiceId(row.id)}
                            />
                          </td>
                          <td>{resolveInvoiceNo(row)}</td>
                          <td>{formatDate(row.issueDate)}</td>
                          <td>{invoiceStatusLabels[row.status] ?? row.status}</td>
                          <td className="text-right">{formatMoney(row.outstandingAmount)}</td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>

              {selectedInvoice && (
                <div className="alert alert--info" role="status">
                  Hóa đơn chọn: {resolveInvoiceNo(selectedInvoice)}. Hệ thống sẽ áp tối đa{' '}
                  {formatMoney(applyTarget.amountRemaining)} từ khoản treo này trước, sau đó mới xét
                  phần top-up nếu bạn bật tùy chọn.
                </div>
              )}

              {invoiceError && (
                <div className="alert alert--error" role="alert">
                  {invoiceError}
                </div>
              )}
              {applyError && (
                <div className="alert alert--error" role="alert">
                  {applyError}
                </div>
              )}
            </div>
            <div className="modal-footer modal-footer--end">
              <button
                className="btn btn-primary"
                type="button"
                onClick={() => void handleApply()}
                disabled={applyLoading || !selectedInvoiceId}
              >
                {applyLoading ? 'Đang áp...' : 'Xác nhận áp'}
              </button>
            </div>
          </div>
        </div>
      )}

      <ActionConfirmModal
        isOpen={Boolean(releaseTarget)}
        title="Chuyển về credit chung"
        description="Khoản tiền còn treo sẽ quay về credit chung của khách hàng và có thể được hệ thống tự phân bổ sang hóa đơn mở khác sau đó."
        confirmLabel="Chuyển credit chung"
        cancelLabel="Đóng"
        reasonRequired={false}
        loading={releaseLoading}
        error={releaseError}
        tone="danger"
        onClose={onCloseRelease}
        onConfirm={() => {
          void handleRelease()
        }}
      />
    </>
  )
}
