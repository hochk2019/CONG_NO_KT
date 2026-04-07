import { act, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../api/client'
import { AuthProvider } from '../AuthContext'
import { useAuth } from '../AuthStore'

const refreshSession = vi.fn()
const AUTH_SESSION_STORAGE_KEY = 'cng.auth.session.v1'

vi.mock('../../api/auth', () => ({
  login: vi.fn(),
  logoutSession: vi.fn(),
  refreshSession: (...args: unknown[]) => refreshSession(...args),
}))

const buildToken = (payload: Record<string, unknown>) => {
  const base64Url = (value: string) =>
    btoa(value).replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_')
  const header = base64Url(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))
  const body = base64Url(JSON.stringify(payload))
  return `${header}.${body}.sig`
}

const AuthStatus = () => {
  const { isAuthenticated, isBootstrapping, state } = useAuth()
  const stateWithPermissions = state as typeof state & { permissions?: string[] }
  const permissions = Array.isArray(stateWithPermissions.permissions) ? stateWithPermissions.permissions : []
  return (
    <>
      <div data-testid="status">
        {isAuthenticated ? `auth:${state.username}` : isBootstrapping ? 'boot' : 'guest'}
      </div>
      <div data-testid="permissions">{permissions.join(',')}</div>
    </>
  )
}

describe('AuthProvider bootstrap', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    refreshSession.mockReset()
    window.sessionStorage.clear()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('retries refresh once on transient error', async () => {
    const token = buildToken({
      'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name': 'admin',
      'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': ['Admin'],
    })
    refreshSession
      .mockRejectedValueOnce(new ApiError('Server error', 500))
      .mockResolvedValueOnce({ accessToken: token, expiresAt: new Date(Date.now() + 3600_000).toISOString() })

    render(
      <AuthProvider>
        <AuthStatus />
      </AuthProvider>,
    )

    await act(async () => {
      await vi.advanceTimersByTimeAsync(600)
    })

    await act(async () => {
      await Promise.resolve()
    })

    expect(screen.getByTestId('status')).toHaveTextContent('auth:admin')
  })

  it('decodes permission claims into auth state and persists them after bootstrap refresh', async () => {
    const token = buildToken({
      'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name': 'admin',
      'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': ['Admin'],
      permission: ['customer.view', 'import.commit.invoice'],
    })
    const expiresAt = new Date(Date.now() + 3600_000).toISOString()
    refreshSession.mockResolvedValueOnce({ accessToken: token, expiresAt })

    render(
      <AuthProvider>
        <AuthStatus />
      </AuthProvider>,
    )

    await act(async () => {
      await Promise.resolve()
    })

    expect(screen.getByTestId('status')).toHaveTextContent('auth:admin')
    expect(screen.getByTestId('permissions')).toHaveTextContent('customer.view,import.commit.invoice')

    const persistedState = JSON.parse(window.sessionStorage.getItem(AUTH_SESSION_STORAGE_KEY) ?? '{}') as {
      permissions?: string[]
    }
    expect(persistedState.permissions).toEqual(['customer.view', 'import.commit.invoice'])
  })

  it('keeps restored session on bootstrap 401 after reload', async () => {
    const token = buildToken({
      'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name': 'admin',
      'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': ['Admin'],
    })
    const expiresAt = new Date(Date.now() + 3600_000).toISOString()
    window.sessionStorage.setItem(
      AUTH_SESSION_STORAGE_KEY,
      JSON.stringify({
        accessToken: token,
        expiresAt,
        username: 'admin',
        roles: ['Admin'],
        permissions: ['customer.view', 'import.commit.invoice'],
      }),
    )
    refreshSession.mockRejectedValueOnce(new ApiError('Unauthorized', 401))

    render(
      <AuthProvider>
        <AuthStatus />
      </AuthProvider>,
    )

    await act(async () => {
      await Promise.resolve()
    })

    expect(screen.getByTestId('status')).toHaveTextContent('auth:admin')
    expect(screen.getByTestId('permissions')).toHaveTextContent('customer.view,import.commit.invoice')
  })

  it('falls back to guest when no restored session and bootstrap returns 401', async () => {
    refreshSession.mockRejectedValueOnce(new ApiError('Unauthorized', 401))

    render(
      <AuthProvider>
        <AuthStatus />
      </AuthProvider>,
    )

    await act(async () => {
      await Promise.resolve()
    })

    expect(screen.getByTestId('status')).toHaveTextContent('guest')
  })
})
