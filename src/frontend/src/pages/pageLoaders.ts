type PageLoader = () => Promise<unknown>

type RouteHistory = {
  counts: Record<string, number>
  recent: string[]
}

type PrefetchTier = 'primary' | 'deep'

export const loadAdminAuditPage = () => import('./AdminAuditPage')
export const loadAdminBackupPage = () => import('./AdminBackupPage')
export const loadAdminErpIntegrationPage = () => import('./AdminErpIntegrationPage')
export const loadAdminHealthPage = () => import('./AdminHealthPage')
export const loadAdminPeriodLocksPage = () => import('./AdminPeriodLocksPage')
export const loadAdminUsersPage = () => import('./AdminUsersPage')
export const loadAdvancesPage = () => import('./AdvancesPage')
export const loadCustomersPage = () => import('./CustomersPage')
export const loadDashboardPage = () => import('./DashboardPage')
export const loadDashboardPreviewPage = () => import('./DashboardPreviewPage')
export const loadForbiddenPage = () => import('./ForbiddenPage')
export const loadImportsPage = () => import('./ImportsPage')
export const loadLoginPage = () => import('./LoginPage')
export const loadNotFoundPage = () => import('./NotFoundPage')
export const loadNotificationsPage = () => import('./NotificationsPage')
export const loadReceiptsPage = () => import('./ReceiptsPage')
export const loadReportsPage = () =>
  import('./ReportsPage').then((module) => ({ default: module.ReportsPage }))
export const loadRiskAlertsPage = () => import('./RiskAlertsPage')

const routeLoaders: Record<string, PageLoader> = {
  '/login': loadLoginPage,
  '/dashboard': loadDashboardPage,
  '/dashboard-preview': loadDashboardPreviewPage,
  '/notifications': loadNotificationsPage,
  '/imports': loadImportsPage,
  '/customers': loadCustomersPage,
  '/advances': loadAdvancesPage,
  '/receipts': loadReceiptsPage,
  '/reports': loadReportsPage,
  '/risk': loadRiskAlertsPage,
  '/admin/period-locks': loadAdminPeriodLocksPage,
  '/admin/users': loadAdminUsersPage,
  '/admin/audit': loadAdminAuditPage,
  '/admin/erp-integration': loadAdminErpIntegrationPage,
  '/admin/health': loadAdminHealthPage,
  '/admin/backup': loadAdminBackupPage,
  '/403': loadForbiddenPage,
  '*': loadNotFoundPage,
}

const HISTORY_KEY = 'nav.route.history.v1'
const MAX_RECENT = 8
const MAX_COUNTS = 20

const rolePriority = ['Admin', 'Supervisor', 'Accountant', 'Viewer']

const rolePrefetchPlan: Record<string, { primary: number; deep: number }> = {
  Admin: { primary: 3, deep: 2 },
  Supervisor: { primary: 3, deep: 1 },
  Accountant: { primary: 2, deep: 1 },
  Viewer: { primary: 1, deep: 0 },
}

const rolePreferredRoutes: Record<string, string[]> = {
  Admin: [
    '/dashboard',
    '/imports',
    '/customers',
    '/receipts',
    '/reports',
    '/risk',
  ],
  Supervisor: [
    '/dashboard',
    '/receipts',
    '/imports',
    '/customers',
    '/reports',
    '/risk',
  ],
  Accountant: ['/dashboard', '/imports', '/receipts', '/customers', '/reports', '/risk'],
  Viewer: ['/dashboard', '/customers', '/reports', '/risk', '/notifications'],
}

const roleAdminRoutes: Record<string, string[]> = {
  Admin: ['/admin/users', '/admin/period-locks', '/admin/audit', '/admin/health', '/admin/erp-integration', '/admin/backup'],
  Supervisor: ['/admin/period-locks', '/admin/audit', '/admin/health', '/admin/erp-integration', '/admin/backup'],
  Accountant: [],
  Viewer: [],
}

const roleSecondaryRoutes: Record<string, string[]> = {
  Admin: ['/notifications'],
  Supervisor: ['/notifications'],
  Accountant: ['/notifications'],
  Viewer: ['/notifications'],
}

const roleFlows: Record<string, string[]> = {
  Admin: [
    '/dashboard',
    '/imports',
    '/customers',
    '/receipts',
    '/reports',
    '/risk',
    '/notifications',
    '/admin/users',
    '/admin/period-locks',
    '/admin/audit',
    '/admin/health',
    '/admin/erp-integration',
    '/admin/backup',
  ],
  Supervisor: [
    '/dashboard',
    '/imports',
    '/customers',
    '/receipts',
    '/reports',
    '/risk',
    '/notifications',
    '/admin/period-locks',
    '/admin/audit',
    '/admin/health',
    '/admin/erp-integration',
    '/admin/backup',
  ],
  Accountant: [
    '/dashboard',
    '/imports',
    '/customers',
    '/receipts',
    '/reports',
    '/risk',
    '/notifications',
  ],
  Viewer: ['/dashboard', '/customers', '/reports', '/risk', '/notifications'],
}

