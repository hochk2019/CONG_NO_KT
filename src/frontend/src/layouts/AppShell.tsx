import { useCallback, useEffect, useMemo, useState } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import '../styles/layout-shell.css'
import { useAuth } from '../context/AuthStore'
import NotificationBell from '../components/notifications/NotificationBell'
import NotificationToastHost from '../components/notifications/NotificationToastHost'
import GlobalSearchPalette from '../components/search/GlobalSearchPalette'
import { type ThemePreference, useTheme } from '../hooks/useTheme'
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
  { label: 'Tích hợp ERP', to: '/admin/erp-integration', roles: ['Admin', 'Supervisor'], kicker: 'Admin' },
  { label: 'Sao lưu dữ liệu', to: '/admin/backup', roles: ['Admin', 'Supervisor'], kicker: 'Admin' },
]

const rolePriority = ['Admin', 'Supervisor', 'Accountant', 'Viewer']

const roleGuidance: Record<string, string> = {
  Admin: 'Theo dõi toàn cảnh vận hành, phân quyền và kiểm soát rủi ro hệ thống.',
  Supervisor: 'Ưu tiên xử lý cảnh báo, khóa kỳ và giám sát chất lượng dữ liệu.',
  Accountant: 'Tập trung nhập liệu chính xác, thu tiền đúng hạn và đối chiếu báo cáo.',
  Viewer: 'Theo dõi KPI công nợ, cảnh báo quá hạn và biến động theo kỳ.',
}

const isAllowed = (item: { roles: string[] }, roles: string[]) => {
  return item.roles.some((role) => roles.includes(role))
}

const extraPageTitles: Record<string, string> = {
  '/notifications': 'Thông báo',
}

const resolveCurrentPageTitle = (pathname: string) => {
  const navMatch = navItems.find((item) => item.to === pathname)?.label
  if (navMatch) return navMatch
  return extraPageTitles[pathname] ?? 'Trang'
}

const APP_VERSION = 'v1.0'
const ONBOARDING_DISMISSED_STORAGE_KEY = 'pref.app.onboarding.dismissed.v1'
const themeOptions: Array<{ value: ThemePreference; label: string }> = [
  { value: 'light', label: 'Sáng' },
  { value: 'dark', label: 'Tối' },
  { value: 'system', label: 'Hệ thống' },
]
const onboardingSteps = [
  {
    title: 'Điều hướng nhanh theo nghiệp vụ',
    description: 'Menu trái tách theo nhóm: nhập liệu, thu tiền, báo cáo và quản trị.',
  },
  {
    title: 'Tìm kiếm toàn cục',
    description: 'Dùng nút "Tìm nhanh" hoặc phím tắt Ctrl/Cmd + K để truy cập chứng từ tức thì.',
  },
  {
    title: 'Theo dõi cảnh báo',
    description: 'Chuông thông báo giúp bạn xử lý công nợ quá hạn và rủi ro đúng thời điểm.',
  },
]

