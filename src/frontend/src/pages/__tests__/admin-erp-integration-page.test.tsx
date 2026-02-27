import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { vi } from 'vitest'
import { AuthContext, type AuthContextValue } from '../../context/AuthStore'
import AdminErpIntegrationPage from '../AdminErpIntegrationPage'

const mocks = vi.hoisted(() => ({
  fetchErpIntegrationConfig: vi.fn(),
  fetchErpIntegrationStatus: vi.fn(),
  updateErpIntegrationConfig: vi.fn(),
  syncErpSummary: vi.fn(),
}))

vi.mock('../../api/erpIntegration', () => ({
  fetchErpIntegrationConfig: mocks.fetchErpIntegrationConfig,
  fetchErpIntegrationStatus: mocks.fetchErpIntegrationStatus,
  updateErpIntegrationConfig: mocks.updateErpIntegrationConfig,
  syncErpSummary: mocks.syncErpSummary,
}))

const buildAuthContext = (): AuthContextValue => ({
  state: {
    accessToken: 'token',
    expiresAt: new Date(Date.now() + 60_000).toISOString(),
    username: 'tester',
    roles: ['Admin'],
  },
  isAuthenticated: true,
  isBootstrapping: false,
  login: vi.fn(),
  logout: vi.fn(),
})

const buildStatus = () => ({
  provider: 'MISA',
  enabled: false,
  configured: false,
  hasApiKey: false,
  baseUrl: null,
  companyCode: null,
  timeoutSeconds: 15,
  lastSyncAtUtc: null,
  lastSyncStatus: null,
  lastSyncMessage: null,
})

const buildConfig = () => ({
  enabled: false,
  provider: 'MISA',
  baseUrl: null,
  companyCode: null,
  timeoutSeconds: 15,
  hasApiKey: false,
  updatedAtUtc: null,
  updatedBy: null,
})

const buildSyncResult = () => ({
  success: true,
  status: 'dry_run',
  message: 'ok',
  executedAtUtc: new Date().toISOString(),
  provider: 'MISA',
  requestId: null,
  payload: {
    from: null,
    to: null,
    asOfDate: null,
    dueSoonDays: 7,
    totalOutstanding: 5000,
    outstandingInvoice: 4000,
    outstandingAdvance: 1000,
    unallocatedReceiptsAmount: 100,
    unallocatedReceiptsCount: 2,
    overdueAmount: 2500,
    overdueCustomers: 3,
    dueSoonAmount: 700,
    dueSoonCustomers: 2,
    onTimeCustomers: 5,
  },
})

describe('AdminErpIntegrationPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.fetchErpIntegrationConfig.mockResolvedValue(buildConfig())
    mocks.fetchErpIntegrationStatus.mockResolvedValue(buildStatus())
    mocks.updateErpIntegrationConfig.mockResolvedValue(buildConfig())
    mocks.syncErpSummary.mockResolvedValue(buildSyncResult())
  })

  it('triggers summary sync with default form values', async () => {
    const authValue = buildAuthContext()
    const user = userEvent.setup()

    render(
      <MemoryRouter>
        <AuthContext.Provider value={authValue}>
          <AdminErpIntegrationPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    const syncButton = await screen.findByRole('button', { name: 'Đồng bộ tổng hợp' })
    await user.click(syncButton)

    await waitFor(() => {
      expect(mocks.syncErpSummary).toHaveBeenCalledTimes(1)
    })

    expect(mocks.syncErpSummary).toHaveBeenCalledWith(
      'token',
      expect.objectContaining({
        dueSoonDays: 7,
        dryRun: false,
      }),
    )
  })

  it('saves ERP config from admin form', async () => {
    const authValue = buildAuthContext()
    const user = userEvent.setup()

    render(
      <MemoryRouter>
        <AuthContext.Provider value={authValue}>
          <AdminErpIntegrationPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    const saveButton = await screen.findByRole('button', { name: 'Lưu cấu hình' })
    expect(saveButton).toBeDisabled()

    const timeoutInput = screen.getByLabelText('Timeout (giây)')
    fireEvent.change(timeoutInput, { target: { value: '20' } })

    expect(saveButton).toBeEnabled()
    await user.click(saveButton)

    await waitFor(() => {
      expect(mocks.updateErpIntegrationConfig).toHaveBeenCalledTimes(1)
    })

    expect(mocks.updateErpIntegrationConfig).toHaveBeenCalledWith(
      'token',
      expect.objectContaining({
        enabled: false,
        provider: 'MISA',
        timeoutSeconds: 20,
        clearApiKey: false,
      }),
    )
  })

  it('validates required config fields on client when enabling integration', async () => {
    const authValue = buildAuthContext()
    const user = userEvent.setup()

    render(
      <MemoryRouter>
        <AuthContext.Provider value={authValue}>
          <AdminErpIntegrationPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    await screen.findByRole('button', { name: 'Lưu cấu hình' })

    await user.selectOptions(screen.getByLabelText('Bật tích hợp'), 'enabled')
    await user.click(screen.getByRole('button', { name: 'Lưu cấu hình' }))

    await waitFor(() => {
      expect(
        screen.getByText('Vui lòng kiểm tra lại thông tin cấu hình trước khi lưu.'),
      ).toBeInTheDocument()
    })

    expect(mocks.updateErpIntegrationConfig).not.toHaveBeenCalled()
  })
})
