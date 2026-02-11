import { useEffect, useMemo, useRef, useState } from 'react'
import { ApiError } from '../../api/client'
import { cancelImport, commitImport, fetchPreview, rollbackImport, uploadImport } from '../../api/imports'
import ImportHistorySection from './ImportHistorySection'
import ImportPreviewModal from './ImportPreviewModal'

const DEFAULT_PAGE_SIZE = 10
const PAGE_SIZE_STORAGE_KEY = 'pref.table.pageSize'
const IMPORTS_PREVIEW_STATUS_KEY = 'pref.imports.previewStatus'

const getStoredPageSize = () => {
  if (typeof window === 'undefined') return DEFAULT_PAGE_SIZE
  const raw = window.localStorage.getItem(PAGE_SIZE_STORAGE_KEY)
  const parsed = Number(raw)
  return Number.isFinite(parsed) && parsed > 0 ? parsed : DEFAULT_PAGE_SIZE
}

const storePageSize = (value: number) => {
  if (typeof window === 'undefined') return
  window.localStorage.setItem(PAGE_SIZE_STORAGE_KEY, String(value))
}

const getStoredFilter = (key: string) => {
  if (typeof window === 'undefined') return ''
  return window.localStorage.getItem(key) ?? ''
}

const storeFilter = (key: string, value: string) => {
  if (typeof window === 'undefined') return
  if (!value) {
    window.localStorage.removeItem(key)
  } else {
    window.localStorage.setItem(key, value)
  }
}

const importTypes = ['INVOICE', 'ADVANCE', 'RECEIPT']
const importTypeLabels: Record<string, string> = {
  INVOICE: 'Hóa đơn',
  ADVANCE: 'Khoản trả hộ KH',
  RECEIPT: 'Phiếu thu',
}
const previewStatusLabels: Record<string, string> = {
  OK: 'Hợp lệ',
  WARN: 'Cảnh báo',
  ERROR: 'Lỗi',
}
const historyStatusLabels: Record<string, string> = {
  STAGING: 'Đang chờ',
  COMMITTED: 'Đã ghi',
  ROLLED_BACK: 'Đã hoàn tác',
  CANCELLED: 'Đã hủy',
}
const actionSuggestionLabels: Record<string, string> = {
  INSERT: 'Ghi',
  SKIP: 'Bỏ qua',
}
const validationMessageLabels: Record<string, string> = {
  DUP_IN_DB: 'Trùng hóa đơn đã có trong hệ thống',
  DUP_IN_FILE: 'Trùng trong file',
  BUYER_TAX_REQUIRED: 'Thiếu MST người mua',
  BUYER_NAME_REQUIRED: 'Thiếu tên người mua',
  INVOICE_NO_REQUIRED: 'Thiếu số hóa đơn',
  ISSUE_DATE_REQUIRED: 'Thiếu ngày phát hành',
  SELLER_TAX_REQUIRED: 'Thiếu MST người bán',
  CUSTOMER_TAX_REQUIRED: 'Thiếu MST khách hàng',
  ADVANCE_DATE_REQUIRED: 'Thiếu ngày trả hộ',
  RECEIPT_DATE_REQUIRED: 'Thiếu ngày thu',
  APPLIED_PERIOD_REQUIRED: 'Thiếu kỳ áp dụng',
  APPLIED_PERIOD_NOT_FIRST_DAY: 'Kỳ áp dụng phải là ngày đầu tháng',
  METHOD_INVALID: 'Hình thức thu không hợp lệ',
  AMOUNT_REQUIRED: 'Thiếu số tiền',
  NEGATIVE_AMOUNT: 'Số tiền âm không hợp lệ',
}
const formatValidationMessages = (messages: string[]) =>
  messages.map((message) => validationMessageLabels[message] ?? message).join(', ')

const previewPageSizes = [10, 20, 50, 100, 200]

type ImportBatchSectionProps = {
  token: string
  canStage: boolean
  canCommit: boolean
}

