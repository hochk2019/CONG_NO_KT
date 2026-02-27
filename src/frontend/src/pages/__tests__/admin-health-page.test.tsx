import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { vi } from 'vitest'
import { AuthContext, type AuthContextValue } from '../../context/AuthStore'
import AdminHealthPage from '../AdminHealthPage'

const mocks = vi.hoisted(() => ({
  fetchAdminHealth: vi.fn(),
  runAdminBalanceReconcile: vi.fn(),
}))

vi.mock('../../api/adminHealth', () => ({
  fetchAdminHealth: mocks.fetchAdminHealth,
  runAdminBalanceReconcile: mocks.runAdminBalanceReconcile,
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

const buildHealth = () => ({
  serverTimeUtc: new Date().toISOString(),
  tables: [],
  balanceDrift: {
    checkedCustomers: 2,
    driftedCustomers: 1,
    totalAbsoluteDrift: 10,
    maxAbsoluteDrift: 10,
    topDrifts: [],
  },
})

describe('AdminHealthPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.fetchAdminHealth.mockResolvedValue(buildHealth())
    mocks.runAdminBalanceReconcile.mockResolvedValue({
      executedAtUtc: new Date().toISOString(),
      checkedCustomers: 2,
      driftedCustomers: 1,
      updatedCustomers: 1,
      totalAbsoluteDrift: 10,
      maxAbsoluteDrift: 10,
      topDrifts: [],
    })
  })

  it('runs dry-run reconcile from action button', async () => {
    const authValue = buildAuthContext()

    render(
      <MemoryRouter>
        <AuthContext.Provider value={authValue}>
          <AdminHealthPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    const actionButton = await screen.findByRole('button', { name: 'Kiểm tra lệch số dư' })
    await userEvent.click(actionButton)

    await waitFor(() => {
      expect(mocks.runAdminBalanceReconcile).toHaveBeenCalledWith('token', {
        applyChanges: false,
        maxItems: 10,
        tolerance: 0.01,
      })
    })
  })
})
