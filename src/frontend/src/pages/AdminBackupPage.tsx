import { useCallback, useEffect, useState } from 'react'
import type { ChangeEvent } from 'react'
import { ApiError } from '../api/client'
import type {
  BackupAuditItem,
  BackupJobDetail,
  BackupJobListItem,
  BackupSettings,
} from '../api/backup'
import {
  downloadBackupFile,
  fetchBackupAudit,
  fetchBackupJob,
  fetchBackupJobs,
  fetchBackupSettings,
  fetchBackupStatus,
  issueBackupDownloadToken,
  restoreBackup,
  runManualBackup,
  updateBackupSettings,
  uploadBackupFile,
} from '../api/backup'
import DataTable from '../components/DataTable'
import { useAuth } from '../context/AuthStore'
import { formatDateTime } from '../utils/format'

const dayOptions = [
  { value: 0, label: 'Chủ nhật' },
  { value: 1, label: 'Thứ 2' },
  { value: 2, label: 'Thứ 3' },
  { value: 3, label: 'Thứ 4' },
  { value: 4, label: 'Thứ 5' },
  { value: 5, label: 'Thứ 6' },
  { value: 6, label: 'Thứ 7' },
]

const statusLabels: Record<string, string> = {
  queued: 'Đang xếp hàng',
  running: 'Đang chạy',
  success: 'Thành công',
  failed: 'Thất bại',
  skipped: 'Bỏ qua',
}

const typeLabels: Record<string, string> = {
  manual: 'Thủ công',
  scheduled: 'Tự động',
}

const formatFileSize = (value?: number | null) => {
  if (!value) return '-'
  if (value < 1024) return `${value} B`
  if (value < 1024 * 1024) return `${Math.round(value / 1024)} KB`
  if (value < 1024 * 1024 * 1024) return `${Math.round(value / (1024 * 1024))} MB`
  return `${Math.round(value / (1024 * 1024 * 1024))} GB`
}

