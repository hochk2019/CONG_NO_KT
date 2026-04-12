import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { vi } from 'vitest'
import { fetchBackupJobs, fetchBackupSettings, restoreBackup } from '../../../api/backup'
import { ApiError } from '../../../api/client'
import { AuthContext, type AuthContextValue } from '../../../context/AuthStore'
import AdminBackupPage from '../../AdminBackupPage'

vi.mock('../../../api/backup', () => ({
  completeBackupGoogleDriveCallback: vi.fn(async () => ({
    isConnected: true,
    provider: 'google_drive',
    googleDriveFolderId: 'folder-123',
    connectedAt: null,
    lastValidatedAt: null,
    lastError: null,
  })),
  disconnectBackupOffsiteConnection: vi.fn(async () => undefined),
  downloadBackupFile: vi.fn(async () => ({ blob: new Blob(), fileName: 'backup.dump' })),
  fetchBackupAudit: vi.fn(async () => ({
    items: [],
    page: 1,
    pageSize: 20,
    total: 0,
  })),
  fetchBackupJob: vi.fn(async () => ({
    id: 'job-1',
    type: 'manual',
    status: 'success',
    createdAt: new Date().toISOString(),
    fileName: 'backup.dump',
    fileSize: 1024,
    errorMessage: null,
    createdBy: null,
    stdoutLog: 'ok',
    stderrLog: '',
  })),
  fetchBackupJobs: vi.fn(async () => ({
    items: [],
    page: 1,
    pageSize: 20,
    total: 0,
  })),
  fetchBackupOffsiteUploads: vi.fn(async () => ({
    items: [],
    page: 1,
    pageSize: 10,
    total: 0,
  })),
  fetchBackupSettings: vi.fn(async () => ({
    enabled: false,
    backupPath: 'C:\\apps\\congno\\backup\\dumps',
    usesContainerPaths: false,
    hostBackupPath: 'C:\\apps\\congno\\backup\\dumps',
    hostBackupPathConfigKey: null,
    canEditBackupPath: true,
    canEditPgBinPath: true,
    retentionCount: 10,
    scheduleDayOfWeek: 1,
    scheduleTime: '02:00',
    timezone: 'UTC',
    pgBinPath: 'C:\\Program Files\\PostgreSQL\\16\\bin',
    offsiteEnabled: true,
    provider: 'google_drive',
    googleDriveFolderId: 'folder-123',
    offsiteRetentionCount: 5,
    uploadAfterBackup: true,
    offsiteConnection: {
      isConnected: true,
      provider: 'google_drive',
      googleDriveFolderId: 'folder-123',
      connectedAt: null,
      lastValidatedAt: null,
      lastError: null,
    },
    lastRunAt: null,
  })),
  fetchBackupStatus: vi.fn(async () => ({ maintenance: false, message: null })),
  issueBackupDownloadToken: vi.fn(async () => ({
    token: 'download-token',
    expiresAt: new Date().toISOString(),
  })),
  requestBackupGoogleDriveConnectUrl: vi.fn(async () => ({
    url: 'https://accounts.google.com/o/oauth2/auth',
  })),
  reuploadBackupJob: vi.fn(async () => ({
    id: 'upload-1',
    backupJobId: 'job-1',
    provider: 'google_drive',
    status: 'queued',
    remoteFileId: null,
    remoteChecksum: null,
    remoteFileSize: null,
    attemptCount: 0,
    errorMessage: null,
    createdAt: new Date().toISOString(),
    queuedAt: new Date().toISOString(),
    completedAt: null,
  })),
  restoreBackup: vi.fn(async () => undefined),
  runManualBackup: vi.fn(async () => ({
    id: 'job-run',
    type: 'manual',
    status: 'queued',
    createdAt: new Date().toISOString(),
    fileName: null,
    fileSize: null,
    errorMessage: null,
    createdBy: null,
  })),
  testBackupOffsiteUpload: vi.fn(async () => ({
    id: 'upload-test',
    backupJobId: null,
    provider: 'google_drive',
    status: 'queued',
    remoteFileId: null,
    remoteChecksum: null,
    remoteFileSize: null,
    attemptCount: 0,
    errorMessage: null,
    createdAt: new Date().toISOString(),
    queuedAt: new Date().toISOString(),
    completedAt: null,
  })),
  updateBackupSettings: vi.fn(async (_token: string, payload: unknown) => payload),
  uploadBackupFile: vi.fn(async () => ({
    uploadId: 'upload-1',
    fileName: 'manual.dump',
    fileSize: 1024,
    expiresAt: new Date().toISOString(),
  })),
}))

const baseAuth: AuthContextValue = {
  state: {
    accessToken: 'token',
    expiresAt: null,
    username: 'tester',
    roles: ['Admin'],
    permissions: [],
  },
  isAuthenticated: true,
  isBootstrapping: false,
  login: async () => undefined,
  logout: () => undefined,
}

