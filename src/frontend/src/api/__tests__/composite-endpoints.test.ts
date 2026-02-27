import { beforeEach, describe, expect, it, vi } from 'vitest'
import { apiFetch } from '../client'
import { fetchReportOverview } from '../reports'
import { fetchRiskBootstrap } from '../risk'

vi.mock('../client', () => ({
  apiFetch: vi.fn(),
}))

describe('composite endpoints', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(apiFetch).mockResolvedValue({} as never)
  })

  it('calls reports overview endpoint with combined filters', async () => {
    await fetchReportOverview('token-1', {
      from: '2026-01-01',
      to: '2026-01-31',
      asOfDate: '2026-01-31',
      sellerTaxCode: '0312345678',
      customerTaxCode: '0101234567',
      ownerId: 'b4f35d54-12c6-4b87-8f96-2a54788e3ce5',
      dueSoonDays: 14,
      top: 10,
    })

    expect(apiFetch).toHaveBeenCalledTimes(1)
    const [url, options] = vi.mocked(apiFetch).mock.calls[0]
    expect(url).toContain('/reports/overview?')
    expect(url).toContain('from=2026-01-01')
    expect(url).toContain('to=2026-01-31')
    expect(url).toContain('asOfDate=2026-01-31')
    expect(url).toContain('sellerTaxCode=0312345678')
    expect(url).toContain('customerTaxCode=0101234567')
    expect(url).toContain('ownerId=b4f35d54-12c6-4b87-8f96-2a54788e3ce5')
    expect(url).toContain('dueSoonDays=14')
    expect(url).toContain('top=10')
    expect(options).toEqual({ token: 'token-1' })
  })

  it('calls risk bootstrap endpoint with initial payload', async () => {
    await fetchRiskBootstrap({
      token: 'token-2',
      search: 'alpha',
      ownerId: 'fdf376cf-7dc3-4d85-bc05-b233f08b3f58',
      level: 'HIGH',
      asOfDate: '2026-02-11',
      page: 2,
      pageSize: 25,
      sort: 'overdueAmount',
      order: 'desc',
      logChannel: 'ZALO',
      logStatus: 'SENT',
      logPage: 3,
      logPageSize: 50,
      notificationPage: 1,
      notificationPageSize: 5,
    })

    expect(apiFetch).toHaveBeenCalledTimes(1)
    const [url, options] = vi.mocked(apiFetch).mock.calls[0]
    expect(url).toContain('/risk/bootstrap?')
    expect(url).toContain('page=2')
    expect(url).toContain('pageSize=25')
    expect(url).toContain('logPage=3')
    expect(url).toContain('logPageSize=50')
    expect(url).toContain('search=alpha')
    expect(url).toContain('ownerId=fdf376cf-7dc3-4d85-bc05-b233f08b3f58')
    expect(url).toContain('level=HIGH')
    expect(url).toContain('asOfDate=2026-02-11')
    expect(url).toContain('sort=overdueAmount')
    expect(url).toContain('order=desc')
    expect(url).toContain('logChannel=ZALO')
    expect(url).toContain('logStatus=SENT')
    expect(url).toContain('notificationPage=1')
    expect(url).toContain('notificationPageSize=5')
    expect(options).toEqual({ token: 'token-2' })
  })
})
