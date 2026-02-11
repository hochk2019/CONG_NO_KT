import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { vi } from 'vitest'
import { AuthContext, type AuthContextValue } from '../../context/AuthStore'
import DashboardPage from '../DashboardPage'

const mocks = vi.hoisted(() => ({
  fetchDashboardOverview: vi.fn(),
  fetchDashboardOverdueGroups: vi.fn(),
}))

vi.mock('../../api/dashboard', () => ({
  fetchDashboardOverview: mocks.fetchDashboardOverview,
  fetchDashboardOverdueGroups: mocks.fetchDashboardOverdueGroups,
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

const buildOverview = () => ({
  trendFrom: '2025-02-03',
  trendTo: '2025-02-17',
  kpis: {
    totalOutstanding: 0,
    outstandingInvoice: 0,
    outstandingAdvance: 0,
    overdueTotal: 0,
    overdueCustomers: 0,
    onTimeCustomers: 0,
    unallocatedReceiptsAmount: 0,
    unallocatedReceiptsCount: 0,
    pendingReceiptsCount: 0,
    pendingReceiptsAmount: 0,
    pendingAdvancesCount: 0,
    pendingAdvancesAmount: 0,
    pendingImportBatches: 0,
    lockedPeriodsCount: 0,
  },
  trend: [
    { period: '2025-W06', invoicedTotal: 1_200_000, advancedTotal: 300_000, receiptedTotal: 900_000 },
    { period: '2025-W07', invoicedTotal: 900_000, advancedTotal: 200_000, receiptedTotal: 1_100_000 },
  ],
  topOutstanding: [],
  topOnTime: [],
  topOverdueDays: [],
  agingBuckets: [],
  allocationStatuses: [],
  lastUpdatedAt: new Date().toISOString(),
})

describe('DashboardPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.fetchDashboardOverview.mockResolvedValue(buildOverview())
    mocks.fetchDashboardOverdueGroups.mockResolvedValue([])
    window.localStorage.clear()
  })

  it('switches cashflow periods between week and month', async () => {
    const authValue = buildAuthContext()

    render(
      <MemoryRouter>
        <AuthContext.Provider value={authValue}>
          <DashboardPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    const weeklyButton = await screen.findByRole('button', { name: 'Theo tuần' })
    expect(weeklyButton).toBeInTheDocument()

    await waitFor(() => {
      const cashflowCall = mocks.fetchDashboardOverview.mock.calls
        .map((call) => call[0])
        .find((params) => params.trendGranularity === 'week')
      expect(cashflowCall).toBeTruthy()
      expect(cashflowCall?.trendPeriods).toBeGreaterThan(1)
    })

    await userEvent.click(screen.getByRole('button', { name: 'Theo tháng' }))

    await waitFor(() => {
      const cashflowCall = mocks.fetchDashboardOverview.mock.calls
        .map((call) => call[0])
        .find((params) => params.trendGranularity === 'month')
      expect(cashflowCall).toBeTruthy()
      expect(cashflowCall?.trendPeriods).toBe(6)
    })

    expect(window.localStorage.getItem('dashboard.cashflow.granularity')).toBe('month')
  })
})
