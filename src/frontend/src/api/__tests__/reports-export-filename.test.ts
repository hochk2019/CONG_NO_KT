import { beforeEach, describe, expect, it, vi } from 'vitest'
import { apiFetchBlob } from '../client'
import { exportReport } from '../reports'

vi.mock('../client', () => ({
  apiFetch: vi.fn(),
  apiFetchBlob: vi.fn(),
}))

describe('report export filename parsing', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('prefers filename* when both filename and filename* are present', async () => {
    vi.mocked(apiFetchBlob).mockResolvedValue({
      blob: new Blob(['pdf-content'], { type: 'application/pdf' }),
      headers: new Headers({
        'content-disposition':
          "attachment; filename=legacy.pdf; filename*=UTF-8''CongNo_TongHop_2026-02-13.pdf",
      }),
    })

    const result = await exportReport(
      'token-1',
      { from: '2026-01-01', to: '2026-02-13' },
      'Summary',
      'Pdf',
    )

    expect(result.fileName).toBe('CongNo_TongHop_2026-02-13.pdf')
    expect(vi.mocked(apiFetchBlob)).toHaveBeenCalledWith(
      expect.stringContaining('/reports/export?'),
      { token: 'token-1' },
    )
  })

  it('decodes RFC5987 encoded filename* values', async () => {
    vi.mocked(apiFetchBlob).mockResolvedValue({
      blob: new Blob(['pdf-content'], { type: 'application/pdf' }),
      headers: new Headers({
        'content-disposition':
          "attachment; filename*=UTF-8''Bao%20cao%20tong%20hop%20thang%2002-2026.pdf",
      }),
    })

    const result = await exportReport(
      'token-2',
      { from: '2026-02-01', to: '2026-02-13' },
      'Summary',
      'Pdf',
    )

    expect(result.fileName).toBe('Bao cao tong hop thang 02-2026.pdf')
  })

  it('falls back to default filename when content-disposition is missing', async () => {
    vi.mocked(apiFetchBlob).mockResolvedValue({
      blob: new Blob(['pdf-content'], { type: 'application/pdf' }),
      headers: new Headers(),
    })

    const result = await exportReport(
      'token-3',
      { from: '2026-02-01', to: '2026-02-13' },
      'Summary',
      'Pdf',
    )

    expect(result.fileName).toBe('congno_report_export.pdf')
  })
})
