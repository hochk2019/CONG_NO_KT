import { describe, expect, it } from 'vitest'
import {
  computePrefetchBudget,
  createPrefetcher,
  readRouteHistory,
  recordRouteVisit,
  selectPrefetchTargets,
} from '../pageLoaders'

describe('page loaders', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('prefetches known routes once and ignores unknown routes', () => {
    const calls: string[] = []
    const loaders = {
      '/a': () => {
        calls.push('a')
        return Promise.resolve()
      },
      '/b': () => {
        calls.push('b')
        return Promise.resolve()
      },
    }

    const prefetch = createPrefetcher(loaders)
    prefetch('/a')
    prefetch('/a')
    prefetch('/b')
    prefetch('/c')

    expect(calls).toEqual(['a', 'b'])
  })

  it('selects affinity routes first when allowed', () => {
    const targets = selectPrefetchTargets({
      roles: ['Accountant'],
      allowedPaths: ['/dashboard', '/imports', '/customers', '/receipts', '/reports', '/risk'],
      currentPath: '/customers',
      max: 2,
    })

    expect(targets).toEqual(['/receipts', '/reports'])
  })

  it('falls back to role priority when no affinity match', () => {
    const targets = selectPrefetchTargets({
      roles: ['Viewer'],
      allowedPaths: ['/dashboard', '/customers', '/reports', '/risk', '/notifications'],
      currentPath: '/unknown',
      max: 2,
    })

    expect(targets).toEqual(['/dashboard', '/customers'])
  })

  it('uses history to prioritize recent routes', () => {
    const allowedPaths = ['/dashboard', '/imports', '/customers', '/receipts', '/reports', '/risk']
    recordRouteVisit('/dashboard', allowedPaths)
    recordRouteVisit('/imports', allowedPaths)
    recordRouteVisit('/imports', allowedPaths)

    const history = readRouteHistory()
    const targets = selectPrefetchTargets({
      roles: ['Accountant'],
      allowedPaths,
      currentPath: '/unknown',
      history,
      max: 2,
    })

    expect(targets[0]).toBe('/imports')
  })

  it('prioritizes admin routes when in admin area', () => {
    const targets = selectPrefetchTargets({
      roles: ['Admin'],
      allowedPaths: ['/dashboard', '/admin/users', '/admin/audit', '/admin/period-locks'],
      currentPath: '/admin/users',
      max: 2,
    })

    expect(targets[0]).toBe('/admin/period-locks')
  })

  it('includes deeper affinity targets on deep tier', () => {
    const targets = selectPrefetchTargets({
      roles: ['Accountant'],
      allowedPaths: ['/dashboard', '/imports', '/customers', '/receipts', '/reports'],
      currentPath: '/dashboard',
      max: 3,
      tier: 'deep',
    })

    expect(targets).toEqual(['/imports', '/receipts', '/customers'])
  })

  it('computes prefetch budget per role', () => {
    expect(computePrefetchBudget(['Admin'])).toBe(3)
    expect(computePrefetchBudget(['Viewer'])).toBe(1)
  })
})
