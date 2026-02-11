import { createContext, useContext } from 'react'

export type AuthState = {
  accessToken: string | null
  expiresAt: string | null
  username: string | null
  roles: string[]
}

export type AuthContextValue = {
  state: AuthState
  isAuthenticated: boolean
  isBootstrapping: boolean
  login: (username: string, password: string) => Promise<void>
  logout: () => void
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined)

export const useAuth = () => {
  const ctx = useContext(AuthContext)
  if (!ctx) {
    throw new Error('AuthContext not available')
  }
  return ctx
}