const routeAffinity: Record<string, string[]> = {
  '/dashboard': ['/imports', '/receipts'],
  '/imports': ['/customers', '/receipts'],
  '/customers': ['/receipts', '/reports'],
  '/receipts': ['/customers', '/reports'],
  '/reports': ['/customers', '/dashboard'],
  '/risk': ['/customers', '/reports'],
  '/notifications': ['/dashboard', '/reports'],
  '/admin/users': ['/admin/period-locks', '/admin/audit'],
  '/admin/period-locks': ['/admin/audit', '/admin/users'],
  '/admin/audit': ['/admin/backup', '/admin/users'],
  '/admin/health': ['/admin/backup', '/admin/users'],
  '/admin/erp-integration': ['/admin/health', '/admin/backup'],
  '/admin/backup': ['/admin/audit', '/admin/users'],
}

const resolvePrimaryRole = (roles: string[]) => {
  return rolePriority.find((role) => roles.includes(role))
}

export const computePrefetchBudget = (roles: string[]) => {
  const primaryRole = resolvePrimaryRole(roles)
  if (!primaryRole) return 1
  return rolePrefetchPlan[primaryRole]?.primary ?? 1
}

export const computePrefetchPlan = (roles: string[]) => {
  const primaryRole = resolvePrimaryRole(roles)
  if (!primaryRole) return { primary: 1, deep: 0 }
  return rolePrefetchPlan[primaryRole] ?? { primary: 1, deep: 0 }
}

export const readRouteHistory = (): RouteHistory => {
  if (typeof window === 'undefined') return { counts: {}, recent: [] }
  try {
    const raw = window.localStorage.getItem(HISTORY_KEY)
    if (!raw) return { counts: {}, recent: [] }
    const parsed = JSON.parse(raw) as RouteHistory
    if (!parsed || typeof parsed !== 'object') return { counts: {}, recent: [] }
    return {
      counts: parsed.counts && typeof parsed.counts === 'object' ? parsed.counts : {},
      recent: Array.isArray(parsed.recent) ? parsed.recent : [],
    }
  } catch {
    return { counts: {}, recent: [] }
  }
}

const writeRouteHistory = (history: RouteHistory) => {
  if (typeof window === 'undefined') return
  window.localStorage.setItem(HISTORY_KEY, JSON.stringify(history))
}

export const recordRouteVisit = (path: string, allowedPaths: string[]) => {
  if (typeof window === 'undefined') return
  if (!allowedPaths.includes(path)) return
  const history = readRouteHistory()
  const counts = { ...history.counts }
  counts[path] = (counts[path] ?? 0) + 1

  const recent = [path, ...history.recent.filter((item) => item !== path)].slice(0, MAX_RECENT)

  const trimmedCounts: Record<string, number> = {}
  Object.entries(counts)
    .filter(([key]) => allowedPaths.includes(key))
    .sort((a, b) => b[1] - a[1])
    .slice(0, MAX_COUNTS)
    .forEach(([key, value]) => {
      trimmedCounts[key] = value
    })

  writeRouteHistory({ counts: trimmedCounts, recent })
}

export const selectPrefetchTargets = ({
  roles,
  allowedPaths,
  currentPath,
  history,
  max = 2,
  tier = 'primary',
}: {
  roles: string[]
  allowedPaths: string[]
  currentPath: string
  history?: RouteHistory
  max?: number
  tier?: PrefetchTier
}) => {
  if (max <= 0) return []
  const affinity = routeAffinity[currentPath] ?? []
  const secondOrderAffinity =
    tier === 'deep'
      ? affinity.flatMap((route) => routeAffinity[route] ?? []).filter((route) => route !== currentPath)
      : []
  const recent = history?.recent ?? []
  const frequent = history
    ? Object.entries(history.counts)
        .sort((a, b) => b[1] - a[1])
        .map(([path]) => path)
    : []
  const primaryRole = resolvePrimaryRole(roles)
  const preferred = primaryRole ? rolePreferredRoutes[primaryRole] ?? [] : []
  const secondary = primaryRole ? roleSecondaryRoutes[primaryRole] ?? [] : []
  const adminRoutes = primaryRole ? roleAdminRoutes[primaryRole] ?? [] : []
  const flow = primaryRole ? roleFlows[primaryRole] ?? [] : []
  const flowIndex = flow.indexOf(currentPath)
  const flowNext =
    flowIndex >= 0
      ? flow.slice(flowIndex + 1, flowIndex + 1 + (tier === 'deep' ? 3 : 2))
      : []
  const isAdminArea = currentPath.startsWith('/admin')
  const roleCandidates = isAdminArea
    ? [...adminRoutes, ...preferred, ...secondary]
    : tier === 'deep'
      ? [...preferred, ...secondary, ...adminRoutes]
      : [...preferred, ...secondary]

  const candidates = [
    ...affinity,
    ...flowNext,
    ...secondOrderAffinity,
    ...recent,
    ...frequent,
    ...roleCandidates,
    ...allowedPaths,
  ]

  const seen = new Set<string>()
  const results: string[] = []

  for (const path of candidates) {
    if (results.length >= max) break
    if (path === currentPath) continue
    if (!allowedPaths.includes(path)) continue
    if (seen.has(path)) continue
    seen.add(path)
    results.push(path)
  }

  return results
}

export const createPrefetcher = (loaders: Record<string, PageLoader>) => {
  const prefetched = new Set<string>()
  return (route: string) => {
    const loader = loaders[route]
    if (!loader || prefetched.has(route)) return
    prefetched.add(route)
    void loader()
  }
}

export const prefetchRoute = createPrefetcher(routeLoaders)
