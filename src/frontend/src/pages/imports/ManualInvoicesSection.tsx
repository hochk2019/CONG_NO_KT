import { useMemo, useState } from 'react'
import { utils, write } from 'xlsx'
import { ApiError } from '../../api/client'
import {
  commitImport,
  uploadImport,
  type ImportCommitResult,
  type ImportStagingResult,
} from '../../api/imports'

type ManualInvoicesSectionProps = {
  token: string
  canCommit: boolean
}

type SubmitMode = 'stage' | 'commit'

const XLSX_MIME_TYPE = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
const INVOICE_HEADERS = [
  'seller_tax_code',
  'customer_tax_code',
  'customer_name',
  'invoice_template_code',
  'invoice_series',
  'invoice_no',
  'issue_date',
  'revenue_excl_vat',
  'vat_amount',
  'total_amount',
  'note',
]

const toMonthStart = (value: string) => {
  if (!value) return ''
  const [year, month] = value.split('-')
  if (!year || !month) return ''
  return `${year}-${month}-01`
}

const toMonthEnd = (value: string) => {
  if (!value) return ''
  const [yearValue, monthValue] = value.split('-')
  const year = Number(yearValue)
  const month = Number(monthValue)
  if (!Number.isFinite(year) || !Number.isFinite(month) || month < 1 || month > 12) return ''
  const lastDate = new Date(Date.UTC(year, month, 0))
  return lastDate.toISOString().slice(0, 10)
}

const formatNumber = (value: number) => {
  return new Intl.NumberFormat('vi-VN', {
    maximumFractionDigits: 2,
  }).format(value)
}

