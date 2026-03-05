import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { vi } from 'vitest'
import type { NotificationItem } from '../../api/notifications'
import { AuthContext, type AuthContextValue } from '../../context/AuthStore'
import RiskAlertsPage from '../RiskAlertsPage'

const mocks = vi.hoisted(() => ({
  fetchRiskBootstrap: vi.fn(),
  fetchRiskOverview: vi.fn(),
  fetchRiskCustomers: vi.fn(),
  fetchRiskRules: vi.fn(),
  updateRiskRules: vi.fn(),
  fetchReminderLogs: vi.fn(),
  fetchReminderSettings: vi.fn(),
  runReminders: vi.fn(),
  updateReminderSettings: vi.fn(),
  fetchNotifications: vi.fn(),
  markNotificationRead: vi.fn(),
  markAllNotificationsRead: vi.fn(),
  fetchZaloLinkStatus: vi.fn(),
  requestZaloLinkCode: vi.fn(),
  fetchOwnerLookup: vi.fn(),
  mapOwnerOptions: vi.fn(),
}))

vi.mock('../../api/risk', () => ({
  fetchRiskBootstrap: mocks.fetchRiskBootstrap,
  fetchRiskOverview: mocks.fetchRiskOverview,
  fetchRiskCustomers: mocks.fetchRiskCustomers,
  fetchRiskRules: mocks.fetchRiskRules,
  updateRiskRules: mocks.updateRiskRules,
}))

vi.mock('../../api/reminders', () => ({
  fetchReminderLogs: mocks.fetchReminderLogs,
  fetchReminderSettings: mocks.fetchReminderSettings,
  runReminders: mocks.runReminders,
  updateReminderSettings: mocks.updateReminderSettings,
}))

vi.mock('../../api/notifications', () => ({
  fetchNotifications: mocks.fetchNotifications,
  markNotificationRead: mocks.markNotificationRead,
  markAllNotificationsRead: mocks.markAllNotificationsRead,
}))

vi.mock('../../api/zalo', () => ({
  fetchZaloLinkStatus: mocks.fetchZaloLinkStatus,
  requestZaloLinkCode: mocks.requestZaloLinkCode,
}))

vi.mock('../../api/lookups', () => ({
  fetchOwnerLookup: mocks.fetchOwnerLookup,
  mapOwnerOptions: mocks.mapOwnerOptions,
}))

const buildAuthContext = (): AuthContextValue => ({
  state: {
    accessToken: 'token',
    expiresAt: new Date(Date.now() + 60_000).toISOString(),
    username: 'supervisor',
    roles: ['Supervisor'],
  },
  isAuthenticated: true,
  isBootstrapping: false,
  login: vi.fn(),
  logout: vi.fn(),
})

const buildRiskBootstrap = () => ({
  overview: {
    asOfDate: '2026-02-24',
    items: [
      {
        level: 'HIGH',
        customers: 2,
        totalOutstanding: 500_000_000,
        overdueAmount: 320_000_000,
      },
    ],
    totalCustomers: 2,
    totalOutstanding: 500_000_000,
    totalOverdue: 320_000_000,
  },
  customers: {
    items: [],
    page: 1,
    pageSize: 10,
    total: 0,
  },
  rules: [
    {
      level: 'HIGH',
      matchMode: 'ANY' as const,
      minOverdueDays: 30,
      minOverdueRatio: 0.25,
      minLateCount: 2,
      isActive: true,
    },
  ],
  settings: {
    enabled: true,
    frequencyDays: 7,
    upcomingDueDays: 3,
    escalationMaxAttempts: 3,
    escalationCooldownHours: 24,
    escalateToSupervisorAfter: 2,
    escalateToAdminAfter: 3,
    channels: ['IN_APP'],
    targetLevels: ['HIGH'],
    lastRunAt: null,
    nextRunAt: null,
  },
  logs: {
    items: [],
    page: 1,
    pageSize: 10,
    total: 0,
  },
  notifications: {
    items: [] as NotificationItem[],
    page: 1,
    pageSize: 5,
    total: 0,
  },
  zaloStatus: {
    linked: false,
    zaloUserId: null,
    linkedAt: null,
  },
})

