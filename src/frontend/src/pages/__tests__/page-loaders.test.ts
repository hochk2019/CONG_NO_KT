import { describe, expect, it } from 'vitest'
import {
  computePrefetchBudget,
  computePrefetchPlan,
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
      allowedPaths: ['/dashboard', '/invoices', '/customers', '/receipts', '/reports', '/risk'],
      currentPath: '/customers',
      max: 2,
    })

    expect(targets).toEqual(['/receipts', '/dashboard'])
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

  it('selects accountant affinity for permission-only users', () => {
    const targets = selectPrefetchTargets({
      roles: [],
      permissions: ['import.upload', 'customer.edit.owned', 'receipt.approve'],
      allowedPaths: ['/dashboard', '/invoices', '/customers', '/receipts', '/reports'],
      currentPath: '/customers',
      max: 2,
    } as Parameters<typeof selectPrefetchTargets>[0] & { permissions: string[] })

    expect(targets).toEqual(['/receipts', '/dashboard'])
  })

  it('falls back to permission-driven priority when no role affinity exists', () => {
    const targets = selectPrefetchTargets({
      roles: [],
      permissions: ['import.upload', 'customer.edit.owned', 'receipt.approve'],
      allowedPaths: ['/reports', '/dashboard', '/invoices', '/customers', '/receipts'],
      currentPath: '/unknown',
      max: 2,
    } as Parameters<typeof selectPrefetchTargets>[0] & { permissions: string[] })

    expect(targets).toEqual(['/dashboard', '/invoices'])
  })

  it('uses history to prioritize recent routes', () => {
    const allowedPaths = ['/dashboard', '/invoices', '/customers', '/receipts', '/reports', '/risk']
    recordRouteVisit('/dashboard', allowedPaths)
    recordRouteVisit('/invoices', allowedPaths)
    recordRouteVisit('/invoices', allowedPaths)

    const history = readRouteHistory()
    const targets = selectPrefetchTargets({
      roles: ['Accountant'],
      allowedPaths,
      currentPath: '/unknown',
      history,
      max: 2,
    })

    expect(targets[0]).toBe('/invoices')
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
      allowedPaths: ['/dashboard', '/invoices', '/customers', '/receipts', '/reports'],
      currentPath: '/dashboard',
      max: 3,
      tier: 'deep',
    })

    expect(targets).toEqual(['/invoices', '/receipts', '/customers'])
  })

  it('computes prefetch budget per role', () => {
    expect(computePrefetchBudget(['Admin'])).toBe(3)
    expect(computePrefetchBudget(['Viewer'])).toBe(1)
  })

  it('computes prefetch plan from permissions when no role is present', () => {
    const adminPlan = (
      computePrefetchPlan as (roles: string[], permissions: string[]) => { primary: number; deep: number }
    )([], ['admin.manage'])
    const viewerBudget = (computePrefetchBudget as (roles: string[], permissions: string[]) => number)(
      [],
      ['customer.view'],
    )

    expect(adminPlan).toEqual({ primary: 3, deep: 2 })
    expect(viewerBudget).toBe(1)
  })
})
