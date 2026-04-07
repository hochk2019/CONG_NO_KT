import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { vi } from 'vitest'
import { RequirePermission } from '../AuthContext'
import { AuthContext, type AuthContextValue } from '../AuthStore'

type AuthOverride = {
  roles?: string[]
  permissions?: string[]
}

const buildAuthContext = ({ roles = [], permissions = [] }: AuthOverride = {}): AuthContextValue => ({
  state: {
    accessToken: 'token',
    expiresAt: new Date(Date.now() + 60_000).toISOString(),
    username: 'guard-user',
    roles,
    permissions,
  },
  isAuthenticated: true,
  isBootstrapping: false,
  login: vi.fn(),
  logout: vi.fn(),
})

function renderGuard(authOverride?: AuthOverride) {
  return render(
    <MemoryRouter initialEntries={['/protected']}>
      <AuthContext.Provider value={buildAuthContext(authOverride)}>
        <Routes>
          <Route path="/403" element={<div>Forbidden</div>} />
          <Route element={<RequirePermission permissions={['admin.manage']} />}>
            <Route path="/protected" element={<div>Protected</div>} />
          </Route>
        </Routes>
      </AuthContext.Provider>
    </MemoryRouter>,
  )
}

describe('RequirePermission', () => {
  it('redirects to 403 when permission is missing', () => {
    renderGuard({ permissions: ['customer.view'] })

    expect(screen.getByText('Forbidden')).toBeInTheDocument()
    expect(screen.queryByText('Protected')).not.toBeInTheDocument()
  })

  it('renders child route when permission is present', () => {
    renderGuard({ permissions: ['admin.manage'] })

    expect(screen.getByText('Protected')).toBeInTheDocument()
    expect(screen.queryByText('Forbidden')).not.toBeInTheDocument()
  })
})
