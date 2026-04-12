import { useCallback, useEffect, useState } from 'react'
import type { ChangeEvent } from 'react'
import { ApiError } from '../api/client'
import type {
  BackupAuditItem,
  BackupJobDetail,
  BackupJobListItem,
  BackupOffsiteUpload,
  BackupSettings,
} from '../api/backup'
import {
  completeBackupGoogleDriveCallback,
  downloadBackupFile,
  disconnectBackupOffsiteConnection,
  fetchBackupAudit,
  fetchBackupJob,
  fetchBackupJobs,
  fetchBackupOffsiteUploads,
  fetchBackupSettings,
  fetchBackupStatus,
  issueBackupDownloadToken,
  requestBackupGoogleDriveConnectUrl,
  reuploadBackupJob,
  restoreBackup,
  runManualBackup,
  testBackupOffsiteUpload,
  updateBackupSettings,
  uploadBackupFile,
} from '../api/backup'
import DataTable from '../components/DataTable'
import ActionConfirmModal, { type ActionConfirmPayload } from '../components/modals/ActionConfirmModal'
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

const offsiteProviderLabels: Record<string, string> = {
  google_drive: 'Google Drive',
}

const formatFileSize = (value?: number | null) => {
  if (!value) return '-'
  if (value < 1024) return `${value} B`
  if (value < 1024 * 1024) return `${Math.round(value / 1024)} KB`
  if (value < 1024 * 1024 * 1024) return `${Math.round(value / (1024 * 1024))} MB`
  return `${Math.round(value / (1024 * 1024 * 1024))} GB`
}

const buildGoogleDriveRedirectUri = () => `${window.location.origin}${window.location.pathname}`

const clearGoogleDriveCallbackQuery = () => {
  const url = new URL(window.location.href)
  ;['code', 'scope', 'state', 'error', 'error_description'].forEach((key) => {
    url.searchParams.delete(key)
  })
  const nextSearch = url.searchParams.toString()
  const nextUrl = `${url.pathname}${nextSearch ? `?${nextSearch}` : ''}${url.hash}`
  window.history.replaceState({}, document.title, nextUrl)
}

