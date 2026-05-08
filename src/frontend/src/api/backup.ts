import { ApiError, apiFetch, apiFetchBlob } from './client'
import type { PagedResult } from './types'

export type BackupSettings = {
  enabled: boolean
  scheduleFrequency: number
  backupPath: string
  retentionCount: number
  scheduleDayOfWeek: number
  scheduleTime: string
  timezone: string
  pgBinPath: string
  offsiteEnabled: boolean
  provider?: string | null
  googleDriveFolderId?: string | null
  offsiteRetentionCount: number
  uploadAfterBackup: boolean
  offsiteConnection?: BackupOffsiteConnectionStatus | null
  lastRunAt?: string | null
  usesContainerPaths?: boolean
  hostBackupPath?: string | null
  hostBackupPathConfigKey?: string | null
  canEditBackupPath?: boolean
  canEditPgBinPath?: boolean
}

export type BackupOffsiteConnectionStatus = {
  isConnected: boolean
  provider: string
  googleDriveFolderId?: string | null
  connectedAt?: string | null
  lastValidatedAt?: string | null
  lastError?: string | null
}

export type BackupJobListItem = {
  id: string
  type: string
  status: string
  createdAt: string
  startedAt?: string | null
  finishedAt?: string | null
  fileName?: string | null
  fileSize?: number | null
  errorMessage?: string | null
  createdBy?: string | null
}

export type BackupJobDetail = BackupJobListItem & {
  stdoutLog?: string | null
  stderrLog?: string | null
  downloadTokenExpiresAt?: string | null
}

export type BackupAuditItem = {
  id: string
  action: string
  actorId?: string | null
  result: string
  details?: string | null
  createdAt: string
}

export type BackupDownloadToken = {
  token: string
  expiresAt: string
}

export type BackupUploadResult = {
  uploadId: string
  fileName: string
  fileSize: number
  expiresAt: string
}

export type BackupStatus = {
  maintenance: boolean
  message?: string | null
}

export type BackupOffsiteConnectUrlResponse = {
  url: string
}

export type BackupOffsiteUpload = {
  id: string
  backupJobId?: string | null
  provider: string
  status: string
  remoteFileId?: string | null
  remoteChecksum?: string | null
  remoteFileSize?: number | null
  attemptCount: number
  errorMessage?: string | null
  createdAt: string
  queuedAt?: string | null
  completedAt?: string | null
}

export const fetchBackupSettings = async (token: string) => {
  return apiFetch<BackupSettings>('/admin/backup/settings', { token })
}

export const updateBackupSettings = async (token: string, payload: BackupSettings) => {
  return apiFetch<BackupSettings>('/admin/backup/settings', {
    token,
    method: 'PUT',
    body: {
      enabled: payload.enabled,
      scheduleFrequency: payload.scheduleFrequency,
      backupPath: payload.backupPath,
      retentionCount: payload.retentionCount,
      scheduleDayOfWeek: payload.scheduleDayOfWeek,
      scheduleTime: payload.scheduleTime,
      pgBinPath: payload.pgBinPath,
      offsiteEnabled: payload.offsiteEnabled,
      provider: payload.provider,
      googleDriveFolderId: payload.googleDriveFolderId,
      offsiteRetentionCount: payload.offsiteRetentionCount,
      uploadAfterBackup: payload.uploadAfterBackup,
    },
  })
}

export const fetchBackupOffsiteConnection = async (token: string) => {
  return apiFetch<BackupOffsiteConnectionStatus>('/admin/backup/offsite/connection', { token })
}

export const requestBackupGoogleDriveConnectUrl = async (
  token: string,
  payload: { redirectUri: string; googleDriveFolderId?: string | null },
) => {
  return apiFetch<BackupOffsiteConnectUrlResponse>('/admin/backup/offsite/google-drive/connect-url', {
    token,
    method: 'POST',
    body: payload,
  })
}

export const completeBackupGoogleDriveCallback = async (
  token: string,
  payload: { code: string; redirectUri: string; googleDriveFolderId?: string | null },
) => {
  return apiFetch<BackupOffsiteConnectionStatus>('/admin/backup/offsite/google-drive/callback', {
    token,
    method: 'POST',
    body: payload,
  })
}

