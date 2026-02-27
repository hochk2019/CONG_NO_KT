import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { vi } from 'vitest'
import type { DashboardOverview } from '../../api/dashboard'
import { AuthContext, type AuthContextValue } from '../../context/AuthStore'
import DashboardPage from '../DashboardPage'

const mocks = vi.hoisted(() => ({
  fetchDashboardPreferences: vi.fn(),
  fetchDashboardOverview: vi.fn(),
  fetchDashboardOverdueGroups: vi.fn(),
  updateDashboardPreferences: vi.fn(),
}))

vi.mock('../../api/dashboard', () => ({
  fetchDashboardPreferences: mocks.fetchDashboardPreferences,
  fetchDashboardOverview: mocks.fetchDashboardOverview,
  fetchDashboardOverdueGroups: mocks.fetchDashboardOverdueGroups,
  updateDashboardPreferences: mocks.updateDashboardPreferences,
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

const buildOverview = (): DashboardOverview => ({
  trendFrom: '2025-02-03',
  trendTo: '2025-02-17',
  executiveSummary: {
    status: 'stable',
    message: 'Tình hình công nợ đang ổn định.',
    actionHint: 'Tiếp tục theo dõi các khoản đến hạn trong tuần.',
    generatedAt: new Date().toISOString(),
  },
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
  kpiMoM: {
    totalOutstanding: { current: 0, previous: 0, delta: 0, deltaPercent: null as number | null },
    outstandingInvoice: { current: 0, previous: 0, delta: 0, deltaPercent: null as number | null },
    outstandingAdvance: { current: 0, previous: 0, delta: 0, deltaPercent: null as number | null },
    overdueTotal: { current: 0, previous: 0, delta: 0, deltaPercent: null as number | null },
    unallocatedReceiptsAmount: { current: 0, previous: 0, delta: 0, deltaPercent: null as number | null },
    onTimeCustomers: { current: 0, previous: 0, delta: 0, deltaPercent: null as number | null },
  },
  trend: [
    {
      period: '2025-W06',
      invoicedTotal: 1_200_000,
      advancedTotal: 300_000,
      receiptedTotal: 900_000,
      expectedTotal: 1_500_000,
      actualTotal: 900_000,
      variance: -600_000,
    },
    {
      period: '2025-W07',
      invoicedTotal: 900_000,
      advancedTotal: 200_000,
      receiptedTotal: 1_100_000,
      expectedTotal: 1_100_000,
      actualTotal: 1_100_000,
      variance: 0,
    },
  ],
  cashflowForecast: [],
  topOutstanding: [],
  topOnTime: [],
  topOverdueDays: [],
  agingBuckets: [],
  allocationStatuses: [],
  lastUpdatedAt: new Date().toISOString(),
})

const defaultWidgetOrder = [
  'executiveSummary',
  'roleCockpit',
  'kpis',
  'cashflow',
  'panels',
]

describe('DashboardPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.fetchDashboardPreferences.mockResolvedValue({
      widgetOrder: defaultWidgetOrder,
      hiddenWidgets: [],
    })
    mocks.fetchDashboardOverview.mockResolvedValue(buildOverview())
    mocks.fetchDashboardOverdueGroups.mockResolvedValue([])
    mocks.updateDashboardPreferences.mockImplementation(async (_token, payload) => ({
      widgetOrder: payload.widgetOrder ?? defaultWidgetOrder,
      hiddenWidgets: payload.hiddenWidgets ?? [],
    }))
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
      expect(mocks.fetchDashboardOverview).toHaveBeenCalledTimes(1)
      expect(mocks.fetchDashboardOverdueGroups).toHaveBeenCalledTimes(1)
    })

    await userEvent.click(screen.getByRole('button', { name: 'Theo tháng' }))

    await waitFor(() => {
      const cashflowCall = mocks.fetchDashboardOverview.mock.calls
        .map((call) => call[0])
        .find((params) => params.trendGranularity === 'month')
      expect(cashflowCall).toBeTruthy()
      expect(cashflowCall?.trendPeriods).toBe(6)
      expect(mocks.fetchDashboardOverview).toHaveBeenCalledTimes(2)
      expect(mocks.fetchDashboardOverdueGroups).toHaveBeenCalledTimes(1)
    })

    expect(
      JSON.parse(window.localStorage.getItem('dashboard.cashflow.granularity') ?? 'null'),
    ).toBe('month')
  })

  it('renders executive summary and MoM badges', async () => {
    const authValue = buildAuthContext()
    const overview = buildOverview()
    overview.executiveSummary = {
      status: 'warning',
      message: 'Cần ưu tiên xử lý công nợ quá hạn trong tuần này.',
      actionHint: 'Ưu tiên gọi nhóm HIGH/CRITICAL trước thứ Sáu.',
      generatedAt: new Date('2026-02-24T08:30:00.000Z').toISOString(),
    }
    overview.kpiMoM = {
      totalOutstanding: {
        current: 2_500_000_000,
        previous: 2_800_000_000,
        delta: -300_000_000,
        deltaPercent: -10.7,
      },
      outstandingInvoice: {
        current: 1_900_000_000,
        previous: 1_800_000_000,
        delta: 100_000_000,
        deltaPercent: 5.6,
      },
      outstandingAdvance: { current: 600_000_000, previous: 600_000_000, delta: 0, deltaPercent: 0 },
      overdueTotal: {
        current: 350_000_000,
        previous: 420_000_000,
        delta: -70_000_000,
        deltaPercent: -16.7,
      },
      unallocatedReceiptsAmount: {
        current: 120_000_000,
        previous: 100_000_000,
        delta: 20_000_000,
        deltaPercent: 20,
      },
      onTimeCustomers: {
        current: 28,
        previous: 24,
        delta: 4,
        deltaPercent: 16.7,
      },
    }
    mocks.fetchDashboardOverview.mockResolvedValueOnce(overview)

    render(
      <MemoryRouter>
        <AuthContext.Provider value={authValue}>
          <DashboardPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    expect(await screen.findByText('Tóm tắt điều hành')).toBeInTheDocument()
    expect(
      screen.getByRole('heading', {
        name: 'Cần ưu tiên xử lý công nợ quá hạn trong tuần này.',
      }),
    ).toBeInTheDocument()
    expect(screen.getByText('Ưu tiên gọi nhóm HIGH/CRITICAL trước thứ Sáu.')).toBeInTheDocument()

    expect(screen.getByText(/Giảm\s+300\.000\.000.*so với tháng trước\./)).toBeInTheDocument()
    expect(screen.getByText(/Tăng\s+100\.000\.000.*so với tháng trước\./)).toBeInTheDocument()
    expect(screen.getByText('Không đổi so với tháng trước.')).toBeInTheDocument()
  })

  it('updates widget visibility preferences and hides section', async () => {
    const authValue = buildAuthContext()

    render(
      <MemoryRouter>
        <AuthContext.Provider value={authValue}>
          <DashboardPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    await userEvent.click(await screen.findByRole('button', { name: 'Tùy chỉnh Dashboard' }))
    expect(await screen.findByRole('dialog', { name: 'Tùy chỉnh Dashboard' })).toBeInTheDocument()
    expect(screen.getByText('Tổng dư công nợ')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('checkbox', { name: /Chỉ số KPI/i }))

    await waitFor(() => {
      expect(screen.queryByText('Tổng dư công nợ')).not.toBeInTheDocument()
    })

    await waitFor(() => {
      expect(mocks.updateDashboardPreferences).toHaveBeenCalledWith(
        'token',
        expect.objectContaining({
          hiddenWidgets: expect.arrayContaining(['kpis']),
        }),
      )
    })
  })

  it('renders role cockpit for director with priority cards', async () => {
    const authValue = buildAuthContext()
    const overview = buildOverview()
    overview.kpis.overdueTotal = 920_000_000
    overview.kpis.overdueCustomers = 6
    overview.kpis.unallocatedReceiptsAmount = 260_000_000
    overview.kpis.unallocatedReceiptsCount = 11
    overview.kpis.pendingReceiptsCount = 5
    overview.kpis.pendingImportBatches = 2
    overview.topOverdueDays = [
      {
        customerTaxCode: '0100999888',
        customerName: 'Cong ty A',
        amount: 410_000_000,
        daysPastDue: 18,
      },
    ]
    mocks.fetchDashboardOverview.mockResolvedValueOnce(overview)

    render(
      <MemoryRouter>
        <AuthContext.Provider value={authValue}>
          <DashboardPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    expect(await screen.findByRole('heading', { name: /Cockpit theo vai trò/i })).toBeInTheDocument()
    expect(screen.getByText(/Góc nhìn: Giám đốc/i)).toBeInTheDocument()
    expect(screen.getByText('Áp lực công nợ quá hạn')).toBeInTheDocument()
    expect(screen.getAllByText('Cong ty A').length).toBeGreaterThan(0)
  })
})
