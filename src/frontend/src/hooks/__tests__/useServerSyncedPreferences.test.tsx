import { act, renderHook, waitFor } from '@testing-library/react'
import { useState } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useServerSyncedPreferences } from '../useServerSyncedPreferences'

type Preferences = {
  kpiOrder: string[]
  dueSoonDays: number
}

const toLocal = (value: Preferences) => value

const isEqual = (first: Preferences | null, second: Preferences | null) => {
  if (!first || !second) return false
  if (first.dueSoonDays !== second.dueSoonDays) return false
  if (first.kpiOrder.length !== second.kpiOrder.length) return false
  return first.kpiOrder.every((item, index) => item === second.kpiOrder[index])
}

describe('useServerSyncedPreferences', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('loads preferences from server and marks as loaded', async () => {
    const fetchPreferences = vi.fn<(_: string) => Promise<Preferences>>().mockResolvedValue({
      kpiOrder: ['a'],
      dueSoonDays: 10,
    })
    const updatePreferences = vi
      .fn<(_: string, __: Preferences) => Promise<Preferences>>()
      .mockResolvedValue({
        kpiOrder: ['a'],
        dueSoonDays: 10,
      })

    const { result } = renderHook(() => {
      const [preferences, setPreferences] = useState<Preferences>({
        kpiOrder: ['default'],
        dueSoonDays: 7,
      })
      const syncState = useServerSyncedPreferences<Preferences, Preferences>({
        token: 'token',
        pendingPreferences: preferences,
        fetchPreferences,
        updatePreferences,
        toLocal,
        applyLocal: setPreferences,
        isEqual,
      })
      return { ...syncState, preferences }
    })

    await waitFor(() => {
      expect(result.current.preferencesLoaded).toBe(true)
    })

    expect(fetchPreferences).toHaveBeenCalledWith('token')
    expect(result.current.preferences).toEqual({ kpiOrder: ['a'], dueSoonDays: 10 })
    expect(updatePreferences).not.toHaveBeenCalled()
  })

  it('persists updated preferences and applies server response', async () => {
    const fetchPreferences = vi.fn<(_: string) => Promise<Preferences>>().mockResolvedValue({
      kpiOrder: ['a'],
      dueSoonDays: 7,
    })
    const updatePreferences = vi
      .fn<(_: string, payload: Preferences) => Promise<Preferences>>()
      .mockImplementation(async (_token, payload) => ({
        ...payload,
        dueSoonDays: payload.dueSoonDays + 1,
      }))

    const { result } = renderHook(() => {
      const [preferences, setPreferences] = useState<Preferences>({
        kpiOrder: ['default'],
        dueSoonDays: 7,
      })
      const syncState = useServerSyncedPreferences<Preferences, Preferences>({
        token: 'token',
        pendingPreferences: preferences,
        fetchPreferences,
        updatePreferences,
        toLocal,
        applyLocal: setPreferences,
        isEqual,
      })
      return { ...syncState, preferences, setPreferences }
    })

    await waitFor(() => {
      expect(result.current.preferencesLoaded).toBe(true)
    })

    act(() => {
      result.current.setPreferences({ kpiOrder: ['a'], dueSoonDays: 9 })
    })

    await waitFor(() => {
      expect(updatePreferences).toHaveBeenCalledWith('token', { kpiOrder: ['a'], dueSoonDays: 9 })
    })

    await waitFor(() => {
      expect(result.current.preferences.dueSoonDays).toBe(10)
    })
  })

  it('calls onPersistError when update fails', async () => {
    const fetchPreferences = vi.fn<(_: string) => Promise<Preferences>>().mockResolvedValue({
      kpiOrder: ['a'],
      dueSoonDays: 7,
    })
    const updatePreferences = vi
      .fn<(_: string, __: Preferences) => Promise<Preferences>>()
      .mockRejectedValue(new Error('network'))
    const onPersistError = vi.fn()

    const { result } = renderHook(() => {
      const [preferences, setPreferences] = useState<Preferences>({
        kpiOrder: ['default'],
        dueSoonDays: 7,
      })
      const syncState = useServerSyncedPreferences<Preferences, Preferences>({
        token: 'token',
        pendingPreferences: preferences,
        fetchPreferences,
        updatePreferences,
        toLocal,
        applyLocal: setPreferences,
        isEqual,
        onPersistError,
      })
      return { ...syncState, setPreferences }
    })

    await waitFor(() => {
      expect(result.current.preferencesLoaded).toBe(true)
    })

    act(() => {
      result.current.setPreferences({ kpiOrder: ['a'], dueSoonDays: 9 })
    })

    await waitFor(() => {
      expect(onPersistError).toHaveBeenCalledTimes(1)
    })
  })
})
