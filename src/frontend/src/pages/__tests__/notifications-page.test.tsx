import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { vi } from 'vitest'
import { AuthContext, type AuthContextValue } from '../../context/AuthStore'
import NotificationsPage from '../NotificationsPage'

const mocks = vi.hoisted(() => ({
  fetchNotifications: vi.fn().mockResolvedValue({
    items: [
      {
        id: 'n-1',
        title: 'Thông báo test',
        body: 'Nội dung',
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
  markNotificationRead: vi.fn().mockResolvedValue(undefined),
  refreshUnread: vi.fn(),
  preferences: {
    receiveNotifications: true,
    popupEnabled: true,
    popupSeverities: ['WARN'],
    popupSources: ['RECEIPT'],
  },
  updatePreferences: vi.fn(),
}))

vi.mock('../../api/notifications', () => ({
  fetchNotifications: mocks.fetchNotifications,
  markNotificationRead: mocks.markNotificationRead,
}))

vi.mock('../../context/useNotificationCenter', () => ({
  useNotificationCenter: () => ({
    preferences: mocks.preferences,
    updatePreferences: mocks.updatePreferences,
    refreshUnread: mocks.refreshUnread,
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

describe('NotificationsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('refreshes unread count after marking read', async () => {
    const authValue = buildAuthContext()

    const { unmount } = render(
      <MemoryRouter>
        <AuthContext.Provider value={authValue}>
          <NotificationsPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    const titles = await screen.findAllByText('Thông báo test')
    expect(titles.length).toBeGreaterThan(0)

    await userEvent.click(screen.getByRole('button', { name: 'Đã đọc' }))

    await waitFor(() => {
      expect(mocks.markNotificationRead).toHaveBeenCalledWith('token', 'n-1')
      expect(mocks.refreshUnread).toHaveBeenCalled()
    })

    unmount()
  })

  it('marks notification read when clicking the row', async () => {
    const authValue = buildAuthContext()

    const { unmount } = render(
      <MemoryRouter>
        <AuthContext.Provider value={authValue}>
          <NotificationsPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    const rowButton = await screen.findByRole('button', { name: /Thông báo test/i })
    await userEvent.click(rowButton)

    await waitFor(() => {
      expect(mocks.markNotificationRead).toHaveBeenCalledWith('token', 'n-1')
    })

    unmount()
  })

  it('opens detail modal when clicking view detail', async () => {
    const authValue = buildAuthContext()

    const { unmount } = render(
      <MemoryRouter>
        <AuthContext.Provider value={authValue}>
          <NotificationsPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    const viewButton = await screen.findByRole('button', { name: 'Xem chi tiết' })
    await userEvent.click(viewButton)

    const dialog = await screen.findByRole('dialog')
    expect(dialog).toHaveTextContent('Thông báo test')
    expect(dialog).toHaveTextContent('Nội dung')

    unmount()
  })
})
