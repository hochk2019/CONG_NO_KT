import { useCallback, useEffect, useMemo, useState } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import '../styles/layout-shell.css'
import { ApiError } from '../api/client'
import { changePassword } from '../api/auth'
import { useAuth } from '../context/AuthStore'
import NotificationBell from '../components/notifications/NotificationBell'
import NotificationToastHost from '../components/notifications/NotificationToastHost'
import GlobalSearchPalette from '../components/search/GlobalSearchPalette'
import { type ThemePreference, useTheme } from '../hooks/useTheme'
import { formatRoleDisplay } from '../utils/roles'
import { validatePasswordPolicy } from '../utils/passwordPolicy'
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

const isRiskCollectionItem = (item: NavItem) => item.to === '/risk' || item.to === '/collections'

const navItems: NavItem[] = [
  { label: 'Tổng quan', to: '/dashboard', roles: ['Admin', 'Supervisor', 'Accountant', 'Viewer'] },
  { label: 'Nhập liệu HĐ', to: '/imports', roles: ['Admin', 'Supervisor', 'Accountant'] },
  { label: 'Nhập liệu Trả hộ', to: '/advances', roles: ['Admin', 'Supervisor', 'Accountant'] },
  { label: 'Thu tiền', to: '/receipts', roles: ['Admin', 'Supervisor', 'Accountant'] },
  { label: 'Cảnh báo rủi ro', to: '/risk', roles: ['Admin', 'Supervisor', 'Accountant', 'Viewer'] },
  { label: 'Thu hồi nợ', to: '/collections', roles: ['Admin', 'Supervisor', 'Accountant'] },
  { label: 'Khách hàng', to: '/customers', roles: ['Admin', 'Supervisor', 'Accountant', 'Viewer'] },
  { label: 'Báo cáo chi tiết', to: '/reports', roles: ['Admin', 'Supervisor', 'Accountant', 'Viewer'] },
  { label: 'Người dùng', to: '/admin/users', roles: ['Admin'], kicker: 'Admin' },
  { label: 'Khóa kỳ', to: '/admin/period-locks', roles: ['Admin', 'Supervisor'], kicker: 'Admin' },
  { label: 'Nhật ký', to: '/admin/audit', roles: ['Admin', 'Supervisor'], kicker: 'Admin' },
  { label: 'Tình trạng dữ liệu', to: '/admin/health', roles: ['Admin', 'Supervisor'], kicker: 'Admin' },
  { label: 'Tích hợp ERP', to: '/admin/erp-integration', roles: ['Admin', 'Supervisor'], kicker: 'Admin' },
  { label: 'Sao lưu dữ liệu', to: '/admin/backup', roles: ['Admin', 'Supervisor'], kicker: 'Admin' },
]

const rolePriority = ['Admin', 'Supervisor', 'Accountant', 'Viewer']