export default function ManualInvoicesSection({ token, canCommit }: ManualInvoicesSectionProps) {
  const [sellerTaxCode, setSellerTaxCode] = useState('')
  const [customerTaxCode, setCustomerTaxCode] = useState('')
  const [customerName, setCustomerName] = useState('')
  const [invoiceTemplateCode, setInvoiceTemplateCode] = useState('01GTKT')
  const [invoiceSeries, setInvoiceSeries] = useState('AA/23E')
  const [invoiceNo, setInvoiceNo] = useState('')
  const [issueDate, setIssueDate] = useState('')
  const [revenueExclVat, setRevenueExclVat] = useState('0')
  const [vatAmount, setVatAmount] = useState('0')
  const [note, setNote] = useState('')
  const [idempotencyKey, setIdempotencyKey] = useState('')
  const [periodFrom, setPeriodFrom] = useState('')
  const [periodTo, setPeriodTo] = useState('')

  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [submitMode, setSubmitMode] = useState<SubmitMode | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [batchId, setBatchId] = useState('')
  const [staging, setStaging] = useState<ImportStagingResult | null>(null)
  const [commitResult, setCommitResult] = useState<ImportCommitResult | null>(null)

  const totalAmount = useMemo(() => {
    const revenue = Number(revenueExclVat)
    const vat = Number(vatAmount)
    if (!Number.isFinite(revenue) || !Number.isFinite(vat)) return 0
    return revenue + vat
  }, [revenueExclVat, vatAmount])

  const setFieldError = (field: string, message?: string) => {
    setFieldErrors((prev) => ({ ...prev, [field]: message ?? '' }))
  }

  const resetMessages = () => {
    setError(null)
    setSuccess(null)
  }

  const resetResults = () => {
    setBatchId('')
    setStaging(null)
    setCommitResult(null)
  }

  const validate = () => {
    const nextErrors: Record<string, string> = {}

    const revenueValue = Number(revenueExclVat)
    const vatValue = Number(vatAmount)

    if (!sellerTaxCode.trim()) nextErrors.sellerTaxCode = 'Vui lòng nhập MST bên bán.'
    if (!customerTaxCode.trim()) nextErrors.customerTaxCode = 'Vui lòng nhập MST bên mua.'
    if (!invoiceNo.trim()) nextErrors.invoiceNo = 'Vui lòng nhập số hóa đơn.'
    if (!issueDate.trim()) nextErrors.issueDate = 'Vui lòng chọn ngày phát hành.'
    if (!Number.isFinite(revenueValue) || revenueValue < 0) {
      nextErrors.revenueExclVat = 'Doanh số chưa thuế không hợp lệ.'
    }
    if (!Number.isFinite(vatValue) || vatValue < 0) {
      nextErrors.vatAmount = 'Tiền VAT không hợp lệ.'
    }

    setFieldErrors(nextErrors)
    if (Object.keys(nextErrors).length > 0) return null

    return {
      sellerTaxCode: sellerTaxCode.trim(),
      customerTaxCode: customerTaxCode.trim(),
      customerName: customerName.trim(),
      invoiceTemplateCode: invoiceTemplateCode.trim(),
      invoiceSeries: invoiceSeries.trim(),
      invoiceNo: invoiceNo.trim(),
      issueDate: issueDate.trim(),
      revenueExclVat: revenueValue,
      vatAmount: vatValue,
      totalAmount: revenueValue + vatValue,
      note: note.trim(),
    }
  }

  const buildWorkbookFile = (payload: ReturnType<typeof validate>) => {
    if (!payload) return null
    const row = {
      seller_tax_code: payload.sellerTaxCode,
      customer_tax_code: payload.customerTaxCode,
      customer_name: payload.customerName,
      invoice_template_code: payload.invoiceTemplateCode,
      invoice_series: payload.invoiceSeries,
      invoice_no: payload.invoiceNo,
      issue_date: payload.issueDate,
      revenue_excl_vat: payload.revenueExclVat,
      vat_amount: payload.vatAmount,
      total_amount: payload.totalAmount,
      note: payload.note,
    }

    const worksheet = utils.json_to_sheet([row], { header: INVOICE_HEADERS })
    const workbook = utils.book_new()
    utils.book_append_sheet(workbook, worksheet, 'Invoices')
    const workbookBytes = write(workbook, { type: 'array', bookType: 'xlsx' })
    const stamp = new Date().toISOString().replaceAll(':', '-')
    return new File([workbookBytes], `manual-invoice-${stamp}.xlsx`, { type: XLSX_MIME_TYPE })
  }

  const handleGenerateKey = () => {
    if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
      setIdempotencyKey(crypto.randomUUID())
    }
  }

  const handleSubmit = async (mode: SubmitMode) => {
    if (!token) {
      setError('Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại.')
      return
    }
    if (mode === 'commit' && !canCommit) {
      setError('Bạn không có quyền ghi dữ liệu.')
      return
    }

    resetMessages()
    resetResults()
    const payload = validate()
    if (!payload) {
      setError('Vui lòng kiểm tra lại thông tin hóa đơn.')
      return
    }

    const file = buildWorkbookFile(payload)
    if (!file) {
      setError('Không thể tạo file nhập liệu từ dữ liệu thủ công.')
      return
    }

    const fallbackFrom = toMonthStart(payload.issueDate)
    const fallbackTo = toMonthEnd(payload.issueDate)
    setSubmitMode(mode)
    try {
      const uploadResult = await uploadImport({
        token,
        type: 'INVOICE',
        file,
        periodFrom: periodFrom || fallbackFrom || undefined,
        periodTo: periodTo || fallbackTo || undefined,
        idempotencyKey: idempotencyKey || undefined,
      })
      setBatchId(uploadResult.batch.batchId)
      setStaging(uploadResult.staging)

      if (mode === 'commit') {
        const commit = await commitImport({
          token,
          batchId: uploadResult.batch.batchId,
          idempotencyKey: idempotencyKey || undefined,
        })
        setCommitResult(commit)
        setSuccess(
          `Đã ghi ${commit.insertedInvoices} hóa đơn vào hệ thống (batch ${uploadResult.batch.batchId}).`,
        )
      } else {
        setSuccess(
          `Đã tạo lô nháp ${uploadResult.batch.batchId}. Bạn có thể kiểm tra lại trước khi ghi dữ liệu.`,
        )
      }
    } catch (submitError) {
      if (submitError instanceof ApiError) {
        setError(submitError.message)
      } else {
        setError('Không thể nhập hóa đơn thủ công.')
      }
    } finally {
      setSubmitMode(null)
    }
  }

  return (
    <div className="page-stack">
      <section className="card">
        <div className="card-row">
          <div>
            <h3>Nhập thủ công 1 hóa đơn</h3>
            <p className="muted">
              Hệ thống sẽ tự tạo file import `.xlsx` từ biểu mẫu bên dưới, sau đó đưa vào pipeline
              nhập liệu hóa đơn.
            </p>
          </div>
        </div>

        <div className="form-grid">
          <label className={fieldErrors.sellerTaxCode ? 'field field--error' : 'field'}>
            <span>MST bên bán</span>
            <input
              value={sellerTaxCode}
              onChange={(event) => {
                setSellerTaxCode(event.target.value)
                if (event.target.value.trim()) setFieldError('sellerTaxCode')
              }}
              onBlur={() => {
                if (!sellerTaxCode.trim()) setFieldError('sellerTaxCode', 'Vui lòng nhập MST bên bán.')
              }}
              placeholder="VD: 0312345678"
              aria-invalid={Boolean(fieldErrors.sellerTaxCode)}
            />
            {fieldErrors.sellerTaxCode && <span className="field-error">{fieldErrors.sellerTaxCode}</span>}
          </label>

          <label className={fieldErrors.customerTaxCode ? 'field field--error' : 'field'}>
            <span>MST bên mua</span>
            <input
              value={customerTaxCode}
              onChange={(event) => {
                setCustomerTaxCode(event.target.value)
                if (event.target.value.trim()) setFieldError('customerTaxCode')
              }}
              onBlur={() => {
                if (!customerTaxCode.trim()) {
                  setFieldError('customerTaxCode', 'Vui lòng nhập MST bên mua.')
                }
              }}
              placeholder="VD: 0101234567"
              aria-invalid={Boolean(fieldErrors.customerTaxCode)}
            />
            {fieldErrors.customerTaxCode && (
              <span className="field-error">{fieldErrors.customerTaxCode}</span>
            )}
          </label>

          <label className="field">
            <span>Tên bên mua</span>
            <input
              value={customerName}
              onChange={(event) => setCustomerName(event.target.value)}
              placeholder="Tên công ty khách hàng"
            />
          </label>

          <label className="field">
            <span>Ký hiệu mẫu</span>
            <input
              value={invoiceTemplateCode}
              onChange={(event) => setInvoiceTemplateCode(event.target.value)}
            />
          </label>

          <label className="field">
            <span>Số hiệu hóa đơn</span>
            <input value={invoiceSeries} onChange={(event) => setInvoiceSeries(event.target.value)} />
          </label>

          <label className={fieldErrors.invoiceNo ? 'field field--error' : 'field'}>
            <span>Số hóa đơn</span>
            <input
              value={invoiceNo}
              onChange={(event) => {
                setInvoiceNo(event.target.value)
                if (event.target.value.trim()) setFieldError('invoiceNo')
              }}
              onBlur={() => {
                if (!invoiceNo.trim()) setFieldError('invoiceNo', 'Vui lòng nhập số hóa đơn.')
              }}
              placeholder="VD: INV-0001"
              aria-invalid={Boolean(fieldErrors.invoiceNo)}
            />
            {fieldErrors.invoiceNo && <span className="field-error">{fieldErrors.invoiceNo}</span>}
          </label>

          <label className={fieldErrors.issueDate ? 'field field--error' : 'field'}>
            <span>Ngày phát hành</span>
            <input
              type="date"
              value={issueDate}
              onChange={(event) => {
                setIssueDate(event.target.value)
                if (event.target.value.trim()) setFieldError('issueDate')
              }}
              onBlur={() => {
                if (!issueDate.trim()) setFieldError('issueDate', 'Vui lòng chọn ngày phát hành.')
              }}
              aria-invalid={Boolean(fieldErrors.issueDate)}
            />
            {fieldErrors.issueDate && <span className="field-error">{fieldErrors.issueDate}</span>}
          </label>

          <label className={fieldErrors.revenueExclVat ? 'field field--error' : 'field'}>
            <span>Doanh số chưa thuế</span>
            <input
              type="number"
              min="0"
              inputMode="decimal"
              value={revenueExclVat}
              onChange={(event) => {
                setRevenueExclVat(event.target.value)
                const parsed = Number(event.target.value)
                if (Number.isFinite(parsed) && parsed >= 0) setFieldError('revenueExclVat')
              }}
              onBlur={() => {
                const parsed = Number(revenueExclVat)
                if (!Number.isFinite(parsed) || parsed < 0) {
                  setFieldError('revenueExclVat', 'Doanh số chưa thuế không hợp lệ.')
                }
              }}
              aria-invalid={Boolean(fieldErrors.revenueExclVat)}
            />
            {fieldErrors.revenueExclVat && (
              <span className="field-error">{fieldErrors.revenueExclVat}</span>
            )}
          </label>

          <label className={fieldErrors.vatAmount ? 'field field--error' : 'field'}>
            <span>Tiền VAT</span>
            <input
              type="number"
              min="0"
              inputMode="decimal"
              value={vatAmount}
              onChange={(event) => {
                setVatAmount(event.target.value)
                const parsed = Number(event.target.value)
                if (Number.isFinite(parsed) && parsed >= 0) setFieldError('vatAmount')
              }}
              onBlur={() => {
                const parsed = Number(vatAmount)
                if (!Number.isFinite(parsed) || parsed < 0) {
                  setFieldError('vatAmount', 'Tiền VAT không hợp lệ.')
                }
              }}
              aria-invalid={Boolean(fieldErrors.vatAmount)}
            />
            {fieldErrors.vatAmount && <span className="field-error">{fieldErrors.vatAmount}</span>}
          </label>

          <label className="field">
            <span>Kỳ từ (tùy chọn)</span>
            <input type="date" value={periodFrom} onChange={(event) => setPeriodFrom(event.target.value)} />
          </label>

          <label className="field">
            <span>Kỳ đến (tùy chọn)</span>
            <input type="date" value={periodTo} onChange={(event) => setPeriodTo(event.target.value)} />
          </label>

          <label className="field field-span-full">
            <span>Ghi chú</span>
            <input
              value={note}
              onChange={(event) => setNote(event.target.value)}
              placeholder="Không bắt buộc"
            />
          </label>
        </div>

        <details className="advanced-panel">
          <summary>Tùy chọn nâng cao</summary>
          <div className="advanced-panel__content">
            <label className="field field-span-full">
              <span>Idempotency key</span>
              <div className="input-row">
                <input
                  value={idempotencyKey}
                  onChange={(event) => setIdempotencyKey(event.target.value)}
                  placeholder="uuid"
                />
                <button className="btn btn-ghost" type="button" onClick={handleGenerateKey}>
                  Tạo khóa
                </button>
              </div>
            </label>
          </div>
        </details>

        <div className="inline-actions">
          <button
            className="btn btn-primary"
            type="button"
            onClick={() => handleSubmit('stage')}
            disabled={submitMode !== null}
          >
            {submitMode === 'stage' ? 'Đang tạo lô...' : 'Tạo lô nháp'}
          </button>
          <button
            className="btn btn-outline"
            type="button"
            onClick={() => handleSubmit('commit')}
            disabled={!canCommit || submitMode !== null}
          >
            {submitMode === 'commit' ? 'Đang ghi...' : 'Nhập và ghi dữ liệu'}
          </button>
          <span className="muted">Tổng tiền: {formatNumber(totalAmount)}</span>
        </div>

        {error && (
          <div className="alert alert--error" role="alert" aria-live="assertive">
            {error}
          </div>
        )}
        {success && (
          <div className="alert alert--success" role="alert" aria-live="assertive">
            {success}
          </div>
        )}
      </section>

      {(batchId || staging || commitResult) && (
        <section className="card">
          <h3>Kết quả nhập thủ công</h3>
          {batchId && (
            <p className="muted">
              Batch: <strong>{batchId}</strong>
            </p>
          )}
          {staging && (
            <p className="muted">
              Staging: {staging.totalRows} dòng · hợp lệ {staging.okCount} · cảnh báo{' '}
              {staging.warnCount} · lỗi {staging.errorCount}
            </p>
          )}
          {commitResult && (
            <p className="muted">
              Đã ghi: {commitResult.insertedInvoices} hóa đơn, {commitResult.insertedAdvances}{' '}
              khoản trả hộ, {commitResult.insertedReceipts} phiếu thu.
            </p>
          )}
        </section>
      )}
    </div>
  )
}
