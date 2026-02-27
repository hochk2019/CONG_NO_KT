import { useEffect, useMemo, useState } from 'react'
import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { login, logoutSession, refreshSession } from '../api/auth'
import { ApiError } from '../api/client'
import { decodeJwt } from '../utils/jwt'
import { AuthContext, useAuth } from './AuthStore'
import type { AuthContextValue, AuthState } from './AuthStore'

const emptyState: AuthState = {
  accessToken: null,
  expiresAt: null,
  username: null,
  roles: [],
}

const AUTH_SESSION_STORAGE_KEY = 'cng.auth.session.v1'

const isExpired = (expiresAt: string | null) => {
  if (!expiresAt) {
    return true
  }
  return new Date(expiresAt).getTime() <= Date.now()
}

const readPersistedState = (): AuthState => {
  if (typeof window === 'undefined') {
    return emptyState
  }

  try {
    const raw = window.sessionStorage.getItem(AUTH_SESSION_STORAGE_KEY)
    if (!raw) {
      return emptyState
    }

    const parsed = JSON.parse(raw) as Partial<AuthState> | null
    if (!parsed || typeof parsed !== 'object') {
      return emptyState
    }

    const accessToken = typeof parsed.accessToken === 'string' ? parsed.accessToken : null
    const expiresAt = typeof parsed.expiresAt === 'string' ? parsed.expiresAt : null
    const username = typeof parsed.username === 'string' ? parsed.username : null
    const roles = Array.isArray(parsed.roles)
      ? parsed.roles.filter((role): role is string => typeof role === 'string')
      : []

    if (!accessToken || isExpired(expiresAt)) {
      return emptyState
    }

    return {
      accessToken,
      expiresAt,
      username,
      roles,
    }
  } catch {
    return emptyState
  }
}

const persistState = (state: AuthState) => {
  if (typeof window === 'undefined') {
    return
  }

  try {
    if (!state.accessToken || isExpired(state.expiresAt)) {
      window.sessionStorage.removeItem(AUTH_SESSION_STORAGE_KEY)
      return
    }

    window.sessionStorage.setItem(AUTH_SESSION_STORAGE_KEY, JSON.stringify(state))
  } catch {
    // Ignore storage failures.
  }
}

const clearPersistedState = () => {
  if (typeof window === 'undefined') {
    return
  }

  try {
    window.sessionStorage.removeItem(AUTH_SESSION_STORAGE_KEY)
  } catch {
    // Ignore storage failures.
  }
}

export const AuthProvider = ({ children }: { children: React.ReactNode }) => {
  const debugAuth = import.meta.env.DEV
  const restoredState = useMemo(() => readPersistedState(), [])
  const hasRestoredSession = useMemo(
    () => Boolean(restoredState.accessToken && !isExpired(restoredState.expiresAt)),
    [restoredState.accessToken, restoredState.expiresAt],
  )
  const [state, setState] = useState<AuthState>(() => restoredState)
  const [bootstrapping, setBootstrapping] = useState(!hasRestoredSession)

  const isAuthenticated = useMemo(() => {
    return Boolean(state.accessToken && !isExpired(state.expiresAt))
  }, [state.accessToken, state.expiresAt])

  const applySession = (accessToken: string, expiresAt: string, fallbackUsername?: string) => {
    const payload = decodeJwt(accessToken)
    const roles = payload.roles
    const nextState: AuthState = {
      accessToken,
      expiresAt,
      username: payload.username ?? fallbackUsername ?? null,
      roles,
    }
    setState(nextState)
  }

  const handleLogin = async (username: string, password: string) => {
    if (debugAuth) {
      console.info('[auth] login attempt', { username })
    }
    const result = await login(username, password)
    applySession(result.accessToken, result.expiresAt, username)
    setBootstrapping(false)
    if (debugAuth) {
      console.info('[auth] login success', { expiresAt: result.expiresAt })
    }
  }

  const handleLogout = () => {
    if (debugAuth) {
      console.info('[auth] logout')
    }
    void logoutSession()
    setState(emptyState)
    clearPersistedState()
  }

  useEffect(() => {
    let active = true

    const bootstrap = async () => {
      if (debugAuth) {
        console.info('[auth] bootstrap refresh start')
      }
      try {
        for (let attempt = 1; attempt <= 2; attempt += 1) {
          try {
            const result = await refreshSession()
            if (!active) return
            applySession(result.accessToken, result.expiresAt)
            if (debugAuth) {
              console.info('[auth] bootstrap refresh success', { expiresAt: result.expiresAt })
            }
            return
          } catch (err) {
            if (!active) return
            const isUnauthorized = err instanceof ApiError && err.status === 401
            if (isUnauthorized || attempt === 2) {
              if (debugAuth) {
                console.warn('[auth] bootstrap refresh failed', err)
              }
              if (!hasRestoredSession) {
                setState(emptyState)
              }
              return
            }
            if (debugAuth) {
              console.warn('[auth] bootstrap refresh retry', { attempt })
            }
            await new Promise((resolve) => {
              window.setTimeout(resolve, 500)
            })
          }
        }
      } finally {
        if (active) {
          setBootstrapping(false)
        }
      }
    }

    bootstrap()
    return () => {
      active = false
    }
  }, [debugAuth, hasRestoredSession])

  useEffect(() => {
    if (!state.expiresAt || !state.accessToken) {
      return
    }

    const expiresAt = new Date(state.expiresAt).getTime()
    const refreshAt = expiresAt - 60_000
    if (refreshAt <= Date.now()) {
      if (debugAuth) {
        console.info('[auth] refresh immediately', { expiresAt: state.expiresAt })
      }
      void refreshSession()
        .then((result) => applySession(result.accessToken, result.expiresAt))
        .catch((err) => {
          if (debugAuth) {
            console.warn('[auth] refresh failed', err)
          }
          if (err instanceof ApiError && err.status === 401) {
            setState(emptyState)
          }
        })
      return
    }

    if (debugAuth) {
      console.info('[auth] refresh scheduled', {
        refreshInMs: refreshAt - Date.now(),
        expiresAt: state.expiresAt,
      })
    }
    const timeoutId = window.setTimeout(() => {
      if (debugAuth) {
        console.info('[auth] refresh tick')
      }
      void refreshSession()
        .then((result) => applySession(result.accessToken, result.expiresAt))
        .catch((err) => {
          if (debugAuth) {
            console.warn('[auth] refresh failed', err)
          }
          if (err instanceof ApiError && err.status === 401) {
            setState(emptyState)
          }
        })
    }, refreshAt - Date.now())

    return () => {
      window.clearTimeout(timeoutId)
    }
  }, [debugAuth, state.accessToken, state.expiresAt])

  useEffect(() => {
    persistState(state)
  }, [state])

  const value: AuthContextValue = {
    state,
    isAuthenticated,
    isBootstrapping: bootstrapping,
    login: handleLogin,
    logout: handleLogout,
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export const RequireAuth = () => {
  const { isAuthenticated, isBootstrapping } = useAuth()
  const location = useLocation()

  if (isBootstrapping) {
    return (
      <div className="page-loading">
        <span className="muted">Đang khởi tạo phiên...</span>
      </div>
    )
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location }} />
  }

  return <Outlet />
}

export const RequireRole = ({
  roles,
  children,
}: {
  roles: string[]
  children?: React.ReactNode
}) => {
  const { state } = useAuth()
  const allowed = roles.length === 0 || roles.some((r) => state.roles.includes(r))

  if (!allowed) {
    return <Navigate to="/403" replace />
  }

  return <>{children ?? <Outlet />}</>
}