describe('RiskAlertsPage tabs', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    const bootstrap = buildRiskBootstrap()
    mocks.fetchRiskBootstrap.mockResolvedValue(bootstrap)
    mocks.fetchRiskOverview.mockResolvedValue(bootstrap.overview)
    mocks.fetchRiskCustomers.mockResolvedValue(bootstrap.customers)
    mocks.fetchRiskRules.mockResolvedValue(bootstrap.rules)
    mocks.updateRiskRules.mockResolvedValue(undefined)
    mocks.fetchReminderSettings.mockResolvedValue(bootstrap.settings)
    mocks.updateReminderSettings.mockResolvedValue(undefined)
    mocks.runReminders.mockResolvedValue({
      runAt: new Date().toISOString(),
      totalCandidates: 0,
      sentCount: 0,
      failedCount: 0,
      skippedCount: 0,
      dryRun: false,
      previewItems: [],
    })
    mocks.fetchReminderLogs.mockResolvedValue(bootstrap.logs)
    mocks.fetchNotifications.mockResolvedValue(bootstrap.notifications)
    mocks.markNotificationRead.mockResolvedValue(undefined)
    mocks.markAllNotificationsRead.mockResolvedValue(undefined)
    mocks.fetchZaloLinkStatus.mockResolvedValue(bootstrap.zaloStatus)
    mocks.requestZaloLinkCode.mockResolvedValue({
      code: 'ABC123',
      expiresAt: new Date(Date.now() + 10 * 60_000).toISOString(),
    })
    mocks.fetchOwnerLookup.mockResolvedValue([])
    mocks.mapOwnerOptions.mockReturnValue([])
    window.localStorage.clear()
  })

  it('switches between Tổng quan / Cấu hình / Lịch sử tabs', async () => {
    const authValue = buildAuthContext()

    render(
      <MemoryRouter>
        <AuthContext.Provider value={authValue}>
          <RiskAlertsPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    const overviewTab = await screen.findByRole('tab', { name: 'Tổng quan' })
    const configTab = screen.getByRole('tab', { name: 'Cấu hình' })
    const historyTab = screen.getByRole('tab', { name: 'Lịch sử' })

    expect(overviewTab).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByText('Tổng quan rủi ro')).toBeInTheDocument()
    expect(screen.getByText('Danh sách cảnh báo')).toBeInTheDocument()

    await userEvent.click(configTab)
    expect(configTab).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByText('Tiêu chí phân nhóm')).toBeInTheDocument()
    expect(screen.getByText('Thiết lập nhắc kế toán')).toBeInTheDocument()
    expect(screen.queryByText('Tổng quan rủi ro')).not.toBeInTheDocument()

    await userEvent.click(historyTab)
    expect(historyTab).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByText('Nhật ký nhắc')).toBeInTheDocument()
    expect(screen.getByText('Thông báo nội bộ')).toBeInTheDocument()
    expect(screen.queryByText('Tiêu chí phân nhóm')).not.toBeInTheDocument()
    expect(window.localStorage.getItem('pref.risk.activeTab')).toBe('history')
  })

  it('marks all notifications as read from history tab', async () => {
    const bootstrap = buildRiskBootstrap()
    bootstrap.notifications.items = [
      {
        id: 'n-1',
        title: 'Nhắc rủi ro',
        body: 'Theo dõi công nợ quá hạn',
        severity: 'WARN',
        source: 'RISK',
        createdAt: new Date().toISOString(),
        readAt: null,
      },
    ]
    bootstrap.notifications.total = 1
    mocks.fetchRiskBootstrap.mockResolvedValue(bootstrap)
    mocks.fetchNotifications.mockResolvedValue(bootstrap.notifications)

    const authValue = buildAuthContext()

    render(
      <MemoryRouter>
        <AuthContext.Provider value={authValue}>
          <RiskAlertsPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    await userEvent.click(await screen.findByRole('tab', { name: 'Lịch sử' }))
    await userEvent.click(await screen.findByRole('button', { name: 'Đã đọc tất cả (1)' }))

    await waitFor(() => {
      expect(mocks.markAllNotificationsRead).toHaveBeenCalledWith('token')
    })
  })
})
