import { useEffect } from 'react'
import { Link, NavLink, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../context/AuthStore'
import NotificationBell from '../components/notifications/NotificationBell'
import NotificationToastHost from '../components/notifications/NotificationToastHost'
import { formatRoleDisplay } from '../utils/roles'
import {
  computePrefetchPlan,
  prefetchRoute,
  recordRouteVisit,
  readRouteHistory,
  selectPrefetchTargets,
} from '../pages/pageLoaders'

type NavItem = {
  label: string
  to: string
  roles: string[]
  kicker?: string
}

const navItems: NavItem[] = [
  { label: 'Tổng quan', to: '/dashboard', roles: ['Admin', 'Supervisor', 'Accountant', 'Viewer'] },
  { label: 'Nhập liệu', to: '/imports', roles: ['Admin', 'Supervisor', 'Accountant'] },
  { label: 'Khách hàng', to: '/customers', roles: ['Admin', 'Supervisor', 'Accountant', 'Viewer'] },
  { label: 'Thu tiền', to: '/receipts', roles: ['Admin', 'Supervisor', 'Accountant'] },
  { label: 'Báo cáo', to: '/reports', roles: ['Admin', 'Supervisor', 'Accountant', 'Viewer'] },
  { label: 'Cảnh báo rủi ro', to: '/risk', roles: ['Admin', 'Supervisor', 'Accountant', 'Viewer'] },
  { label: 'Người dùng', to: '/admin/users', roles: ['Admin'], kicker: 'Admin' },
  { label: 'Khóa kỳ', to: '/admin/period-locks', roles: ['Admin', 'Supervisor'], kicker: 'Admin' },
  { label: 'Nhật ký', to: '/admin/audit', roles: ['Admin', 'Supervisor'], kicker: 'Admin' },
  { label: 'Tình trạng dữ liệu', to: '/admin/health', roles: ['Admin', 'Supervisor'], kicker: 'Admin' },
  { label: 'Sao lưu dữ liệu', to: '/admin/backup', roles: ['Admin', 'Supervisor'], kicker: 'Admin' },
]

const isAllowed = (item: NavItem, roles: string[]) => {
  return item.roles.some((role) => roles.includes(role))
}

type Crumb = {
  label: string
  to?: string
}

const baseBreadcrumbs: Record<string, string> = {
  '/dashboard': 'Tổng quan',
  '/imports': 'Nhập liệu',
  '/customers': 'Khách hàng',
  '/receipts': 'Thu tiền',
  '/reports': 'Báo cáo',
  '/risk': 'Cảnh báo rủi ro',
  '/notifications': 'Thông báo',
}

const adminBreadcrumbs: Record<string, string> = {
  '/admin/users': 'Người dùng',
  '/admin/period-locks': 'Khóa kỳ',
  '/admin/audit': 'Nhật ký',
  '/admin/health': 'Tình trạng dữ liệu',
  '/admin/backup': 'Sao lưu dữ liệu',
}

const buildBreadcrumbs = (pathname: string): Crumb[] => {
  if (pathname.startsWith('/admin')) {
    const adminLabel = adminBreadcrumbs[pathname] ?? 'Admin'
    return [
      { label: 'Admin', to: '/admin/users' },
      { label: adminLabel },
    ]
  }
  const label = baseBreadcrumbs[pathname]
  return label ? [{ label }] : [{ label: 'Trang' }]
}

const APP_VERSION = 'v1.0'

export default function AppShell() {
  const { state, logout } = useAuth()
  const allowed = navItems.filter((item) => isAllowed(item, state.roles))
  const allowedPaths = [...new Set([...allowed.map((item) => item.to), '/notifications'])]
  const location = useLocation()
  const breadcrumbs = buildBreadcrumbs(location.pathname)

  useEffect(() => {
    if (typeof window === 'undefined') return

    const win = window as Window & typeof globalThis
    const connection = (navigator as Navigator & { connection?: { effectiveType?: string; saveData?: boolean } })
      .connection
    if (connection?.saveData) return
    if (connection?.effectiveType && ['slow-2g', '2g'].includes(connection.effectiveType)) return
    if (document.visibilityState === 'hidden') return

    const currentPath = location.pathname
    const history = readRouteHistory()
    const plan = computePrefetchPlan(state.roles)
    let primaryMax = plan.primary
    let deepMax = plan.deep
    if (connection?.effectiveType === '3g') {
      primaryMax = Math.min(primaryMax, 1)
      deepMax = 0
    }
    if (connection?.effectiveType && connection.effectiveType !== '4g') {
      deepMax = 0
    }

    const targets = selectPrefetchTargets({
      roles: state.roles,
      allowedPaths,
      currentPath,
      history,
      max: primaryMax,
    })

    const deepTargetsRaw =
      deepMax > 0
        ? selectPrefetchTargets({
            roles: state.roles,
            allowedPaths,
            currentPath,
            history,
            max: deepMax + targets.length,
            tier: 'deep',
          })
        : []
    const deepTargets = deepTargetsRaw.filter((target) => !targets.includes(target)).slice(0, deepMax)

    if (targets.length === 0 && deepTargets.length === 0) return

    const scheduleIdle = (action: () => void, idleTimeout: number, fallbackDelay: number) => {
      if (typeof win.requestIdleCallback === 'function') {
        const handle = win.requestIdleCallback(action, { timeout: idleTimeout })
        return () => win.cancelIdleCallback?.(handle)
      }
      const timeoutId = win.setTimeout(action, fallbackDelay)
      return () => win.clearTimeout(timeoutId)
    }

    const cleanups: Array<() => void> = []

    if (targets.length > 0) {
      cleanups.push(scheduleIdle(() => targets.forEach((route) => prefetchRoute(route)), 2000, 800))
    }

    if (deepTargets.length > 0) {
      cleanups.push(
        scheduleIdle(() => deepTargets.forEach((route) => prefetchRoute(route)), 4000, 1600),
      )
    }

    return () => cleanups.forEach((cleanup) => cleanup())
  }, [allowedPaths, location.pathname, state.roles])

  useEffect(() => {
    recordRouteVisit(location.pathname, allowedPaths)
  }, [allowedPaths, location.pathname])

  return (
    <div className="app-shell">
      <aside className="app-nav">
        <div className="brand">
          <span className="brand__kicker">Golden Logistics</span>
          <span className="brand__title">Quản lý công nợ</span>
          <div className="brand-meta">
            <span>Phiên bản: {APP_VERSION}</span>
            <span className="brand-meta__design">Design by Hoc HK</span>
          </div>
        </div>
        <nav className="nav-list">
          {allowed.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) => `nav-item${isActive ? ' nav-item--active' : ''}`}
              onMouseEnter={() => prefetchRoute(item.to)}
              onFocus={() => prefetchRoute(item.to)}
            >
              <span>{item.label}</span>
              {item.kicker && <span className="nav-pill">{item.kicker}</span>}
            </NavLink>
          ))}
        </nav>
        <div className="nav-footer">
          <div className="user-chip">
            <span className="user-chip__name">{state.username ?? 'Không rõ'}</span>
            <span className="user-chip__role">
              {state.roles.length > 0 ? state.roles.map((role) => formatRoleDisplay(role)).join(', ') : '-'}
            </span>
          </div>
          <button className="btn btn-ghost" onClick={logout}>
            Đăng xuất
          </button>
        </div>
      </aside>
      <div className="app-main">
        <div className="app-context">
          <div className="app-header">
            <nav className="breadcrumbs" aria-label="Breadcrumb">
              {breadcrumbs.map((crumb, index) => {
                const isLast = index === breadcrumbs.length - 1
                return (
                  <span key={`${crumb.label}-${index}`}>
                    {crumb.to && !isLast ? (
                      <Link to={crumb.to}>{crumb.label}</Link>
                    ) : (
                      <span aria-current={isLast ? 'page' : undefined}>{crumb.label}</span>
                    )}
                    {!isLast && <span className="breadcrumbs__sep">/</span>}
                  </span>
                )
              })}
            </nav>
            <div className="app-header__actions">
              <NotificationBell />
            </div>
          </div>
        </div>
        <main className="app-content">
          <Outlet />
        </main>
      </div>
      <NotificationToastHost />
    </div>
  )
}