export default function AppShell() {
  const { state, logout } = useAuth()
  const { preference, setPreference } = useTheme()
  const location = useLocation()
  const navigate = useNavigate()
  const token = state.accessToken ?? ''
  const allowed = useMemo(() => navItems.filter((item) => isAllowed(item, state.roles)), [state.roles])
  const allowedPaths = useMemo(
    () => [...new Set([...allowed.map((item) => item.to), '/notifications'])],
    [allowed],
  )
  const currentPageTitle = resolveCurrentPageTitle(location.pathname)
  const primaryRole = useMemo(
    () => rolePriority.find((role) => state.roles.includes(role)),
    [state.roles],
  )
  const primaryRoleLabel = primaryRole ? formatRoleDisplay(primaryRole) : 'Chưa xác định'
  const currentRoleGuidance =
    (primaryRole && roleGuidance[primaryRole]) ??
    'Theo dõi tiến độ công việc theo quy trình và ưu tiên các mục quá hạn.'
  const [navOpenPath, setNavOpenPath] = useState<string | null>(null)
  const [isSearchOpen, setIsSearchOpen] = useState(false)
  const [isOnboardingOpen, setIsOnboardingOpen] = useState(() => {
    if (typeof window === 'undefined') return false
    return window.localStorage.getItem(ONBOARDING_DISMISSED_STORAGE_KEY) !== '1'
  })
  const [onboardingStepIndex, setOnboardingStepIndex] = useState(0)
  const isNavOpen = navOpenPath === location.pathname
  const onboardingStep = onboardingSteps[onboardingStepIndex]
  const isOnboardingLastStep = onboardingStepIndex >= onboardingSteps.length - 1

  const handleOpenSearch = useCallback(() => {
    setIsSearchOpen(true)
  }, [])

  const handleCloseSearch = useCallback(() => {
    setIsSearchOpen(false)
  }, [])

  const handleSearchNavigate = useCallback((to: string) => {
    navigate(to)
    setNavOpenPath(null)
  }, [navigate])

  const markOnboardingDismissed = useCallback(() => {
    if (typeof window === 'undefined') return
    window.localStorage.setItem(ONBOARDING_DISMISSED_STORAGE_KEY, '1')
  }, [])

  const handleReplayOnboarding = useCallback(() => {
    setOnboardingStepIndex(0)
    setIsOnboardingOpen(true)
  }, [])

  const handleOnboardingSkip = useCallback(() => {
    markOnboardingDismissed()
    setIsOnboardingOpen(false)
    setOnboardingStepIndex(0)
  }, [markOnboardingDismissed])

  const handleOnboardingNext = useCallback(() => {
    if (isOnboardingLastStep) {
      markOnboardingDismissed()
      setIsOnboardingOpen(false)
      setOnboardingStepIndex(0)
      return
    }

    setOnboardingStepIndex((value) => Math.min(onboardingSteps.length - 1, value + 1))
  }, [isOnboardingLastStep, markOnboardingDismissed])

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

  useEffect(() => {
    if (!isNavOpen) return

    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setNavOpenPath(null)
      }
    }

    window.addEventListener('keydown', handleEscape)
    return () => window.removeEventListener('keydown', handleEscape)
  }, [isNavOpen])

  useEffect(() => {
    const handleQuickSearchHotkey = (event: KeyboardEvent) => {
      if (!(event.ctrlKey || event.metaKey) || event.altKey || event.shiftKey) return
      if (event.key.toLowerCase() !== 'k') return

      event.preventDefault()
      setIsSearchOpen(true)
    }

    window.addEventListener('keydown', handleQuickSearchHotkey)
    return () => window.removeEventListener('keydown', handleQuickSearchHotkey)
  }, [])

  useEffect(() => {
    if (!isOnboardingOpen) return

    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        handleOnboardingSkip()
      }
    }

    window.addEventListener('keydown', handleEscape)
    return () => window.removeEventListener('keydown', handleEscape)
  }, [handleOnboardingSkip, isOnboardingOpen])

  return (
    <div className={`app-shell${isNavOpen ? ' app-shell--nav-open' : ''}`}>
      <button
        className="mobile-nav-backdrop"
        type="button"
        aria-label="Đóng menu điều hướng"
        onClick={() => setNavOpenPath(null)}
      />
      <aside id="app-nav" className="app-nav">
        <div className="app-nav__mobile-actions">
          <button
            type="button"
            className="btn btn-ghost mobile-nav-close"
            aria-label="Đóng menu điều hướng"
            onClick={() => setNavOpenPath(null)}
          >
            Đóng menu
          </button>
        </div>
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
              onClick={() => setNavOpenPath(null)}
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
        <div className="app-main__mobile-topbar">
          <button
            type="button"
            className="btn btn-ghost mobile-nav-toggle"
            aria-controls="app-nav"
            aria-expanded={isNavOpen}
            aria-label="Mở menu điều hướng"
            onClick={() => setNavOpenPath(location.pathname)}
          >
            Menu
          </button>
        </div>
        <div className="app-context">
          <div className="app-context__summary">
            <div className="app-context__copy">
              <p className="app-context__eyebrow">Điều hướng theo vai trò</p>
              <h1 className="app-context__title">{currentPageTitle}</h1>
              <p className="app-context__description">{currentRoleGuidance}</p>
            </div>
            <div className="app-context__controls">
              <div className="app-context__primary-actions">
                <button
                  type="button"
                  className="btn btn-outline quick-search-trigger"
                  aria-haspopup="dialog"
                  aria-expanded={isSearchOpen}
                  onClick={handleOpenSearch}
                >
                  Tìm nhanh
                  <span className="quick-search-trigger__hint">Ctrl/Cmd + K</span>
                </button>
                <div className="app-context__role">
                  <span>Vai trò chính</span>
                  <strong>{primaryRoleLabel}</strong>
                </div>
              </div>
              <div className="app-context__secondary-actions">
                <button
                  type="button"
                  className="btn btn-ghost app-header__guide-btn"
                  onClick={handleReplayOnboarding}
                >
                  Hướng dẫn
                </button>
                <div className="theme-switch">
                  <select
                    className="theme-switch__select"
                    aria-label="Chế độ giao diện"
                    value={preference}
                    onChange={(event) => setPreference(event.target.value as ThemePreference)}
                  >
                    {themeOptions.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                </div>
                <NotificationBell />
              </div>
            </div>
          </div>
        </div>
        <main className="app-content">
          <Outlet />
        </main>
      </div>
      <NotificationToastHost />
      <GlobalSearchPalette
        open={isSearchOpen}
        token={token}
        onClose={handleCloseSearch}
        onNavigate={handleSearchNavigate}
      />
      {isOnboardingOpen && (
        <div className="onboarding-backdrop">
          <section
            className="onboarding-dialog"
            role="dialog"
            aria-modal="true"
            aria-labelledby="onboarding-title"
          >
            <p className="onboarding-progress">
              Bước {onboardingStepIndex + 1}/{onboardingSteps.length}
            </p>
            <h3 id="onboarding-title">{onboardingStep.title}</h3>
            <p>{onboardingStep.description}</p>
            <div className="onboarding-actions">
              <button type="button" className="btn btn-ghost" onClick={handleOnboardingSkip}>
                Bỏ qua
              </button>
              <button type="button" className="btn btn-primary" onClick={handleOnboardingNext}>
                {isOnboardingLastStep ? 'Hoàn tất' : 'Tiếp tục'}
              </button>
            </div>
          </section>
        </div>
      )}
    </div>
  )
}
