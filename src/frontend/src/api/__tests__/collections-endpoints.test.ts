import { beforeEach, describe, expect, it, vi } from 'vitest'
import { apiFetch } from '../client'
import {
  assignCollectionTask,
  generateCollectionTasks,
  listCollectionTasks,
  updateCollectionTaskStatus,
} from '../collections'

vi.mock('../client', () => ({
  apiFetch: vi.fn(),
}))

describe('collection endpoints', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(apiFetch).mockResolvedValue({} as never)
  })

  it('calls list endpoint with query filters', async () => {
    await listCollectionTasks({
      token: 'token-1',
      status: 'OPEN',
      assignedTo: 'ac5b528f-04c0-4ab0-9138-b5eb3fdba9e7',
      search: 'alpha',
      take: 120,
    })

    expect(apiFetch).toHaveBeenCalledTimes(1)
    const [url, options] = vi.mocked(apiFetch).mock.calls[0]
    expect(url).toContain('/collections/tasks?')
    expect(url).toContain('status=OPEN')
    expect(url).toContain('assignedTo=ac5b528f-04c0-4ab0-9138-b5eb3fdba9e7')
    expect(url).toContain('search=alpha')
    expect(url).toContain('take=120')
    expect(options).toEqual({ token: 'token-1' })
  })

  it('calls generate endpoint with snake_case payload', async () => {
    await generateCollectionTasks('token-2', {
      asOfDate: '2026-03-01',
      ownerId: 'bf4bcad4-062a-49d4-afc8-b28bb0706a4f',
      take: 30,
      minPriorityScore: 0.45,
    })

    expect(apiFetch).toHaveBeenCalledTimes(1)
    const [url, options] = vi.mocked(apiFetch).mock.calls[0]
    expect(url).toBe('/collections/tasks/generate')
    expect(options).toEqual({
      method: 'POST',
      token: 'token-2',
      body: {
        as_of_date: '2026-03-01',
        owner_id: 'bf4bcad4-062a-49d4-afc8-b28bb0706a4f',
        take: 30,
        min_priority_score: 0.45,
      },
    })
  })

  it('calls assign endpoint with nullable assigned_to', async () => {
    await assignCollectionTask('token-3', 'task-001', {})

    expect(apiFetch).toHaveBeenCalledTimes(1)
    const [url, options] = vi.mocked(apiFetch).mock.calls[0]
    expect(url).toBe('/collections/tasks/task-001/assign')
    expect(options).toEqual({
      method: 'POST',
      token: 'token-3',
      body: {
        assigned_to: null,
      },
    })
  })

  it('calls status update endpoint with note payload', async () => {
    await updateCollectionTaskStatus('token-4', 'task-002', {
      status: 'DONE',
      note: 'Da lien he va hen ngay thu',
    })

    expect(apiFetch).toHaveBeenCalledTimes(1)
    const [url, options] = vi.mocked(apiFetch).mock.calls[0]
    expect(url).toBe('/collections/tasks/task-002/status')
    expect(options).toEqual({
      method: 'POST',
      token: 'token-4',
      body: {
        status: 'DONE',
        note: 'Da lien he va hen ngay thu',
      },
    })
  })
})
