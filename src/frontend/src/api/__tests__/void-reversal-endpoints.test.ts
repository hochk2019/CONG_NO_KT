import { beforeEach, describe, expect, it, vi } from 'vitest'
import { apiFetch } from '../client'
import { unvoidAdvance } from '../advances'
import { unvoidReceipt } from '../receipts'

vi.mock('../client', () => ({
  apiFetch: vi.fn(),
}))

describe('void reversal endpoints', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(apiFetch).mockResolvedValue({} as never)
  })

  it('calls advance unvoid endpoint with override payload', async () => {
    await unvoidAdvance('token-a', 'adv-123', {
      version: 7,
      overridePeriodLock: true,
      overrideReason: 'Mo khoa ky de khoi phuc',
    })

    expect(apiFetch).toHaveBeenCalledTimes(1)
    const [url, options] = vi.mocked(apiFetch).mock.calls[0]
    expect(url).toBe('/advances/adv-123/unvoid')
    expect(options).toEqual({
      method: 'POST',
      token: 'token-a',
      body: {
        version: 7,
        override_period_lock: true,
        override_reason: 'Mo khoa ky de khoi phuc',
      },
    })
  })

  it('calls receipt unvoid endpoint with default override values', async () => {
    await unvoidReceipt('token-r', 'rec-456', { version: 3 })

    expect(apiFetch).toHaveBeenCalledTimes(1)
    const [url, options] = vi.mocked(apiFetch).mock.calls[0]
    expect(url).toBe('/receipts/rec-456/unvoid')
    expect(options).toEqual({
      method: 'POST',
      token: 'token-r',
      body: {
        version: 3,
        override_period_lock: false,
        override_reason: null,
      },
    })
  })
})