export default function AdminBackupPage() {
  const { state } = useAuth()
  const token = state.accessToken ?? ''
  const canRestore = state.roles.includes('Admin')

  const [settings, setSettings] = useState<BackupSettings | null>(null)
  const [status, setStatus] = useState<{ maintenance: boolean; message?: string | null }>({
    maintenance: false,
    message: null,
  })
  const [jobs, setJobs] = useState<BackupJobListItem[]>([])
  const [audit, setAudit] = useState<BackupAuditItem[]>([])
  const [logJob, setLogJob] = useState<BackupJobDetail | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [total, setTotal] = useState(0)
  const [auditPage, setAuditPage] = useState(1)
  const [auditTotal, setAuditTotal] = useState(0)
  const [uploadFile, setUploadFile] = useState<File | null>(null)
  const [uploadId, setUploadId] = useState<string | null>(null)
  const [uploadMessage, setUploadMessage] = useState<string | null>(null)

  const loadSettings = useCallback(async () => {
    const result = await fetchBackupSettings(token)
    setSettings(result)
  }, [token])

  const loadJobs = useCallback(
    async (nextPage = page, nextSize = pageSize) => {
      const result = await fetchBackupJobs(token, { page: nextPage, pageSize: nextSize })
      setJobs(result.items)
      setTotal(result.total)
      setPage(result.page)
      setPageSize(result.pageSize)
    },
    [page, pageSize, token],
  )

  const loadAudit = useCallback(
    async (nextPage = auditPage) => {
      const result = await fetchBackupAudit(token, nextPage, 20)
      setAudit(result.items)
      setAuditPage(result.page)
      setAuditTotal(result.total)
    },
    [auditPage, token],
  )

  const loadStatus = useCallback(async () => {
    const result = await fetchBackupStatus(token)
    setStatus(result)
  }, [token])

  useEffect(() => {
    if (!token) return
    let active = true

    const loadAll = async () => {
      setLoading(true)
      setError(null)
      try {
        await Promise.all([loadSettings(), loadJobs(1, pageSize), loadAudit(1), loadStatus()])
      } catch (err) {
        if (!active) return
        if (err instanceof ApiError) {
          setError(err.message)
        } else {
          setError('Không tải được dữ liệu sao lưu.')
        }
      } finally {
        if (active) {
          setLoading(false)
        }
      }
    }

    void loadAll()

    return () => {
      active = false
      setNotice(null)
    }
  }, [token, pageSize, loadAudit, loadJobs, loadSettings, loadStatus])

  const handleSaveSettings = async () => {
    if (!token || !settings) return
    setError(null)
    setNotice(null)
    try {
      const result = await updateBackupSettings(token, settings)
      setSettings(result)
      setNotice('Đã lưu cấu hình sao lưu.')
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else {
        setError('Không lưu được cấu hình.')
      }
    }
  }

  const handleRunBackup = async () => {
    if (!token) return
    setError(null)
    setNotice(null)
    try {
      await runManualBackup(token)
      setNotice('Đã xếp hàng sao lưu.')
      await loadJobs()
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else {
        setError('Không tạo được bản sao lưu.')
      }
    }
  }

  const handleDownload = async (job: BackupJobListItem) => {
    if (!token) return
    setError(null)
    try {
      const tokenResult = await issueBackupDownloadToken(token, job.id)
      const { blob, fileName } = await downloadBackupFile(token, job.id, tokenResult.token)
      const url = window.URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = fileName
      document.body.appendChild(link)
      link.click()
      link.remove()
      window.URL.revokeObjectURL(url)
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else {
        setError('Không tải được file sao lưu.')
      }
    }
  }

  const handleRestoreJob = async (job: BackupJobListItem) => {
    if (!token || !canRestore) return
    const confirm = window.prompt('Nhập RESTORE để xác nhận phục hồi dữ liệu.')
    if (!confirm) return
    setError(null)
    setNotice('Đang phục hồi dữ liệu. Vui lòng chờ.')
    try {
      await restoreBackup(token, { jobId: job.id, confirmPhrase: confirm })
      await loadStatus()
    } catch (err) {
      setNotice(null)
      if (err instanceof ApiError) {
        setError(err.message)
      } else {
        setError('Không phục hồi được dữ liệu.')
      }
    }
  }

  const handleUploadChange = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0] ?? null
    setUploadFile(file)
    setUploadId(null)
    setUploadMessage(null)
  }

  const handleUpload = async () => {
    if (!token || !uploadFile) return
    setError(null)
    setUploadMessage(null)
    try {
      const result = await uploadBackupFile(token, uploadFile)
      setUploadId(result.uploadId)
      setUploadMessage(`Đã tải file ${result.fileName}.`)
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else {
        setError('Không tải được file phục hồi.')
      }
    }
  }

  const handleRestoreUpload = async () => {
    if (!token || !uploadId || !canRestore) return
    const confirm = window.prompt('Nhập RESTORE để xác nhận phục hồi dữ liệu.')
    if (!confirm) return
    setError(null)
    setNotice('Đang phục hồi dữ liệu. Vui lòng chờ.')
    try {
      await restoreBackup(token, { uploadId, confirmPhrase: confirm })
      await loadStatus()
    } catch (err) {
      setNotice(null)
      if (err instanceof ApiError) {
        setError(err.message)
      } else {
        setError('Không phục hồi được dữ liệu.')
      }
    }
  }

  const handleViewLog = async (jobId: string) => {
    if (!token) return
    try {
      const detail = await fetchBackupJob(token, jobId)
      setLogJob(detail)
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else {
        setError('Không tải được log.')
      }
    }
  }

  const jobColumns = [
    {
      key: 'createdAt',
      label: 'Thời gian',
      render: (row: BackupJobListItem) => formatDateTime(row.createdAt),
    },
    {
      key: 'type',
      label: 'Loại',
      render: (row: BackupJobListItem) => typeLabels[row.type] ?? row.type,
    },
    {
      key: 'status',
      label: 'Trạng thái',
      render: (row: BackupJobListItem) => statusLabels[row.status] ?? row.status,
    },
    {
      key: 'fileSize',
      label: 'Kích thước',
      align: 'right' as const,
      render: (row: BackupJobListItem) => formatFileSize(row.fileSize),
    },
    {
      key: 'actions',
      label: 'Thao tác',
      render: (row: BackupJobListItem) => (
        <div className="table-actions">
          {row.status === 'success' && (
            <button className="btn btn-ghost" type="button" onClick={() => handleDownload(row)}>
              Tải về
            </button>
          )}
          {row.status !== 'queued' && (
            <button className="btn btn-ghost" type="button" onClick={() => handleViewLog(row.id)}>
              Xem log
            </button>
          )}
          {canRestore && row.status === 'success' && (
            <button className="btn btn-danger" type="button" onClick={() => handleRestoreJob(row)}>
              Phục hồi
            </button>
          )}
        </div>
      ),
    },
  ]

  const auditColumns = [
    {
      key: 'createdAt',
      label: 'Thời gian',
      render: (row: BackupAuditItem) => formatDateTime(row.createdAt),
    },
    { key: 'action', label: 'Hành động' },
    { key: 'result', label: 'Kết quả' },
    {
      key: 'details',
      label: 'Chi tiết',
      render: (row: BackupAuditItem) => row.details ?? '-',
    },
  ]

  if (!settings) {
    return (
      <div className="page-stack">
        <div className="page-header">
          <div>
            <h2>Sao lưu &amp; phục hồi dữ liệu</h2>
          </div>
        </div>
        {loading ? <div className="empty-state">Đang tải...</div> : null}
        {error ? <div className="alert alert--error">{error}</div> : null}
      </div>
    )
  }

  return (
    <div className="page-stack">
      <div className="page-header">
        <div>
          <h2>Sao lưu &amp; phục hồi dữ liệu</h2>
          <p className="muted">Quản lý sao lưu thủ công và tự động cho hệ thống.</p>
        </div>
        <div className="header-actions">
          <button className="btn btn-primary" type="button" onClick={handleRunBackup}>
            Tạo sao lưu ngay
          </button>
        </div>
      </div>

      {status.maintenance && (
        <div className="alert alert--warn">{status.message ?? 'Hệ thống đang phục hồi dữ liệu.'}</div>
      )}
      {notice && <div className="alert alert--success">{notice}</div>}
      {error && <div className="alert alert--error">{error}</div>}

      <section className="card">
        <h3>Cấu hình sao lưu tự động</h3>
        <div className="form-grid">
          <label className="field">
            <span>Bật tự động</span>
            <select
              value={settings.enabled ? 'true' : 'false'}
              onChange={(event) =>
                setSettings((prev) =>
                  prev ? { ...prev, enabled: event.target.value === 'true' } : prev,
                )
              }
            >
              <option value="true">Bật</option>
              <option value="false">Tắt</option>
            </select>
          </label>
          <label className="field">
            <span>Ngày chạy</span>
            <select
              value={settings.scheduleDayOfWeek}
              onChange={(event) =>
                setSettings((prev) =>
                  prev ? { ...prev, scheduleDayOfWeek: Number(event.target.value) } : prev,
                )
              }
            >
              {dayOptions.map((item) => (
                <option key={item.value} value={item.value}>
                  {item.label}
                </option>
              ))}
            </select>
          </label>
          <label className="field">
            <span>Giờ chạy</span>
            <input
              type="time"
              value={settings.scheduleTime}
              onChange={(event) =>
                setSettings((prev) => (prev ? { ...prev, scheduleTime: event.target.value } : prev))
              }
            />
          </label>
          <label className="field">
            <span>Thư mục lưu</span>
            <input
              value={settings.backupPath}
              onChange={(event) =>
                setSettings((prev) => (prev ? { ...prev, backupPath: event.target.value } : prev))
              }
            />
          </label>
          <label className="field">
            <span>Số bản lưu giữ</span>
            <input
              type="number"
              min={1}
              max={200}
              value={settings.retentionCount}
              onChange={(event) =>
                setSettings((prev) =>
                  prev ? { ...prev, retentionCount: Number(event.target.value) } : prev,
                )
              }
            />
          </label>
          <label className="field">
            <span>Đường dẫn pg_bin</span>
            <input
              value={settings.pgBinPath}
              onChange={(event) =>
                setSettings((prev) => (prev ? { ...prev, pgBinPath: event.target.value } : prev))
              }
            />
          </label>
        </div>
        <div className="form-actions">
          <button className="btn btn-primary" type="button" onClick={handleSaveSettings}>
            Lưu cấu hình
          </button>
        </div>
      </section>

      <section className="card">
        <h3>Sao lưu thủ công &amp; phục hồi</h3>
        <div className="form-grid">
          <label className="field">
            <span>Tải file .dump để phục hồi</span>
            <input type="file" accept=".dump" onChange={handleUploadChange} />
          </label>
          <div className="field">
            <span>&nbsp;</span>
            <div className="form-actions">
              <button className="btn btn-ghost" type="button" onClick={handleUpload} disabled={!uploadFile}>
                Tải file
              </button>
              <button
                className="btn btn-danger"
                type="button"
                onClick={handleRestoreUpload}
                disabled={!uploadId || !canRestore}
              >
                Phục hồi từ file
              </button>
            </div>
          </div>
        </div>
        {uploadMessage && <p className="muted">{uploadMessage}</p>}
      </section>

      <section className="card">
        <h3>Danh sách sao lưu</h3>
        <DataTable
          columns={jobColumns}
          rows={jobs}
          getRowKey={(row) => row.id}
          emptyMessage={loading ? 'Đang tải...' : 'Chưa có bản sao lưu.'}
          pagination={{ page, pageSize, total }}
          onPageChange={(next) => loadJobs(next, pageSize)}
          onPageSizeChange={(nextSize) => loadJobs(1, nextSize)}
        />
        {logJob && (
          <div className="form-stack">
            <h4>Log sao lưu</h4>
            <p className="muted">Job {logJob.id}</p>
            <div className="card">
              <h5>STDOUT</h5>
              <pre className="code-block">{logJob.stdoutLog || '-'}</pre>
              <h5>STDERR</h5>
              <pre className="code-block">{logJob.stderrLog || '-'}</pre>
            </div>
          </div>
        )}
      </section>

      <section className="card">
        <h3>Nhật ký thao tác</h3>
        <DataTable
          columns={auditColumns}
          rows={audit}
          getRowKey={(row) => row.id}
          emptyMessage="Chưa có nhật ký."
          pagination={{ page: auditPage, pageSize: 20, total: auditTotal }}
          onPageChange={(next) => loadAudit(next)}
        />
      </section>
    </div>
  )
}
