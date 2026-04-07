import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../client'
import { commitImport } from '../imports'

describe('imports api error parsing', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('parses application/problem+json responses when commit fails', async () => {
    const payload = {
      title: 'Bad Request',
      detail: 'Giá trị invoice_type vượt quá giới hạn lưu trữ.',
      extensions: { code: 'INVALID_OPERATION' },
    }

    const response = new Response(JSON.stringify(payload), {
      status: 400,
      statusText: 'Bad Request',
      headers: { 'Content-Type': 'application/problem+json; charset=utf-8' },
    })

    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response))

    await expect(
      commitImport({
        token: 'test-token',
        batchId: 'batch-001',
      }),
    ).rejects.toMatchObject({
      message: 'Thao tác không hợp lệ. Giá trị invoice_type vượt quá giới hạn lưu trữ.',
      status: 400,
    } satisfies Partial<ApiError>)
  })
})