describe('admin backup page', () => {
  it('renders backup header', async () => {
    render(
      <MemoryRouter>
        <AuthContext.Provider value={baseAuth}>
          <AdminBackupPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(screen.getByText('Sao lưu & phục hồi dữ liệu')).toBeInTheDocument()
    })
  })

  it('renders google drive offsite section', async () => {
    render(
      <MemoryRouter>
        <AuthContext.Provider value={baseAuth}>
          <AdminBackupPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(screen.getByText('Lưu trữ offsite')).toBeInTheDocument()
    })

    expect(screen.getByDisplayValue('folder-123')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Upload kiểm tra' })).toBeEnabled()
  })

  it('shows notice immediately when restore is triggered', async () => {
    const user = userEvent.setup()
    const pending = new Promise<void>(() => {})

    vi.mocked(fetchBackupJobs).mockResolvedValueOnce({
      items: [
        {
          id: '0c66d9d4-ee0f-47c1-be94-473310c3cc6e',
          type: 'manual',
          status: 'success',
          createdAt: new Date().toISOString(),
          fileName: 'backup.dump',
          fileSize: 1024,
          errorMessage: null,
          createdBy: null,
        },
      ],
      page: 1,
      pageSize: 20,
      total: 1,
    })
    vi.mocked(restoreBackup).mockReturnValueOnce(pending)

    render(
      <MemoryRouter>
        <AuthContext.Provider value={baseAuth}>
          <AdminBackupPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(screen.getByText('Phục hồi')).toBeInTheDocument()
    })

    await user.click(screen.getByRole('button', { name: 'Phục hồi' }))
    await screen.findByRole('dialog')
    await user.type(screen.getByLabelText('Mã xác nhận'), 'RESTORE')
    await user.click(screen.getByRole('button', { name: 'Phục hồi dữ liệu' }))

    expect(screen.getByText('Đang phục hồi dữ liệu. Vui lòng chờ.')).toBeInTheDocument()
  })

  it('clears restore notice on job restore failure', async () => {
    const user = userEvent.setup()

    vi.mocked(fetchBackupJobs).mockResolvedValueOnce({
      items: [
        {
          id: 'job-1',
          type: 'manual',
          status: 'success',
          createdAt: new Date().toISOString(),
          fileName: 'backup.dump',
          fileSize: 1024,
          errorMessage: null,
          createdBy: null,
        },
      ],
      page: 1,
      pageSize: 20,
      total: 1,
    })
    vi.mocked(restoreBackup).mockRejectedValueOnce(new ApiError('Restore failed', 400))

    render(
      <MemoryRouter>
        <AuthContext.Provider value={baseAuth}>
          <AdminBackupPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(screen.getByText('Phục hồi')).toBeInTheDocument()
    })

    await user.click(screen.getByRole('button', { name: 'Phục hồi' }))
    await screen.findByRole('dialog')
    await user.type(screen.getByLabelText('Mã xác nhận'), 'RESTORE')
    await user.click(screen.getByRole('button', { name: 'Phục hồi dữ liệu' }))

    await waitFor(() => {
      expect(screen.queryByText('Đang phục hồi dữ liệu. Vui lòng chờ.')).not.toBeInTheDocument()
    })
    expect(screen.getByText('Restore failed')).toBeInTheDocument()
  })

  it('renders skipped status label', async () => {
    vi.mocked(fetchBackupJobs).mockResolvedValueOnce({
      items: [
        {
          id: 'job-skip',
          type: 'scheduled',
          status: 'skipped',
          createdAt: new Date().toISOString(),
          fileName: null,
          fileSize: null,
          errorMessage: null,
          createdBy: null,
        },
      ],
      page: 1,
      pageSize: 20,
      total: 1,
    })

    render(
      <MemoryRouter>
        <AuthContext.Provider value={baseAuth}>
          <AdminBackupPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(screen.getByText('Bỏ qua')).toBeInTheDocument()
    })
  })

  it('shows host path guidance when backup runs inside docker', async () => {
    vi.mocked(fetchBackupSettings).mockResolvedValueOnce({
      enabled: true,
      backupPath: '/var/lib/congno/backups/dumps',
      usesContainerPaths: true,
      hostBackupPath: 'C:/Backup/CongNo',
      hostBackupPathConfigKey: 'BACKUP_HOST_PATH',
      canEditBackupPath: false,
      canEditPgBinPath: false,
      retentionCount: 10,
      scheduleDayOfWeek: 1,
      scheduleTime: '02:00',
      timezone: 'UTC',
      pgBinPath: '/usr/bin',
      offsiteEnabled: true,
      provider: 'google_drive',
      googleDriveFolderId: 'folder-123',
      offsiteRetentionCount: 5,
      uploadAfterBackup: true,
      offsiteConnection: {
        isConnected: true,
        provider: 'google_drive',
        googleDriveFolderId: 'folder-123',
        connectedAt: null,
        lastValidatedAt: null,
        lastError: null,
      },
      lastRunAt: null,
    })

    render(
      <MemoryRouter>
        <AuthContext.Provider value={baseAuth}>
          <AdminBackupPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(screen.getByText(/Hệ thống đang chạy trong Docker/i)).toBeInTheDocument()
    })

    expect(screen.getByDisplayValue('C:/Backup/CongNo')).toHaveAttribute('readonly')
    expect(screen.getByDisplayValue('/var/lib/congno/backups/dumps')).toHaveAttribute('readonly')
    expect(screen.getByDisplayValue('/usr/bin')).toHaveAttribute('readonly')
    expect(screen.getAllByText(/BACKUP_HOST_PATH/)).toHaveLength(2)
  })
})
