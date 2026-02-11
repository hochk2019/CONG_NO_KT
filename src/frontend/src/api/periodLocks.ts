import { apiFetch } from './client'

export type PeriodLockDto = {
  id: string
  periodType: string
  periodKey: string
  lockedAt: string
  lockedBy?: string | null
  note?: string | null
}

export const listPeriodLocks = async (token: string) => {
  return apiFetch<PeriodLockDto[]>('/period-locks', { token })
}

export const createPeriodLock = async (
  token: string,
  payload: { periodType: string; periodKey: string; note?: string },
) => {
  return apiFetch<PeriodLockDto>('/period-locks', {
    method: 'POST',
    token,
    body: payload,
  })
}

export const unlockPeriodLock = async (
  token: string,
  id: string,
  reason: string,
) => {
  return apiFetch<PeriodLockDto>(`/period-locks/${id}/unlock`, {
    method: 'POST',
    token,
    body: { reason },
  })
}
