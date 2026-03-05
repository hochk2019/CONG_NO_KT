import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, apiFetch } from '../client'

describe('apiFetch error parsing', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('parses application/problem+json responses', async () => {
    const payload = {
      title: 'Bad Request',
      detail: 'Restore file not found.',
      extensions: { code: 'INVALID_OPERATION' },
    }

    const response = new Response(JSON.stringify(payload), {
      status: 400,
      statusText: 'Bad Request',
      headers: { 'Content-Type': 'application/problem+json; charset=utf-8' },
    })

    const fetchMock = vi.fn().mockResolvedValue(response)
    vi.stubGlobal('fetch', fetchMock)

    await expect(
      apiFetch('/admin/backup/restore', { method: 'POST', body: {} }),
    ).rejects.toMatchObject({
      message: 'Thao tác không hợp lệ. Restore file not found.',
      status: 400,
      detail: 'Restore file not found.',
      payload,
    } satisfies Partial<ApiError>)
  })
})
