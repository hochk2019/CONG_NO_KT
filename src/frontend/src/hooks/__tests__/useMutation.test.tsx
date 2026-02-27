import { act, renderHook } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { useMutation } from '../useMutation'

describe('useMutation', () => {
  it('stores result when mutation succeeds', async () => {
    const onSuccess = vi.fn()
    const { result } = renderHook(() =>
      useMutation(async (value: number) => value * 2, { onSuccess }),
    )

    await act(async () => {
      const response = await result.current.mutate(21)
      expect(response).toBe(42)
    })

    expect(result.current.loading).toBe(false)
    expect(result.current.error).toBeNull()
    expect(result.current.data).toBe(42)
    expect(onSuccess).toHaveBeenCalledWith(42)
  })

  it('captures error when mutation fails', async () => {
    const expectedError = new Error('boom')
    const onError = vi.fn()
    const { result } = renderHook(() =>
      useMutation(async () => {
        throw expectedError
      }, { onError }),
    )

    await act(async () => {
      await expect(result.current.mutate()).rejects.toThrow('boom')
    })

    expect(result.current.loading).toBe(false)
    expect(result.current.data).toBeNull()
    expect(result.current.error).toBe(expectedError)
    expect(onError).toHaveBeenCalledWith(expectedError)
  })
})
