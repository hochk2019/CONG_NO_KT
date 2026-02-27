import { act, renderHook } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import { usePersistedState } from '../usePersistedState'

describe('usePersistedState', () => {
  beforeEach(() => {
    window.localStorage.clear()
  })

  it('reads existing value from localStorage and persists updates', () => {
    window.localStorage.setItem('pref.theme', '"dark"')

    const { result } = renderHook(() => usePersistedState('pref.theme', 'light'))

    expect(result.current[0]).toBe('dark')

    act(() => {
      result.current[1]('light')
    })

    expect(window.localStorage.getItem('pref.theme')).toBe('"light"')
  })

  it('falls back to initial value when validation fails', () => {
    window.localStorage.setItem('pref.pageSize', '"bad-value"')

    const { result } = renderHook(() =>
      usePersistedState<number>('pref.pageSize', 20, {
        validate: (value): value is number => typeof value === 'number',
      }),
    )

    expect(result.current[0]).toBe(20)
  })
})
