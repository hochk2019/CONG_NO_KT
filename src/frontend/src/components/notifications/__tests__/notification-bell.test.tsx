import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { vi } from 'vitest'
import { AuthContext, type AuthContextValue } from '../../../context/AuthStore'
import { NotificationCenterProvider } from '../../../context/NotificationCenterContext'
import NotificationBell from '../NotificationBell'

vi.mock('../../../api/notifications', () => ({
  fetchNotifications: vi.fn().mockResolvedValue({
    items: [
      {
        id: 'n-1',
        title: 'Nhắc duyệt phiếu thu',
        body: 'Phiếu thu đang chờ duyệt.',
        severity: 'WARN',
        source: 'RECEIPT',
        createdAt: new Date().toISOString(),
        readAt: null,
      },
    ],
    total: 1,
    page: 1,
    pageSize: 10,
  }),
  fetchNotificationPreferences: vi.fn().mockResolvedValue({
    receiveNotifications: true,
    popupEnabled: false,
    popupSeverities: ['WARN'],
    popupSources: ['RECEIPT'],
  }),
  markAllNotificationsRead: vi.fn().mockResolvedValue(undefined),
  markNotificationRead: vi.fn().mockResolvedValue(undefined),
  updateNotificationPreferences: vi.fn().mockResolvedValue({
    receiveNotifications: true,
    popupEnabled: false,
    popupSeverities: ['WARN'],
    popupSources: ['RECEIPT'],
  }),
}))

const buildAuthContext = (): AuthContextValue => ({
  state: {
    accessToken: 'token',
    expiresAt: new Date(Date.now() + 60_000).toISOString(),
    username: 'tester',
    roles: [],
  },
  isAuthenticated: true,
  isBootstrapping: false,
  login: vi.fn(),
  logout: vi.fn(),
})

describe('NotificationBell', () => {
  it('shows unread badge count', async () => {
    const authValue = buildAuthContext()

    render(
      <MemoryRouter>
        <AuthContext.Provider value={authValue}>
          <NotificationCenterProvider>
            <NotificationBell />
          </NotificationCenterProvider>
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(screen.getByText('1')).toBeInTheDocument()
    })
  })
})