const roleGuidance: Record<string, string> = {
  Admin: 'Theo dõi vận hành, phân quyền và rủi ro hệ thống.',
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
const NAV_COLLAPSED_STORAGE_KEY = 'pref.app.nav.collapsed.v1'
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

function ThemePreferenceIcon({ preference }: { preference: ThemePreference }) {
  if (preference === 'light') {
    return (
      <svg viewBox="0 0 24 24" width="14" height="14" aria-hidden="true">
        <circle cx="12" cy="12" r="4.2" fill="currentColor" />
        <path
          d="M12 2.8v2.2M12 19v2.2M21.2 12H19M5 12H2.8M18.7 5.3l-1.6 1.6M6.9 17.1l-1.6 1.6M18.7 18.7l-1.6-1.6M6.9 6.9L5.3 5.3"
          stroke="currentColor"
          strokeWidth="1.6"
          strokeLinecap="round"
        />
      </svg>
    )
  }

  if (preference === 'dark') {
    return (
      <svg viewBox="0 0 24 24" width="14" height="14" aria-hidden="true">
        <path
          d="M15.5 3.5a8.8 8.8 0 1 0 5 15.9 9.2 9.2 0 0 1-2.6.4 8.8 8.8 0 0 1-8.8-8.8c0-3 1.5-5.7 4-7.3a8.6 8.6 0 0 1 2.4-.2z"
          fill="currentColor"
        />
      </svg>
    )
  }

  return (
    <svg viewBox="0 0 24 24" width="14" height="14" aria-hidden="true">
      <rect x="3.5" y="5" width="17" height="12" rx="1.8" fill="none" stroke="currentColor" strokeWidth="1.7" />
      <path d="M9 19h6" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" />
    </svg>
  )
}

export default function AppShell() {
  const { state, logout } = useAuth()
  const { preference, setPreference } = useTheme()
  const location = useLocation()
  const navigate = useNavigate()
  const token = state.accessToken ?? ''
  const allowed = useMemo(() => navItems.filter((item) => isAllowed(item, state.roles)), [state.roles])
  const defaultNavItems = useMemo(() => allowed.filter((item) => !isRiskCollectionItem(item)), [allowed])
  const riskCollectionItems = useMemo(() => allowed.filter(isRiskCollectionItem), [allowed])
  const allowedPaths = useMemo(
    () => [...new Set([...allowed.map((item) => item.to), '/notifications'])],
    [allowed],
  )
  const currentPageTitle = resolveCurrentPageTitle(location.pathname)
  const primaryRole = useMemo(
    () => rolePriority.find((role) => state.roles.includes(role)),
    [state.roles],
  )
  const currentRoleGuidance =
    (primaryRole && roleGuidance[primaryRole]) ??
    'Theo dõi tiến độ công việc theo quy trình và ưu tiên các mục quá hạn.'
  const [navOpenPath, setNavOpenPath] = useState<string | null>(null)
  const [isSearchOpen, setIsSearchOpen] = useState(false)
  const [isNavCollapsed, setIsNavCollapsed] = useState(() => {
    if (typeof window === 'undefined') return false
    return window.localStorage.getItem(NAV_COLLAPSED_STORAGE_KEY) === '1'
  })
  const [isOnboardingOpen, setIsOnboardingOpen] = useState(() => {
    if (typeof window === 'undefined') return false
    return window.localStorage.getItem(ONBOARDING_DISMISSED_STORAGE_KEY) !== '1'
  })
  const [onboardingStepIndex, setOnboardingStepIndex] = useState(0)
  const [isChangePasswordOpen, setIsChangePasswordOpen] = useState(false)
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [changePasswordError, setChangePasswordError] = useState<string | null>(null)
  const [changePasswordSuccess, setChangePasswordSuccess] = useState<string | null>(null)
  const [isChangingPassword, setIsChangingPassword] = useState(false)
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

  const handleToggleNavCollapsed = useCallback(() => {
    setIsNavCollapsed((previous) => !previous)
  }, [])

  const closeChangePasswordModal = useCallback(() => {
    setIsChangePasswordOpen(false)
    setCurrentPassword('')
    setNewPassword('')
    setConfirmPassword('')
    setChangePasswordError(null)
    setChangePasswordSuccess(null)
    setIsChangingPassword(false)
  }, [])

  const openChangePasswordModal = useCallback(() => {
    setIsChangePasswordOpen(true)
    setChangePasswordError(null)
    setChangePasswordSuccess(null)
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

  useEffect(() => {
    if (typeof window === 'undefined') return
    window.localStorage.setItem(NAV_COLLAPSED_STORAGE_KEY, isNavCollapsed ? '1' : '0')
  }, [isNavCollapsed])

  const handleSubmitChangePassword = useCallback(async () => {
    if (!token) return

    setChangePasswordError(null)
    setChangePasswordSuccess(null)

    if (!currentPassword.trim()) {
      setChangePasswordError('Vui lòng nhập mật khẩu hiện tại.')
      return
    }

    const policyError = validatePasswordPolicy(newPassword)
    if (policyError) {
      setChangePasswordError(policyError)
      return
    }

    if (newPassword.trim() !== confirmPassword.trim()) {
      setChangePasswordError('Mật khẩu xác nhận không khớp.')
      return
    }

    setIsChangingPassword(true)
    try {
      await changePassword(token, {
        currentPassword: currentPassword.trim(),
        newPassword: newPassword.trim(),
      })
      setChangePasswordSuccess('Đổi mật khẩu thành công. Vui lòng đăng nhập lại.')
      setCurrentPassword('')
      setNewPassword('')
      setConfirmPassword('')
      setTimeout(() => logout(), 900)
    } catch (error) {
      if (error instanceof ApiError) {
        setChangePasswordError(error.message)
      } else {
        setChangePasswordError('Không thể đổi mật khẩu.')
      }
    } finally {
      setIsChangingPassword(false)
    }
  }, [confirmPassword, currentPassword, logout, newPassword, token])

  const renderNavItem = useCallback(
    (item: NavItem) => (
      <NavLink
        key={item.to}
        to={item.to}
        className={({ isActive }) => `nav-item${isActive ? ' nav-item--active' : ''}`}
        onMouseEnter={() => prefetchRoute(item.to)}
        onFocus={() => prefetchRoute(item.to)}
        onClick={() => setNavOpenPath(null)}
      >
        <span className="nav-item__icon" aria-hidden="true">
          {item.label.slice(0, 1)}
        </span>
        <span className="nav-item__label">{item.label}</span>
        {item.kicker && <span className="nav-pill">{item.kicker}</span>}
      </NavLink>
    ),
    [],
  )

  return (
    <div
      className={`app-shell${isNavOpen ? ' app-shell--nav-open' : ''}${
        isNavCollapsed ? ' app-shell--nav-collapsed' : ''
      }`}
    >
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
          <span className="brand__compact-mark" aria-hidden="true">
            CN
          </span>
          <div className="brand-meta">
            <span>Phiên bản: {APP_VERSION}</span>
            <span className="brand-meta__design">Design by Hoc HK</span>
          </div>
        </div>
        <nav className="nav-list">
          {defaultNavItems.map(renderNavItem)}
          {riskCollectionItems.length > 0 && (
            <div className="nav-group" aria-label="Risk and Collections">
              <p className="nav-group__title">Risk &amp; Collections</p>
              <div className="nav-group__items">{riskCollectionItems.map(renderNavItem)}</div>
            </div>
          )}
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
          <button
            type="button"
            className="btn btn-ghost nav-collapse-toggle"
            onClick={handleToggleNavCollapsed}
            title={isNavCollapsed ? 'Mở rộng menu' : 'Thu gọn menu'}
          >
            {isNavCollapsed ? '»' : '«'}
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
          <header className="app-context__summary app-header-card">
            <div className="app-header-toolbar">
              <div className="app-context__copy app-header-copy app-header-toolbar__copy">
                <p className="app-context__eyebrow">Điều hướng theo vai trò</p>
                <h1 className="app-context__title">{currentPageTitle}</h1>
                <p className="app-context__description">{currentRoleGuidance}</p>
              </div>
              <button
                type="button"
                className="btn btn-outline quick-search-trigger app-header-toolbar__search"
                aria-haspopup="dialog"
                aria-expanded={isSearchOpen}
                onClick={handleOpenSearch}
              >
                Tìm nhanh
                <span className="quick-search-trigger__hint">Ctrl/Cmd + K</span>
              </button>
              <div className="app-header-toolbar__actions">
                <NotificationBell />
                <button
                  type="button"
                  className="btn btn-ghost app-header__guide-btn"
                  onClick={handleReplayOnboarding}
                >
                  Hướng dẫn
                </button>
                <button
                  type="button"
                  className="btn btn-ghost app-header__guide-btn"
                  onClick={openChangePasswordModal}
                >
                  Đổi mật khẩu
                </button>
                <div className="theme-switch">
                  <span className="theme-switch__icon">
                    <ThemePreferenceIcon preference={preference} />
                  </span>
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
              </div>
            </div>
          </header>
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
      {isChangePasswordOpen && (
        <div className="modal-backdrop">
          <button
            type="button"
            className="modal-scrim"
            aria-label="Đóng hộp thoại"
            onClick={closeChangePasswordModal}
          />
          <div
            className="modal modal--narrow"
            role="dialog"
            aria-modal="true"
            aria-labelledby="change-password-title"
          >
            <div className="modal-header">
              <div>
                <h3 id="change-password-title">Đổi mật khẩu</h3>
                <p className="muted">Mật khẩu mới cần có tối thiểu 8 ký tự gồm hoa/thường/số.</p>
              </div>
              <button type="button" className="btn btn-ghost" onClick={closeChangePasswordModal} aria-label="Đóng">
                ✕
              </button>
            </div>
            <div className="modal-body form-stack">
              <label className="field">
                <span>Mật khẩu hiện tại</span>
                <input
                  type="password"
                  value={currentPassword}
                  onChange={(event) => setCurrentPassword(event.target.value)}
                  autoComplete="current-password"
                />
              </label>
              <label className="field">
                <span>Mật khẩu mới</span>
                <input
                  type="password"
                  value={newPassword}
                  onChange={(event) => setNewPassword(event.target.value)}
                  autoComplete="new-password"
                />
              </label>
              <label className="field">
                <span>Xác nhận mật khẩu mới</span>
                <input
                  type="password"
                  value={confirmPassword}
                  onChange={(event) => setConfirmPassword(event.target.value)}
                  autoComplete="new-password"
                />
              </label>
              {changePasswordError && (
                <div className="alert alert--error" role="alert" aria-live="assertive">
                  {changePasswordError}
                </div>
              )}
              {changePasswordSuccess && (
                <div className="alert alert--success" role="alert" aria-live="assertive">
                  {changePasswordSuccess}
                </div>
              )}
            </div>
            <div className="modal-footer modal-footer--end">
              <button
                type="button"
                className="btn btn-primary"
                onClick={handleSubmitChangePassword}
                disabled={isChangingPassword}
              >
                Cập nhật mật khẩu
              </button>
              <button type="button" className="btn btn-outline" onClick={closeChangePasswordModal}>
                Hủy
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
