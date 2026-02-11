import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { vi } from 'vitest'
import { AuthContext, type AuthContextValue } from '../../context/AuthStore'
import AppShell from '../AppShell'

vi.mock('../../components/notifications/NotificationBell', () => ({
  default: () => <div data-testid="notification-bell" />,
}))

vi.mock('../../components/notifications/NotificationToastHost', () => ({
  default: () => <div data-testid="notification-toast-host" />,
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

describe('AppShell', () => {
  it('shows sidebar meta info', () => {
    const authValue = buildAuthContext()

    render(
      <MemoryRouter>
        <AuthContext.Provider value={authValue}>
          <AppShell />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    const versionLine = screen.getByText(/Phiên bản:\s*v1\.0/i)
    const designLine = screen.getByText('Design by Hoc HK')

    expect(versionLine.closest('.brand')).toBeTruthy()
    expect(designLine.closest('.brand')).toBeTruthy()
    expect(screen.queryByText(/Bản quyền/i)).not.toBeInTheDocument()
    expect(screen.queryByText('Debt Management Console')).not.toBeInTheDocument()
    expect(screen.getByText('Admin (Quản trị)')).toBeInTheDocument()
  })
})