type RestoreTarget =
  | { kind: 'job'; jobId: string; label: string }
  | { kind: 'upload'; uploadId: string; label: string }

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
  const [offsiteUploads, setOffsiteUploads] = useState<BackupOffsiteUpload[]>([])
  const [audit, setAudit] = useState<BackupAuditItem[]>([])
  const [logJob, setLogJob] = useState<BackupJobDetail | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [total, setTotal] = useState(0)
  const [offsitePage, setOffsitePage] = useState(1)
  const [offsitePageSize, setOffsitePageSize] = useState(10)
  const [offsiteTotal, setOffsiteTotal] = useState(0)
  const [auditPage, setAuditPage] = useState(1)
  const [auditTotal, setAuditTotal] = useState(0)
  const [uploadFile, setUploadFile] = useState<File | null>(null)
  const [uploadId, setUploadId] = useState<string | null>(null)
  const [uploadMessage, setUploadMessage] = useState<string | null>(null)
  const [restoreTarget, setRestoreTarget] = useState<RestoreTarget | null>(null)
  const [restoreLoading, setRestoreLoading] = useState(false)
  const [restoreError, setRestoreError] = useState<string | null>(null)
  const [offsiteBusy, setOffsiteBusy] = useState(false)

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

  const loadOffsiteUploads = useCallback(
    async (nextPage = offsitePage, nextSize = offsitePageSize) => {
      const result = await fetchBackupOffsiteUploads(token, { page: nextPage, pageSize: nextSize })
      setOffsiteUploads(result.items)
      setOffsiteTotal(result.total)
      setOffsitePage(result.page)
      setOffsitePageSize(result.pageSize)
    },
    [offsitePage, offsitePageSize, token],
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
        await Promise.all([
          loadSettings(),
          loadJobs(1, pageSize),
          loadOffsiteUploads(1, offsitePageSize),
          loadAudit(1),
          loadStatus(),
        ])
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
  }, [token, pageSize, offsitePageSize, loadAudit, loadJobs, loadOffsiteUploads, loadSettings, loadStatus])

  useEffect(() => {
    if (!token) return

    const url = new URL(window.location.href)
    const code = url.searchParams.get('code')
    const oauthError = url.searchParams.get('error')
    const redirectUri = buildGoogleDriveRedirectUri()

    if (!code && !oauthError) {
      return
    }

    let active = true

    const handleCallback = async () => {
      setOffsiteBusy(true)
      setError(null)
      try {
        if (oauthError) {
          const description = url.searchParams.get('error_description')
          throw new ApiError(description || 'Google Drive từ chối kết nối.', 400)
        }

        await completeBackupGoogleDriveCallback(token, {
          code: code ?? '',
          redirectUri,
          googleDriveFolderId:
            url.searchParams.get('googleDriveFolderId') ??
            settings?.googleDriveFolderId ??
            null,
        })

        if (active) {
          setNotice('Đã kết nối Google Drive cho bản sao lưu offsite.')
        }

        await Promise.all([loadSettings(), loadOffsiteUploads(1, offsitePageSize)])
      } catch (err) {
        if (!active) return
        if (err instanceof ApiError) {
          setError(err.message)
        } else {
          setError('Không hoàn tất được kết nối Google Drive.')
        }
      } finally {
        clearGoogleDriveCallbackQuery()
        if (active) {
          setOffsiteBusy(false)
        }
      }
    }

    void handleCallback()

    return () => {
      active = false
    }
  }, [token, settings?.googleDriveFolderId, offsitePageSize, loadOffsiteUploads, loadSettings])

  const handleSaveSettings = async () => {
    if (!token || !settings) return
    setError(null)
    setNotice(null)
    try {
      const result = await updateBackupSettings(token, {
        ...settings,
        provider: settings.offsiteEnabled ? settings.provider || 'google_drive' : null,
        googleDriveFolderId: settings.offsiteEnabled
          ? settings.googleDriveFolderId?.trim() || null
          : null,
        offsiteRetentionCount: Math.max(1, settings.offsiteRetentionCount || 1),
      })
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

  const handleConnectGoogleDrive = async () => {
    if (!token || !settings) return
    setError(null)
    setNotice(null)
    setOffsiteBusy(true)
    try {
      const result = await requestBackupGoogleDriveConnectUrl(token, {
        redirectUri: buildGoogleDriveRedirectUri(),
        googleDriveFolderId: settings.googleDriveFolderId?.trim() || null,
      })
      window.location.assign(result.url)
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else {
        setError('Không tạo được liên kết kết nối Google Drive.')
      }
      setOffsiteBusy(false)
    }
  }

  const handleDisconnectGoogleDrive = async () => {
    if (!token) return
    setError(null)
    setNotice(null)
    setOffsiteBusy(true)
    try {
      await disconnectBackupOffsiteConnection(token)
      await Promise.all([loadSettings(), loadOffsiteUploads(1, offsitePageSize)])
      setNotice('Đã ngắt kết nối Google Drive.')
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else {
        setError('Không ngắt được kết nối Google Drive.')
      }
    } finally {
      setOffsiteBusy(false)
    }
  }

  const handleTestOffsite = async () => {
    if (!token) return
    setError(null)
    setNotice(null)
    setOffsiteBusy(true)
    try {
      await testBackupOffsiteUpload(token)
      await Promise.all([loadSettings(), loadOffsiteUploads(1, offsitePageSize)])
      setNotice('Đã xếp hàng upload kiểm tra lên Google Drive.')
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else {
        setError('Không tạo được upload kiểm tra.')
      }
    } finally {
      setOffsiteBusy(false)
    }
  }

  const handleReupload = async (jobId: string) => {
    if (!token) return
    setError(null)
    setNotice(null)
    setOffsiteBusy(true)
    try {
      await reuploadBackupJob(token, jobId)
      await loadOffsiteUploads()
      setNotice('Đã xếp hàng upload lại bản sao lưu lên Google Drive.')
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else {
        setError('Không upload lại được bản sao lưu.')
      }
    } finally {
      setOffsiteBusy(false)
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

  const handleRestoreJob = (job: BackupJobListItem) => {
    if (!token || !canRestore) return
    setError(null)
    setRestoreError(null)
    setRestoreTarget({
      kind: 'job',
      jobId: job.id,
      label: job.fileName?.trim() || job.id,
    })
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

  const handleRestoreUpload = () => {
    if (!token || !uploadId || !canRestore) return
    setError(null)
    const uploadLabel = uploadFile?.name?.trim() || uploadId
    setRestoreError(null)
    setRestoreTarget({ kind: 'upload', uploadId, label: uploadLabel })
  }

  const handleConfirmRestore = useCallback(
    async (payload: ActionConfirmPayload) => {
      if (!token || !restoreTarget) return
      setRestoreLoading(true)
      setRestoreError(null)
      setError(null)
      setNotice('Đang phục hồi dữ liệu. Vui lòng chờ.')
      try {
        if (restoreTarget.kind === 'job') {
          await restoreBackup(token, {
            jobId: restoreTarget.jobId,
            confirmPhrase: payload.reason,
          })
        } else {
          await restoreBackup(token, {
            uploadId: restoreTarget.uploadId,
            confirmPhrase: payload.reason,
          })
        }
        await loadStatus()
        setRestoreTarget(null)
      } catch (err) {
        setNotice(null)
        if (err instanceof ApiError) {
          setRestoreError(err.message)
        } else {
          setRestoreError('Không phục hồi được dữ liệu.')
        }
      } finally {
        setRestoreLoading(false)
      }
    },
    [token, restoreTarget, loadStatus],
  )

  const handleCloseRestoreModal = () => {
    if (restoreLoading) return
    setRestoreTarget(null)
    setRestoreError(null)
  }

  const restoreDescription =
    restoreTarget?.kind === 'job'
      ? `Nhập RESTORE để phục hồi dữ liệu từ bản sao lưu "${restoreTarget.label}".`
      : restoreTarget
        ? `Nhập RESTORE để phục hồi dữ liệu từ file "${restoreTarget.label}".`
        : undefined

  const restoreTitle =
    restoreTarget?.kind === 'job' ? 'Phục hồi từ bản sao lưu' : 'Phục hồi từ file tải lên'

  const handleRestoreModalConfirm = (payload: ActionConfirmPayload) => {
    void handleConfirmRestore(payload)
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

  const offsiteColumns = [
    {
      key: 'createdAt',
      label: 'Thời gian',
      render: (row: BackupOffsiteUpload) => formatDateTime(row.createdAt),
    },
    {
      key: 'provider',
      label: 'Đích lưu',
      render: (row: BackupOffsiteUpload) => offsiteProviderLabels[row.provider] ?? row.provider,
    },
    {
      key: 'status',
      label: 'Trạng thái',
      render: (row: BackupOffsiteUpload) => statusLabels[row.status] ?? row.status,
    },
    {
      key: 'remoteFileSize',
      label: 'Kích thước',
      align: 'right' as const,
      render: (row: BackupOffsiteUpload) => formatFileSize(row.remoteFileSize),
    },
    {
      key: 'attemptCount',
      label: 'Lần thử',
      align: 'right' as const,
      render: (row: BackupOffsiteUpload) => row.attemptCount,
    },
    {
      key: 'errorMessage',
      label: 'Chi tiết',
      render: (row: BackupOffsiteUpload) => row.errorMessage ?? row.remoteFileId ?? '-',
    },
    {
      key: 'actions',
      label: 'Thao tác',
      render: (row: BackupOffsiteUpload) => (
        <div className="table-actions">
          {row.backupJobId && row.status === 'failed' && (
            <button
              className="btn btn-ghost"
              type="button"
              onClick={() => handleReupload(row.backupJobId ?? '')}
              disabled={offsiteBusy}
            >
              Upload lại
            </button>
          )}
        </div>
      ),
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

  const usesContainerPaths = settings.usesContainerPaths ?? false
  const hostBackupPath = settings.hostBackupPath?.trim() || '(chưa cấu hình)'
  const hostBackupPathConfigKey = settings.hostBackupPathConfigKey?.trim() || 'BACKUP_HOST_PATH'
  const canEditBackupPath = settings.canEditBackupPath ?? !usesContainerPaths
  const canEditPgBinPath = settings.canEditPgBinPath ?? !usesContainerPaths
  const offsiteProvider = settings.provider ?? ''
  const offsiteConnection = settings.offsiteConnection
  const offsiteConnected = offsiteConnection?.isConnected ?? false

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
      {usesContainerPaths && (
        <div className="alert alert--warn">
          Hệ thống đang chạy trong Docker. Muốn đổi thư mục lưu trên máy chủ, hãy cập nhật biến{' '}
          <strong>{hostBackupPathConfigKey}</strong> rồi khởi động lại <strong>docker compose</strong>.
          Nút <strong>Tải về</strong> vẫn cho phép chọn nơi lưu cục bộ trong trình duyệt.
        </div>
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
            <span>{usesContainerPaths ? 'Thư mục lưu trên máy chủ' : 'Thư mục lưu'}</span>
            <input
              value={usesContainerPaths ? hostBackupPath : settings.backupPath}
              readOnly={usesContainerPaths || !canEditBackupPath}
              onChange={
                canEditBackupPath
                  ? (event) =>
                      setSettings((prev) => (prev ? { ...prev, backupPath: event.target.value } : prev))
                  : undefined
              }
            />
            {usesContainerPaths && (
              <small className="muted">
                Đây là thư mục Windows/host đã bind mount vào container. Đổi qua biến{' '}
                {hostBackupPathConfigKey}.
              </small>
            )}
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
          {usesContainerPaths && (
            <label className="field">
              <span>Thư mục lưu trong container</span>
              <input value={settings.backupPath} readOnly />
              <small className="muted">
                Backend trong container thực sự ghi file vào đường dẫn này.
              </small>
            </label>
          )}
          <label className="field">
            <span>
              {usesContainerPaths ? 'Đường dẫn pg_dump / pg_restore trong container' : 'Đường dẫn pg_bin'}
            </span>
            <input
              value={settings.pgBinPath}
              readOnly={usesContainerPaths || !canEditPgBinPath}
              onChange={
                canEditPgBinPath
                  ? (event) =>
                      setSettings((prev) => (prev ? { ...prev, pgBinPath: event.target.value } : prev))
                  : undefined
              }
            />
            {usesContainerPaths && <small className="muted">Giá trị mặc định trong Docker thường là /usr/bin.</small>}
          </label>
        </div>
        <div className="form-actions">
          <button className="btn btn-primary" type="button" onClick={handleSaveSettings}>
            Lưu cấu hình
          </button>
        </div>
      </section>

      <section className="card">
        <h3>Lưu trữ offsite</h3>
        <div className="form-grid">
          <label className="field">
            <span>Bật lưu trữ offsite</span>
            <select
              value={settings.offsiteEnabled ? 'true' : 'false'}
              onChange={(event) =>
                setSettings((prev) =>
                  prev
                    ? {
                        ...prev,
                        offsiteEnabled: event.target.value === 'true',
                        provider:
                          event.target.value === 'true'
                            ? prev.provider || 'google_drive'
                            : null,
                      }
                    : prev,
                )
              }
            >
              <option value="false">Tắt</option>
              <option value="true">Bật</option>
            </select>
          </label>
          <label className="field">
            <span>Nhà cung cấp</span>
            <select
              value={offsiteProvider}
              onChange={(event) =>
                setSettings((prev) =>
                  prev
                    ? {
                        ...prev,
                        provider: event.target.value || null,
                      }
                    : prev,
                )
              }
              disabled={!settings.offsiteEnabled}
            >
              <option value="">Chọn nhà cung cấp</option>
              <option value="google_drive">Google Drive</option>
            </select>
          </label>
          <label className="field">
            <span>Google Drive Folder ID</span>
            <input
              value={settings.googleDriveFolderId ?? ''}
              disabled={!settings.offsiteEnabled || offsiteProvider !== 'google_drive'}
              onChange={(event) =>
                setSettings((prev) =>
                  prev ? { ...prev, googleDriveFolderId: event.target.value } : prev,
                )
              }
              placeholder="Thư mục đích trên Google Drive"
            />
          </label>
          <label className="field">
            <span>Số bản offsite lưu giữ</span>
            <input
              type="number"
              min={1}
              max={200}
              value={settings.offsiteRetentionCount}
              disabled={!settings.offsiteEnabled}
              onChange={(event) =>
                setSettings((prev) =>
                  prev ? { ...prev, offsiteRetentionCount: Number(event.target.value) } : prev,
                )
              }
            />
          </label>
          <label className="field">
            <span>Tự upload sau khi backup local</span>
            <select
              value={settings.uploadAfterBackup ? 'true' : 'false'}
              disabled={!settings.offsiteEnabled}
              onChange={(event) =>
                setSettings((prev) =>
                  prev
                    ? { ...prev, uploadAfterBackup: event.target.value === 'true' }
                    : prev,
                )
              }
            >
              <option value="true">Bật</option>
              <option value="false">Tắt</option>
            </select>
          </label>
          <div className="field">
            <span>Trạng thái kết nối</span>
            <div className="form-stack">
              <strong>
                {offsiteConnected
                  ? `${offsiteProviderLabels[offsiteConnection?.provider ?? ''] ?? offsiteConnection?.provider ?? 'Google Drive'} đã kết nối`
                  : 'Chưa kết nối'}
              </strong>
              <small className="muted">
                {offsiteConnection?.googleDriveFolderId
                  ? `Folder ID: ${offsiteConnection.googleDriveFolderId}`
                  : 'Chưa cấu hình thư mục đích.'}
              </small>
              <small className="muted">
                {offsiteConnection?.lastValidatedAt
                  ? `Kiểm tra gần nhất: ${formatDateTime(offsiteConnection.lastValidatedAt)}`
                  : offsiteConnection?.connectedAt
                    ? `Đã kết nối lúc: ${formatDateTime(offsiteConnection.connectedAt)}`
                    : 'Chưa có lịch sử xác thực.'}
              </small>
              {offsiteConnection?.lastError && (
                <small className="muted">Lỗi gần nhất: {offsiteConnection.lastError}</small>
              )}
            </div>
          </div>
        </div>
        <div className="form-actions">
          <button className="btn btn-primary" type="button" onClick={handleSaveSettings}>
            Lưu cấu hình offsite
          </button>
          <button
            className="btn btn-ghost"
            type="button"
            onClick={handleConnectGoogleDrive}
            disabled={
              offsiteBusy || !settings.offsiteEnabled || offsiteProvider !== 'google_drive'
            }
          >
            Kết nối Google Drive
          </button>
          <button
            className="btn btn-ghost"
            type="button"
            onClick={handleTestOffsite}
            disabled={offsiteBusy || !offsiteConnected}
          >
            Upload kiểm tra
          </button>
          <button
            className="btn btn-danger"
            type="button"
            onClick={handleDisconnectGoogleDrive}
            disabled={offsiteBusy || !offsiteConnected}
          >
            Ngắt kết nối
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
        <h3>Hàng đợi upload offsite</h3>
        <DataTable
          columns={offsiteColumns}
          rows={offsiteUploads}
          getRowKey={(row) => row.id}
          emptyMessage={loading ? 'Đang tải...' : 'Chưa có upload offsite.'}
          pagination={{ page: offsitePage, pageSize: offsitePageSize, total: offsiteTotal }}
          onPageChange={(next) => loadOffsiteUploads(next, offsitePageSize)}
          onPageSizeChange={(nextSize) => loadOffsiteUploads(1, nextSize)}
        />
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

      <ActionConfirmModal
        isOpen={Boolean(restoreTarget)}
        title={restoreTitle}
        description={restoreDescription}
        confirmLabel="Phục hồi dữ liệu"
        reasonRequired
        reasonLabel="Mã xác nhận"
        reasonPlaceholder="Nhập RESTORE"
        loading={restoreLoading}
        error={restoreError}
        tone="danger"
        onClose={handleCloseRestoreModal}
        onConfirm={handleRestoreModalConfirm}
      />
    </div>
  )
}
