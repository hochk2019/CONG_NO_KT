import { apiFetch } from './client'

export type LoginResponse = {
  accessToken: string
  expiresAt: string
}

let refreshInFlight: Promise<LoginResponse> | null = null

export const login = async (username: string, password: string) => {
  return apiFetch<LoginResponse>('/auth/login', {
    method: 'POST',
    body: { username, password },
  })
}

export const refreshSession = async () => {
  if (refreshInFlight) {
    return refreshInFlight
  }

  refreshInFlight = apiFetch<LoginResponse>('/auth/refresh', {
    method: 'POST',
  }).finally(() => {
    refreshInFlight = null
  })

  return refreshInFlight
}

export const logoutSession = async () => {
  return apiFetch<void>('/auth/logout', {
    method: 'POST',
  })
}