export const disconnectBackupOffsiteConnection = async (token: string) => {
  return apiFetch<void>('/admin/backup/offsite/connection', {
    token,
    method: 'DELETE',
  })
}

export const fetchBackupOffsiteUploads = async (
  token: string,
  params?: { page?: number; pageSize?: number },
) => {
  const query = new URLSearchParams()
  if (params?.page) query.set('page', String(params.page))
  if (params?.pageSize) query.set('pageSize', String(params.pageSize))
  const suffix = query.toString()
  return apiFetch<PagedResult<BackupOffsiteUpload>>(
    `/admin/backup/offsite/uploads${suffix ? `?${suffix}` : ''}`,
    { token },
  )
}

export const reuploadBackupJob = async (token: string, id: string) => {
  return apiFetch<BackupOffsiteUpload>(`/admin/backup/jobs/${id}/reupload`, {
    token,
    method: 'POST',
  })
}

export const testBackupOffsiteUpload = async (token: string) => {
  return apiFetch<BackupOffsiteUpload>('/admin/backup/offsite/test-upload', {
    token,
    method: 'POST',
  })
}

export const runManualBackup = async (token: string) => {
  return apiFetch<BackupJobListItem>('/admin/backup/run', { token, method: 'POST' })
}

export const fetchBackupJobs = async (token: string, params?: {
  page?: number
  pageSize?: number
  status?: string
  type?: string
}) => {
  const query = new URLSearchParams()
  if (params?.page) query.set('page', String(params.page))
  if (params?.pageSize) query.set('pageSize', String(params.pageSize))
  if (params?.status) query.set('status', params.status)
  if (params?.type) query.set('type', params.type)
  const suffix = query.toString()
  return apiFetch<PagedResult<BackupJobListItem>>(`/admin/backup/jobs${suffix ? `?${suffix}` : ''}`, {
    token,
  })
}

export const fetchBackupJob = async (token: string, id: string) => {
  return apiFetch<BackupJobDetail>(`/admin/backup/jobs/${id}`, { token })
}

export const issueBackupDownloadToken = async (token: string, id: string) => {
  return apiFetch<BackupDownloadToken>(`/admin/backup/jobs/${id}/download-token`, {
    token,
    method: 'POST',
  })
}

export const downloadBackupFile = async (token: string, id: string, downloadToken: string) => {
  const { blob, headers } = await apiFetchBlob(
    `/admin/backup/download/${id}?token=${encodeURIComponent(downloadToken)}`,
    { token },
  )
  const fileName = headers.get('content-disposition')?.split('filename=')?.[1]?.replace(/"/g, '')
  return { blob, fileName: fileName || `backup_${id}.dump` }
}

export const fetchBackupAudit = async (token: string, page = 1, pageSize = 20) => {
  return apiFetch<PagedResult<BackupAuditItem>>(`/admin/backup/audit?page=${page}&pageSize=${pageSize}`, {
    token,
  })
}

export const fetchBackupStatus = async (token: string) => {
  return apiFetch<BackupStatus>('/admin/backup/status', { token })
}

export const uploadBackupFile = async (token: string, file: File) => {
  const form = new FormData()
  form.append('file', file)

  const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? ''

  return new Promise<BackupUploadResult>((resolve, reject) => {
    const xhr = new XMLHttpRequest()
    xhr.open('POST', `${apiBaseUrl}/admin/backup/upload`)
    xhr.setRequestHeader('Authorization', `Bearer ${token}`)
    xhr.withCredentials = true

    xhr.onload = () => {
      const success = xhr.status >= 200 && xhr.status < 300
      if (success) {
        try {
          resolve(JSON.parse(xhr.responseText) as BackupUploadResult)
        } catch {
          reject(new ApiError('Không đọc được phản hồi từ máy chủ.', xhr.status))
        }
        return
      }
      reject(new ApiError('Không tải được file sao lưu.', xhr.status))
    }

    xhr.onerror = () => reject(new ApiError('Không tải được file sao lưu.', 0))
    xhr.send(form)
  })
}

export const restoreBackup = async (token: string, payload: {
  jobId?: string
  uploadId?: string
  confirmPhrase: string
}) => {
  return apiFetch('/admin/backup/restore', {
    token,
    method: 'POST',
    body: payload,
  })
}