export default function ImportBatchSection({ token, canStage, canCommit }: ImportBatchSectionProps) {
  const fileInputRef = useRef<HTMLInputElement | null>(null)

  const [type, setType] = useState('INVOICE')
  const [file, setFile] = useState<File | null>(null)
  const [periodFrom, setPeriodFrom] = useState('')
  const [periodTo, setPeriodTo] = useState('')
  const [idempotencyKey, setIdempotencyKey] = useState('')
  const [batchId, setBatchId] = useState('')
  const [staging, setStaging] = useState<{
    totalRows: number
    okCount: number
    warnCount: number
    errorCount: number
  } | null>(null)
  const [previewStatus, setPreviewStatus] = useState(() => getStoredFilter(IMPORTS_PREVIEW_STATUS_KEY))
  const [preview, setPreview] = useState<{
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
  } | null>(null)
  const [previewPage, setPreviewPage] = useState(1)
  const [previewPageSize, setPreviewPageSize] = useState(() => getStoredPageSize())
  const [previewLoaded, setPreviewLoaded] = useState(false)
  const [previewLoading, setPreviewLoading] = useState(false)
  const [commitResult, setCommitResult] = useState<{
    insertedInvoices: number
    insertedAdvances: number
    insertedReceipts: number
  } | null>(null)
  const [historyReload, setHistoryReload] = useState(0)
  const [overrideLock, setOverrideLock] = useState(false)
  const [overrideReason, setOverrideReason] = useState('')
  const [uploadError, setUploadError] = useState<string | null>(null)
  const [previewError, setPreviewError] = useState<string | null>(null)
  const [commitError, setCommitError] = useState<string | null>(null)
  const [uploading, setUploading] = useState(false)
  const [uploadProgress, setUploadProgress] = useState(0)
  const [commitLoading, setCommitLoading] = useState(false)
  const [rollbackLoading, setRollbackLoading] = useState(false)
  const [cancelLoading, setCancelLoading] = useState(false)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [previewOpen, setPreviewOpen] = useState(false)

  const summaryLabel = useMemo(() => {
    if (!staging) return 'Chưa có lô nào.'
    return `Tổng ${staging.totalRows} dòng · Hợp lệ ${staging.okCount} · Cảnh báo ${staging.warnCount} · Lỗi ${staging.errorCount}`
  }, [staging])

  const previewTotalPages = preview
    ? Math.max(1, Math.ceil(preview.totalRows / preview.pageSize))
    : 1


  useEffect(() => {
    if (!batchId || !token || !previewLoaded) return
    let isActive = true

    const loadPreview = async () => {
      setPreviewLoading(true)
      setPreviewError(null)
      try {
        const result = await fetchPreview({
          token,
          batchId,
          status: previewStatus || undefined,
          page: previewPage,
          pageSize: previewPageSize,
        })
        if (!isActive) return
        setPreview(result)
      } catch (err) {
        if (!isActive) return
        if (err instanceof ApiError) {
          setPreviewError(err.message)
        } else {
          setPreviewError('Không tải được xem trước.')
        }
      } finally {
        if (isActive) {
          setPreviewLoading(false)
        }
      }
    }

    loadPreview()
    return () => {
      isActive = false
    }
  }, [batchId, previewStatus, previewPage, previewPageSize, previewLoaded, token])

  const handleGenerateKey = () => {
    if ('randomUUID' in crypto) {
      setIdempotencyKey(crypto.randomUUID())
    }
  }

  const setFieldError = (field: string, message?: string) => {
    setFieldErrors((prev) => ({
      ...prev,
      [field]: message ?? '',
    }))
  }

  const handleUpload = async () => {
    if (!file || !token) {
      setUploadError('Vui lòng chọn file và đăng nhập.')
      if (!file) {
        setFieldError('file', 'Vui lòng chọn file.')
      }
      return
    }
    setFieldErrors({})
    setUploadError(null)
    setPreviewError(null)
    setCommitError(null)
    setCommitResult(null)
    setUploading(true)
    setUploadProgress(0)
    try {
      const result = await uploadImport({
        token,
        type,
        file,
        periodFrom: periodFrom || undefined,
        periodTo: periodTo || undefined,
        idempotencyKey: idempotencyKey || undefined,
        onProgress: setUploadProgress,
      })
      setBatchId(result.batch.batchId)
      setStaging(result.staging)
      setPreview(null)
      setPreviewLoaded(false)
      setPreviewPage(1)
    } catch (err) {
      if (err instanceof ApiError) {
        setUploadError(err.message)
      } else {
        setUploadError('Tải file thất bại.')
      }
    } finally {
      setUploading(false)
    }
  }

  const handlePreview = () => {
    if (!batchId || !token) {
      setPreviewError('Ấn vào tải file trước.')
      return
    }
    setPreviewError(null)
    setPreviewOpen(true)
    if (!previewLoaded) {
      setPreviewLoaded(true)
      setPreviewPage(1)
    }
  }

  const handleCancelUpload = async () => {
    if (batchId && token) {
      setCancelLoading(true)
      setUploadError(null)
      try {
        await cancelImport({
          token,
          batchId,
          reason: 'Hủy bỏ tại bước tải file',
        })
        setHistoryReload((value) => value + 1)
      } catch (err) {
        if (err instanceof ApiError) {
          setUploadError(err.message)
        } else {
          setUploadError('Hủy lô thất bại.')
        }
        setCancelLoading(false)
        return
      } finally {
        setCancelLoading(false)
      }
    }
    setFile(null)
    if (fileInputRef.current) {
      fileInputRef.current.value = ''
    }
    setBatchId('')
    setStaging(null)
    setPreview(null)
    setPreviewLoaded(false)
    setPreviewPage(1)
    setPreviewOpen(false)
    setUploadError(null)
    setPreviewError(null)
    setCommitError(null)
    setCommitResult(null)
    setUploadProgress(0)
    setOverrideLock(false)
    setOverrideReason('')
    setFieldErrors({})
  }

  const handleCommit = async () => {
    if (!batchId || !token) return
    if (overrideLock && !overrideReason.trim()) {
      setFieldError('overrideReason', 'Vui lòng nhập lý do vượt khóa kỳ.')
      setCommitError('Vui lòng nhập lý do vượt khóa kỳ.')
      return
    }
    setFieldErrors({})
    setCommitLoading(true)
    setCommitError(null)
    try {
      const result = await commitImport({
        token,
        batchId,
        idempotencyKey: idempotencyKey || undefined,
        overridePeriodLock: overrideLock,
        overrideReason: overrideReason || undefined,
      })
      setCommitResult(result)
      setHistoryReload((value) => value + 1)
    } catch (err) {
      if (err instanceof ApiError) {
        setCommitError(err.message)
      } else {
        setCommitError('Ghi dữ liệu thất bại.')
      }
    } finally {
      setCommitLoading(false)
    }
  }

  const handleRollback = async () => {
    if (!batchId || !token) return
    if (overrideLock && !overrideReason.trim()) {
      setFieldError('overrideReason', 'Vui lòng nhập lý do vượt khóa kỳ.')
      setCommitError('Vui lòng nhập lý do vượt khóa kỳ.')
      return
    }
    setFieldErrors({})
    setRollbackLoading(true)
    setCommitError(null)
    try {
      await rollbackImport({
        token,
        batchId,
        overridePeriodLock: overrideLock,
        overrideReason: overrideReason || undefined,
      })
      setCommitResult(null)
      setHistoryReload((value) => value + 1)
    } catch (err) {
      if (err instanceof ApiError) {
        setCommitError(err.message)
      } else {
        setCommitError('Hoàn tác thất bại.')
      }
    } finally {
      setRollbackLoading(false)
    }
  }

  const handleResumeBatch = (row: {
    batchId: string
    type: string
    periodFrom?: string | null
    periodTo?: string | null
  }) => {
    setBatchId(row.batchId)
    setType(row.type)
    setPeriodFrom(row.periodFrom ?? '')
    setPeriodTo(row.periodTo ?? '')
    setPreviewStatus('')
    setPreviewPage(1)
    setPreviewLoaded(true)
    setPreviewOpen(true)
    setCommitResult(null)
    setUploadError(null)
    setCommitError(null)
    setPreviewError(null)
    setStaging(null)
    document.getElementById('upload')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }

  return (
    <div className="page-stack">
      <div className="page-header">
        <div>
          <h2>Nhập file, kiểm tra trước khi ghi dữ liệu</h2>
          <p className="muted">Quy trình: chuẩn bị template → tải file → xem trước → ghi dữ liệu.</p>
        </div>
        <div className="header-actions">
          <a className="btn btn-ghost" href="#templates">
            Tải template
          </a>
          <a className="btn btn-outline" href="#history">
            Lịch sử nhập
          </a>
        </div>
      </div>

      <section className="card" id="templates">
        <p className="eyebrow">Bước 1</p>
        <h3>Chuẩn bị template</h3>
        <p className="muted">
          Không đổi tên cột trong template. Ngày hỗ trợ yyyy-MM-dd, dd/MM/yyyy, dd-MM-yyyy.
        </p>
        <div className="inline-actions">
          <span className="muted">Tải template:</span>
          <a className="btn btn-ghost" href="/templates/invoice_template.xlsx" download>
            Hóa đơn
          </a>
          <a className="btn btn-ghost" href="/templates/advance_template.xlsx" download>
            Khoản trả hộ KH
          </a>
          <a className="btn btn-ghost" href="/templates/receipt_template.xlsx" download>
            Phiếu thu
          </a>
        </div>
        <p className="muted">
          Hóa đơn hỗ trợ cả template đơn giản và ReportDetail.xlsx (sheet ExportData).
        </p>
      </section>

      <section className="card" id="upload">
        <p className="eyebrow">Bước 2</p>
        <h3>Tải file nhập liệu</h3>
        <div className="form-grid form-grid--upload">
          <label className="field">
            <span>Loại dữ liệu</span>
            <select value={type} onChange={(event) => setType(event.target.value)}>
              {importTypes.map((item) => (
                <option key={item} value={item}>
                  {importTypeLabels[item] ?? item}
                </option>
              ))}
            </select>
          </label>
          <label className={fieldErrors.file ? 'field field--error' : 'field'}>
            <span>Chọn file</span>
            <input
              ref={fileInputRef}
              type="file"
              accept=".xlsx"
              onChange={(event) => {
                setFile(event.target.files?.[0] ?? null)
                if (event.target.files?.[0]) {
                  setFieldError('file')
                }
              }}
              aria-invalid={Boolean(fieldErrors.file)}
            />
            {fieldErrors.file && <span className="field-error">{fieldErrors.file}</span>}
          </label>
          <label className="field">
            <span>Kỳ từ</span>
            <input
              type="date"
              value={periodFrom}
              onChange={(event) => setPeriodFrom(event.target.value)}
            />
          </label>
          <label className="field">
            <span>Kỳ đến</span>
            <input
              type="date"
              value={periodTo}
              onChange={(event) => setPeriodTo(event.target.value)}
            />
          </label>
        </div>
        <details className="advanced-panel">
          <summary>Tùy chọn nâng cao</summary>
          <div className="advanced-panel__content">
            <div className="form-grid">
              <label className="field field-span-full">
                <span>Khóa chống trùng</span>
                <div className="input-row">
                  <input
                    value={idempotencyKey}
                    onChange={(event) => setIdempotencyKey(event.target.value)}
                    placeholder="uuid"
                  />
                  <button className="btn btn-ghost" onClick={handleGenerateKey} type="button">
                    Tạo khóa
                  </button>
                </div>
                <span className="muted">
                  Dùng khi cần tải lại cùng file để tránh tạo lô trùng (chống trùng).
                </span>
              </label>
            </div>
          </div>
        </details>
        <div className="inline-actions">
          <button className="btn btn-primary" onClick={handleUpload} disabled={uploading}>
            {uploading ? 'Đang tải...' : 'Tải file'}
          </button>
          <button
            className="btn btn-outline"
            type="button"
            onClick={handlePreview}
            disabled={!batchId || uploading || cancelLoading}
            title={!batchId ? 'Ấn vào tải file trước.' : 'Xem trước dữ liệu'}
          >
            Xem trước
          </button>
          <button
            className="btn btn-outline"
            type="button"
            onClick={handleCancelUpload}
            disabled={uploading || cancelLoading || (!file && !batchId && !preview && !staging)}
          >
            {cancelLoading ? 'Đang hủy...' : 'Hủy bỏ'}
          </button>
          <span className="muted">
            {uploading ? `Đang tải ${uploadProgress}%` : summaryLabel}
          </span>
        </div>
        {uploading && (
          <div className="progress">
            <div className="progress__bar" style={{ width: `${uploadProgress}%` }} />
          </div>
        )}
        {batchId && (
          <div className="meta-row">
            <span>Mã lô: {batchId}</span>
            <span className="pill pill-warn">{historyStatusLabels.STAGING}</span>
          </div>
        )}
        {uploadError && <div className="alert alert--error" role="alert" aria-live="assertive">{uploadError}</div>}
      </section>

      <section className="card" id="commit">
        <p className="eyebrow">Bước 3</p>
        <h3>Ghi dữ liệu / Hoàn tác</h3>
        <p className="muted">
          Hoàn tác sẽ gỡ các chứng từ đã ghi từ lô hiện tại. Không áp dụng cho lô đang chờ.
        </p>
        <details className="advanced-panel">
          <summary>Tùy chọn nâng cao</summary>
          <div className="advanced-panel__content">
            <div className="form-grid">
              <label className="field field-inline">
                <input
                  type="checkbox"
                  checked={overrideLock}
                  onChange={(event) => {
                    setOverrideLock(event.target.checked)
                    if (!event.target.checked) {
                      setFieldError('overrideReason')
                    }
                  }}
                />
                <span>Vượt khóa kỳ</span>
              </label>
              <label className={fieldErrors.overrideReason ? 'field field--error' : 'field'}>
                <span>Lý do vượt khóa kỳ</span>
                <input
                  value={overrideReason}
                  onChange={(event) => {
                    setOverrideReason(event.target.value)
                    if (event.target.value.trim()) {
                      setFieldError('overrideReason')
                    }
                  }}
                  onBlur={() => {
                    if (overrideLock && !overrideReason.trim()) {
                      setFieldError('overrideReason', 'Vui lòng nhập lý do vượt khóa kỳ.')
                    }
                  }}
                  placeholder="Bắt buộc khi vượt khóa kỳ"
                  aria-invalid={Boolean(fieldErrors.overrideReason)}
                />
                {fieldErrors.overrideReason && (
                  <span className="field-error">{fieldErrors.overrideReason}</span>
                )}
              </label>
            </div>
          </div>
        </details>
        <div className="inline-actions">
          <button
            className="btn btn-primary"
            onClick={handleCommit}
            disabled={commitLoading || !canCommit}
          >
            {commitLoading ? 'Đang ghi...' : 'Ghi dữ liệu'}
          </button>
          <button
            className="btn btn-outline"
            onClick={handleRollback}
            disabled={rollbackLoading || !canCommit}
          >
            {rollbackLoading ? 'Đang hoàn tác...' : 'Hoàn tác'}
          </button>
        </div>
        {commitResult && (
          <div className="alert alert--success" role="alert" aria-live="assertive">
            Ghi thành công: {commitResult.insertedInvoices} hóa đơn,{' '}
            {commitResult.insertedAdvances} khoản trả hộ KH, {commitResult.insertedReceipts} phiếu
            thu.
          </div>
        )}
        {commitError && <div className="alert alert--error" role="alert" aria-live="assertive">{commitError}</div>}
      </section>

      <ImportPreviewModal
        isOpen={previewOpen}
        onClose={() => setPreviewOpen(false)}
        batchId={batchId}
        previewStatus={previewStatus}
        onPreviewStatusChange={(value) => {
          setPreviewStatus(value)
          setPreviewPage(1)
          storeFilter(IMPORTS_PREVIEW_STATUS_KEY, value)
        }}
        previewPageSize={previewPageSize}
        onPreviewPageSizeChange={(value) => {
          storePageSize(value)
          setPreviewPageSize(value)
          setPreviewPage(1)
        }}
        previewPageSizes={previewPageSizes}
        previewLoading={previewLoading}
        previewError={previewError}
        preview={preview}
        previewTotalPages={previewTotalPages}
        onPrevPage={() => setPreviewPage(Math.max(1, previewPage - 1))}
        onNextPage={() => setPreviewPage(Math.min(previewTotalPages, previewPage + 1))}
        formatValidationMessages={formatValidationMessages}
        previewStatusLabels={previewStatusLabels}
        actionSuggestionLabels={actionSuggestionLabels}
      />

      <ImportHistorySection
        token={token}
        canStage={canStage}
        canCommit={canCommit}
        importTypeLabels={importTypeLabels}
        historyStatusLabels={historyStatusLabels}
        refreshKey={historyReload}
        onResumeBatch={handleResumeBatch}
      />
    </div>
  )
}
