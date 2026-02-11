import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { vi } from 'vitest'
import { AuthContext, type AuthContextValue } from '../../../context/AuthStore'
import { NotificationCenterProvider } from '../../../context/NotificationCenterContext'
import NotificationToastHost from '../NotificationToastHost'

vi.mock('../../../api/notifications', () => ({
  fetchNotifications: vi.fn().mockResolvedValue({
    items: [
      {
        id: 'n-alert',
        title: 'Hệ thống cảnh báo',
        body: 'Sao lưu dữ liệu thất bại.',
        severity: 'ALERT',
        source: 'SYSTEM',
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
    popupEnabled: true,
    popupSeverities: ['ALERT'],
    popupSources: ['SYSTEM'],
  }),
  markAllNotificationsRead: vi.fn().mockResolvedValue(undefined),
  markNotificationRead: vi.fn().mockResolvedValue(undefined),
  updateNotificationPreferences: vi.fn().mockResolvedValue({
    receiveNotifications: true,
    popupEnabled: true,
    popupSeverities: ['ALERT'],
    popupSources: ['SYSTEM'],
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

describe('NotificationToastHost', () => {
  it('shows critical modal for ALERT severity', async () => {
    localStorage.clear()
    const authValue = buildAuthContext()

    const { unmount } = render(
      <MemoryRouter>
        <AuthContext.Provider value={authValue}>
          <NotificationCenterProvider>
            <NotificationToastHost />
          </NotificationCenterProvider>
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    await waitFor(() => {
      expect(screen.getByText('Thông báo quan trọng cần được lưu ý.')).toBeInTheDocument()
    })

    unmount()
  })
})
