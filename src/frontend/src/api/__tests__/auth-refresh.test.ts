import { describe, expect, it, vi } from 'vitest'
import type { LoginResponse } from '../auth'
import { refreshSession } from '../auth'
import { apiFetch } from '../client'

vi.mock('../client', () => ({
  apiFetch: vi.fn(),
}))

describe('refreshSession', () => {
  it('deduplicates concurrent refresh calls', async () => {
    let resolve: (value: LoginResponse) => void
    const promise = new Promise<LoginResponse>((res) => {
      resolve = res
    })
    vi.mocked(apiFetch).mockReturnValueOnce(promise)

    const first = refreshSession()
    const second = refreshSession()

    expect(apiFetch).toHaveBeenCalledTimes(1)

    resolve!({ accessToken: 'token', expiresAt: new Date().toISOString() })
    await Promise.all([first, second])
  })
})
