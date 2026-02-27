import { useCallback, useEffect, useMemo, useState } from 'react'
import { ApiError } from '../api/client'
import {
  fetchErpIntegrationConfig,
  fetchErpIntegrationStatus,
  syncErpSummary,
  updateErpIntegrationConfig,
  type ErpIntegrationConfig,
  type ErpIntegrationStatus,
  type ErpSyncSummaryResult,
} from '../api/erpIntegration'
import { useAuth } from '../context/AuthStore'
import { formatDateTime, formatMoney } from '../utils/format'

type SyncForm = {
  from: string
  to: string
  asOfDate: string
  dueSoonDays: number
  dryRun: boolean
}

type ConfigForm = {
  enabled: boolean
  provider: string
  baseUrl: string
  companyCode: string
  timeoutSeconds: number
  apiKey: string
  clearApiKey: boolean
}

type ConfigValidation = {
  form?: string
  provider?: string
  baseUrl?: string
  companyCode?: string
  timeoutSeconds?: string
  apiKey?: string
}

const toDateInput = (value: Date) => {
  const year = value.getFullYear()
  const month = String(value.getMonth() + 1).padStart(2, '0')
  const day = String(value.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

const normalizeText = (value: string) => {
  const trimmed = value.trim()
  return trimmed.length > 0 ? trimmed : undefined
}

const normalizeProvider = (value: string) => {
  const normalized = normalizeText(value)
  return normalized ?? 'MISA'
}

const formatCount = (value: number) => {
  return value.toLocaleString('vi-VN')
}

const isAbsoluteUrl = (value: string) => {
  try {
    const parsed = new URL(value)
    return parsed.protocol === 'http:' || parsed.protocol === 'https:'
  } catch {
    return false
  }
}

const validateConfigForm = (
  form: ConfigForm,
  currentConfig: ErpIntegrationConfig | null,
): ConfigValidation => {
  const validation: ConfigValidation = {}
  const provider = normalizeProvider(form.provider)
  const baseUrl = normalizeText(form.baseUrl)
  const companyCode = normalizeText(form.companyCode)
  const apiKey = normalizeText(form.apiKey)
  const hasExistingApiKey = currentConfig?.hasApiKey === true
  const willHaveApiKey = form.clearApiKey ? false : Boolean(apiKey) || hasExistingApiKey

  if (provider.length > 32) {
    validation.provider = 'Provider tối đa 32 ký tự.'
  }

  if (!baseUrl && form.enabled) {
    validation.baseUrl = 'Base URL là bắt buộc khi bật tích hợp.'
  } else if (baseUrl && !isAbsoluteUrl(baseUrl)) {
    validation.baseUrl = 'Base URL phải là URL tuyệt đối hợp lệ.'
  }

  if (!companyCode && form.enabled) {
    validation.companyCode = 'Mã công ty là bắt buộc khi bật tích hợp.'
  }

  if (form.timeoutSeconds < 5 || form.timeoutSeconds > 120) {
    validation.timeoutSeconds = 'Timeout phải nằm trong khoảng 5-120 giây.'
  }

  if (form.enabled && !willHaveApiKey) {
    validation.apiKey = 'Cần khai báo API key khi bật tích hợp.'
  }

  if (Object.keys(validation).length > 0) {
    validation.form = 'Vui lòng kiểm tra lại thông tin cấu hình trước khi lưu.'
  }

  return validation
}

const resolveStatusPillClass = (status?: string | null) => {
  switch (status) {
    case 'success':
      return 'pill-ok'
    case 'dry_run':
      return 'pill-info'
    case 'disabled':
    case 'not_configured':
      return 'pill-warn'
    case 'failed':
    case 'timeout':
    case 'error':
      return 'pill-error'
    default:
      return 'pill-info'
  }
}

export default function AdminErpIntegrationPage() {
  const { state } = useAuth()
  const token = state.accessToken ?? ''

  const [status, setStatus] = useState<ErpIntegrationStatus | null>(null)
  const [config, setConfig] = useState<ErpIntegrationConfig | null>(null)
  const [syncResult, setSyncResult] = useState<ErpSyncSummaryResult | null>(null)
  const [loadingStatus, setLoadingStatus] = useState(false)
  const [loadingConfig, setLoadingConfig] = useState(false)
  const [savingConfig, setSavingConfig] = useState(false)
  const [syncLoading, setSyncLoading] = useState(false)
  const [statusError, setStatusError] = useState<string | null>(null)
  const [configError, setConfigError] = useState<string | null>(null)
  const [syncError, setSyncError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [configValidation, setConfigValidation] = useState<ConfigValidation>({})

  const [form, setForm] = useState<SyncForm>({
    from: '',
    to: '',
    asOfDate: toDateInput(new Date()),
    dueSoonDays: 7,
    dryRun: false,
  })
  const [configForm, setConfigForm] = useState<ConfigForm>({
    enabled: false,
    provider: 'MISA',
    baseUrl: '',
    companyCode: '',
    timeoutSeconds: 15,
    apiKey: '',
    clearApiKey: false,
  })

  const configDirty = useMemo(() => {
    if (!config) {
      return false
    }

    const originalProvider = config.provider || 'MISA'
    const currentProvider = normalizeProvider(configForm.provider)
    const originalBaseUrl = config.baseUrl ?? null
    const currentBaseUrl = normalizeText(configForm.baseUrl) ?? null
    const originalCompanyCode = config.companyCode ?? null
    const currentCompanyCode = normalizeText(configForm.companyCode) ?? null
    const hasNewApiKey = Boolean(normalizeText(configForm.apiKey))

    return (
      config.enabled !== configForm.enabled
      || originalProvider !== currentProvider
      || originalBaseUrl !== currentBaseUrl
      || originalCompanyCode !== currentCompanyCode
      || config.timeoutSeconds !== configForm.timeoutSeconds
      || hasNewApiKey
      || configForm.clearApiKey
    )
  }, [config, configForm])

  const loadStatus = useCallback(async () => {
    if (!token) return
    setLoadingStatus(true)
    setStatusError(null)
    try {
      const data = await fetchErpIntegrationStatus(token)
      setStatus(data)
    } catch (err) {
      if (err instanceof ApiError) {
        setStatusError(err.message)
      } else {
        setStatusError('Không tải được trạng thái tích hợp ERP.')
      }
    } finally {
      setLoadingStatus(false)
    }
  }, [token])

  const loadConfig = useCallback(async () => {
    if (!token) return
    setLoadingConfig(true)
    setConfigError(null)
    setConfigValidation({})
    try {
      const data = await fetchErpIntegrationConfig(token)
      setConfig(data)
      setConfigForm({
        enabled: data.enabled,
        provider: data.provider || 'MISA',
        baseUrl: data.baseUrl || '',
        companyCode: data.companyCode || '',
        timeoutSeconds: data.timeoutSeconds,
        apiKey: '',
        clearApiKey: false,
      })
    } catch (err) {
      if (err instanceof ApiError) {
        setConfigError(err.message)
      } else {
        setConfigError('Không tải được cấu hình ERP.')
      }
    } finally {
      setLoadingConfig(false)
    }
  }, [token])

  useEffect(() => {
    void loadStatus()
    void loadConfig()
  }, [loadConfig, loadStatus])

  const handleSaveConfig = async () => {
    if (!token) return

    const validation = validateConfigForm(configForm, config)
    if (Object.keys(validation).length > 0) {
      setConfigValidation(validation)
      setConfigError(validation.form ?? 'Vui lòng kiểm tra lại thông tin cấu hình.')
      return
    }

    setConfigValidation({})
    setSavingConfig(true)
    setSuccess(null)
    setConfigError(null)
    try {
      const updated = await updateErpIntegrationConfig(token, {
        enabled: configForm.enabled,
        provider: normalizeProvider(configForm.provider),
        baseUrl: normalizeText(configForm.baseUrl),
        companyCode: normalizeText(configForm.companyCode),
        timeoutSeconds: configForm.timeoutSeconds,
        apiKey: normalizeText(configForm.apiKey),
        clearApiKey: configForm.clearApiKey,
      })
      setConfig(updated)
      setConfigForm((prev) => ({
        ...prev,
        enabled: updated.enabled,
        provider: updated.provider || 'MISA',
        baseUrl: updated.baseUrl || '',
        companyCode: updated.companyCode || '',
        timeoutSeconds: updated.timeoutSeconds,
        apiKey: '',
        clearApiKey: false,
      }))
      await loadStatus()
      setSuccess('Đã lưu cấu hình kết nối ERP.')
    } catch (err) {
      if (err instanceof ApiError) {
        setConfigError(err.message)
      } else {
        setConfigError('Không thể lưu cấu hình ERP.')
      }
    } finally {
      setSavingConfig(false)
    }
  }

  const handleSync = async () => {
    if (!token) return
    setSyncLoading(true)
    setSyncError(null)
    try {
      const result = await syncErpSummary(token, {
        from: normalizeText(form.from),
        to: normalizeText(form.to),
        asOfDate: normalizeText(form.asOfDate),
        dueSoonDays: form.dueSoonDays,
        dryRun: form.dryRun,
      })
      setSyncResult(result)
      await loadStatus()
    } catch (err) {
      if (err instanceof ApiError) {
        setSyncError(err.message)
      } else {
        setSyncError('Không thể đồng bộ ERP.')
      }
    } finally {
      setSyncLoading(false)
    }
  }

  return (
    <div className="page-stack">
      <div className="page-header">
        <div>
          <h2>Tích hợp ERP</h2>
          <p className="muted">Theo dõi trạng thái kết nối MISA/ERP và đồng bộ báo cáo tổng hợp thủ công.</p>
        </div>
        <div className="header-actions">
          <button className="btn btn-outline" type="button" onClick={loadStatus} disabled={loadingStatus}>
            {loadingStatus ? 'Đang tải...' : 'Làm mới trạng thái'}
          </button>
        </div>
      </div>

      {success && (
        <div className="alert alert--success" role="status">
          {success}
        </div>
      )}

      <section className="card">
        <div className="card-row">
          <div>
            <h3>Cấu hình kết nối</h3>
            <p className="muted">Thiết lập thông tin kết nối ERP. API key sẽ không hiển thị lại sau khi lưu.</p>
          </div>
          <div className="header-actions">
            <button className="btn btn-outline" type="button" onClick={loadConfig} disabled={loadingConfig}>
              {loadingConfig ? 'Đang tải...' : 'Tải lại cấu hình'}
            </button>
          </div>
        </div>

        {configError && (
          <div className="alert alert--error" role="alert">
            {configError}
          </div>
        )}

        <div className="form-grid">
          <label className={`field${configValidation.provider ? ' field--error' : ''}`}>
            <span>Provider</span>
            <input
              type="text"
              value={configForm.provider}
              onChange={(event) =>
                setConfigForm((prev) => ({
                  ...prev,
                  provider: event.target.value,
                }))
              }
            />
            {configValidation.provider && <span className="field-error">{configValidation.provider}</span>}
          </label>
          <label className={`field${configValidation.baseUrl ? ' field--error' : ''}`}>
            <span>Base URL</span>
            <input
              type="text"
              placeholder="https://erp.example.com"
              value={configForm.baseUrl}
              onChange={(event) =>
                setConfigForm((prev) => ({
                  ...prev,
                  baseUrl: event.target.value,
                }))
              }
            />
            {configValidation.baseUrl && <span className="field-error">{configValidation.baseUrl}</span>}
          </label>
          <label className={`field${configValidation.companyCode ? ' field--error' : ''}`}>
            <span>Mã công ty</span>
            <input
              type="text"
              value={configForm.companyCode}
              onChange={(event) =>
                setConfigForm((prev) => ({
                  ...prev,
                  companyCode: event.target.value,
                }))
              }
            />
            {configValidation.companyCode && <span className="field-error">{configValidation.companyCode}</span>}
          </label>
          <label className={`field${configValidation.timeoutSeconds ? ' field--error' : ''}`}>
            <span>Timeout (giây)</span>
            <input
              type="number"
              min={5}
              max={120}
              value={configForm.timeoutSeconds}
              onChange={(event) =>
                setConfigForm((prev) => ({
                  ...prev,
                  timeoutSeconds: Number(event.target.value) || 15,
                }))
              }
            />
            {configValidation.timeoutSeconds && <span className="field-error">{configValidation.timeoutSeconds}</span>}
          </label>
          <label className={`field${configValidation.apiKey ? ' field--error' : ''}`}>
            <span>API key (nhập mới)</span>
            <input
              type="password"
              placeholder="Để trống để giữ nguyên"
              value={configForm.apiKey}
              onChange={(event) =>
                setConfigForm((prev) => ({
                  ...prev,
                  apiKey: event.target.value,
                  clearApiKey: false,
                }))
              }
            />
            {configValidation.apiKey && <span className="field-error">{configValidation.apiKey}</span>}
          </label>
          <label className="field">
            <span>Bật tích hợp</span>
            <select
              value={configForm.enabled ? 'enabled' : 'disabled'}
              onChange={(event) =>
                setConfigForm((prev) => ({
                  ...prev,
                  enabled: event.target.value === 'enabled',
                }))
              }
            >
              <option value="enabled">Bật</option>
              <option value="disabled">Tắt</option>
            </select>
          </label>
        </div>

        <label className="field-inline">
          <input
            type="checkbox"
            checked={configForm.clearApiKey}
            onChange={(event) =>
              setConfigForm((prev) => ({
                ...prev,
                clearApiKey: event.target.checked,
                apiKey: event.target.checked ? '' : prev.apiKey,
              }))
            }
          />
          Xóa API key hiện tại
        </label>

        {config && (
          <p className="muted">
            API key hiện tại: {config.hasApiKey ? 'Đã khai báo' : 'Chưa khai báo'}
            {config.updatedAtUtc ? ` · Cập nhật ${formatDateTime(config.updatedAtUtc)}` : ''}
            {config.updatedBy ? ` bởi ${config.updatedBy}` : ''}
          </p>
        )}

        <div className="form-actions">
          <button
            className="btn btn-primary"
            type="button"
            onClick={handleSaveConfig}
            disabled={savingConfig || loadingConfig || !configDirty}
          >
            {savingConfig ? 'Đang lưu...' : 'Lưu cấu hình'}
          </button>
        </div>
      </section>

      <section className="card">
        <div className="card-row">
          <div>
            <h3>Trạng thái kết nối</h3>
            <p className="muted">Kiểm tra cấu hình provider, endpoint và trạng thái lần đồng bộ gần nhất.</p>
          </div>
          <span className={`pill ${resolveStatusPillClass(status?.lastSyncStatus)}`}>
            {status?.lastSyncStatus ?? 'chưa_sync'}
          </span>
        </div>

        {status ? (
          <div className="kpi-grid">
            <article className="kpi-card">
              <span className="kpi-card__label">Provider</span>
              <strong>{status.provider}</strong>
            </article>
            <article className="kpi-card">
              <span className="kpi-card__label">Bật tích hợp</span>
              <strong>{status.enabled ? 'Có' : 'Không'}</strong>
            </article>
            <article className="kpi-card">
              <span className="kpi-card__label">Cấu hình đầy đủ</span>
              <strong>{status.configured ? 'Đã cấu hình' : 'Thiếu cấu hình'}</strong>
            </article>
            <article className="kpi-card">
              <span className="kpi-card__label">Timeout (giây)</span>
              <strong>{status.timeoutSeconds}</strong>
            </article>
            <article className="kpi-card">
              <span className="kpi-card__label">Base URL</span>
              <strong>{status.baseUrl || '-'}</strong>
            </article>
            <article className="kpi-card">
              <span className="kpi-card__label">Mã công ty</span>
              <strong>{status.companyCode || '-'}</strong>
            </article>
            <article className="kpi-card">
              <span className="kpi-card__label">API key</span>
              <strong>{status.hasApiKey ? 'Đã khai báo' : 'Chưa khai báo'}</strong>
            </article>
            <article className="kpi-card">
              <span className="kpi-card__label">Lần sync gần nhất</span>
              <strong>{status.lastSyncAtUtc ? formatDateTime(status.lastSyncAtUtc) : '-'}</strong>
            </article>
          </div>
        ) : (
          <div className="empty-state">{loadingStatus ? 'Đang tải trạng thái...' : 'Chưa có dữ liệu trạng thái.'}</div>
        )}

        {statusError && (
          <div className="alert alert--error" role="alert">
            {statusError}
          </div>
        )}

        {status?.lastSyncMessage && <p className="muted">Ghi chú: {status.lastSyncMessage}</p>}
      </section>

      <section className="card">
        <h3>Đồng bộ số liệu tổng hợp</h3>
        {syncError && (
          <div className="alert alert--error" role="alert">
            {syncError}
          </div>
        )}
        <div className="form-grid">
          <label className="field">
            <span>Từ ngày</span>
            <input
              type="date"
              value={form.from}
              onChange={(event) => setForm((prev) => ({ ...prev, from: event.target.value }))}
            />
          </label>
          <label className="field">
            <span>Đến ngày</span>
            <input
              type="date"
              value={form.to}
              onChange={(event) => setForm((prev) => ({ ...prev, to: event.target.value }))}
            />
          </label>
          <label className="field">
            <span>Tính đến ngày</span>
            <input
              type="date"
              value={form.asOfDate}
              onChange={(event) => setForm((prev) => ({ ...prev, asOfDate: event.target.value }))}
            />
          </label>
          <label className="field">
            <span>Số ngày sắp đến hạn</span>
            <input
              type="number"
              min={1}
              max={60}
              value={form.dueSoonDays}
              onChange={(event) =>
                setForm((prev) => ({
                  ...prev,
                  dueSoonDays: Number(event.target.value) || 7,
                }))
              }
            />
          </label>
          <label className="field">
            <span>Chế độ chạy</span>
            <select
              value={form.dryRun ? 'dry-run' : 'real'}
              onChange={(event) =>
                setForm((prev) => ({ ...prev, dryRun: event.target.value === 'dry-run' }))
              }
            >
              <option value="real">Đồng bộ thật</option>
              <option value="dry-run">Dry-run (không gửi ERP)</option>
            </select>
          </label>
        </div>

        <div className="form-actions">
          <button className="btn btn-primary" type="button" onClick={handleSync} disabled={syncLoading}>
            {syncLoading ? 'Đang đồng bộ...' : 'Đồng bộ tổng hợp'}
          </button>
        </div>

        {syncResult && (
          <div className="card card--subtle">
            <div className="card-row">
              <div>
                <h4>Kết quả lần chạy gần nhất</h4>
                <p className="muted">Thời gian: {formatDateTime(syncResult.executedAtUtc)}</p>
              </div>
              <span className={`pill ${resolveStatusPillClass(syncResult.status)}`}>{syncResult.status}</span>
            </div>
            <p className="muted">{syncResult.message}</p>
            <div className="kpi-grid">
              <article className="kpi-card">
                <span className="kpi-card__label">Tổng dư nợ</span>
                <strong>{formatMoney(syncResult.payload.totalOutstanding)}</strong>
              </article>
              <article className="kpi-card">
                <span className="kpi-card__label">Dư hóa đơn</span>
                <strong>{formatMoney(syncResult.payload.outstandingInvoice)}</strong>
              </article>
              <article className="kpi-card">
                <span className="kpi-card__label">Dư khoản trả hộ</span>
                <strong>{formatMoney(syncResult.payload.outstandingAdvance)}</strong>
              </article>
              <article className="kpi-card">
                <span className="kpi-card__label">Quá hạn</span>
                <strong>{formatMoney(syncResult.payload.overdueAmount)}</strong>
              </article>
              <article className="kpi-card">
                <span className="kpi-card__label">Số KH quá hạn</span>
                <strong>{formatCount(syncResult.payload.overdueCustomers)}</strong>
              </article>
              <article className="kpi-card">
                <span className="kpi-card__label">Sắp đến hạn</span>
                <strong>{formatMoney(syncResult.payload.dueSoonAmount)}</strong>
              </article>
              <article className="kpi-card">
                <span className="kpi-card__label">Số KH sắp đến hạn</span>
                <strong>{formatCount(syncResult.payload.dueSoonCustomers)}</strong>
              </article>
              <article className="kpi-card">
                <span className="kpi-card__label">KH đúng hạn</span>
                <strong>{formatCount(syncResult.payload.onTimeCustomers)}</strong>
              </article>
              <article className="kpi-card">
                <span className="kpi-card__label">Phiếu thu treo</span>
                <strong>{formatMoney(syncResult.payload.unallocatedReceiptsAmount)}</strong>
              </article>
              <article className="kpi-card">
                <span className="kpi-card__label">Số phiếu thu treo</span>
                <strong>{formatCount(syncResult.payload.unallocatedReceiptsCount)}</strong>
              </article>
            </div>
            {syncResult.requestId && <p className="muted">Request ID: {syncResult.requestId}</p>}
          </div>
        )}
      </section>
    </div>
  )
}
