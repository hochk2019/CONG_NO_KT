import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { vi } from 'vitest'
import { fetchBackupJobs, restoreBackup } from '../../../api/backup'
import { ApiError } from '../../../api/client'
import { AuthContext, type AuthContextValue } from '../../../context/AuthStore'
import AdminBackupPage from '../../AdminBackupPage'

vi.mock('../../../api/backup', () => ({
  fetchBackupSettings: vi.fn(async () => ({
    enabled: false,
    backupPath: 'C:\\\\apps\\\\congno\\\\backup\\\\dumps',
    retentionCount: 10,
    scheduleDayOfWeek: 1,
    scheduleTime: '02:00',
    timezone: 'UTC',
    pgBinPath: 'C:\\\\Program Files\\\\PostgreSQL\\\\16\\\\bin',
    lastRunAt: null,
  })),
  fetchBackupJobs: vi.fn(async () => ({
    items: [],
    page: 1,
    pageSize: 20,
    total: 0,
  })),
  fetchBackupAudit: vi.fn(async () => ({
    items: [],
    page: 1,
    pageSize: 20,
    total: 0,
  })),
  fetchBackupStatus: vi.fn(async () => ({ maintenance: false, message: null })),
  restoreBackup: vi.fn(async () => undefined),
}))

const baseAuth: AuthContextValue = {
  state: {
    accessToken: 'token',
    expiresAt: null,
    username: 'tester',
    roles: ['Admin'],
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
    vi.spyOn(window, 'prompt').mockReturnValue('RESTORE')

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
    vi.spyOn(window, 'prompt').mockReturnValue('RESTORE')

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
})
