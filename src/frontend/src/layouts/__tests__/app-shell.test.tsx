import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { vi } from 'vitest'
import { AuthContext, type AuthContextValue } from '../../context/AuthStore'
import AppShell from '../AppShell'

const ONBOARDING_DISMISSED_STORAGE_KEY = 'pref.app.onboarding.dismissed.v1'

const { fetchGlobalSearchMock } = vi.hoisted(() => ({
  fetchGlobalSearchMock: vi.fn(),
}))

vi.mock('../../components/notifications/NotificationBell', () => ({
  default: () => <div data-testid="notification-bell" />,
}))

vi.mock('../../components/notifications/NotificationToastHost', () => ({
  default: () => <div data-testid="notification-toast-host" />,
}))

vi.mock('../../api/search', () => ({
  fetchGlobalSearch: fetchGlobalSearchMock,
}))

const buildAuthContext = (roles: string[] = ['Admin']): AuthContextValue => ({
  state: {
    accessToken: 'token',
    expiresAt: new Date(Date.now() + 60_000).toISOString(),
    username: 'tester',
    roles,
  },
  isAuthenticated: true,
  isBootstrapping: false,
  login: vi.fn(),
  logout: vi.fn(),
})

function LocationProbe() {
  const location = useLocation()
  return <div data-testid="location-probe">{`${location.pathname}${location.search}`}</div>
}

const renderInShellRoutes = (authValue: AuthContextValue, initialEntries: string[] = ['/dashboard']) =>
  render(
    <MemoryRouter initialEntries={initialEntries}>
      <AuthContext.Provider value={authValue}>
        <Routes>
          <Route element={<AppShell />}>
            <Route path="*" element={<LocationProbe />} />
          </Route>
        </Routes>
      </AuthContext.Provider>
    </MemoryRouter>,
  )

describe('AppShell', () => {
  beforeEach(() => {
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      value: vi.fn().mockImplementation(() => ({
        matches: false,
        media: '(prefers-color-scheme: dark)',
        onchange: null,
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        addListener: vi.fn(),
        removeListener: vi.fn(),
        dispatchEvent: vi.fn(),
      })),
    })
    window.localStorage.clear()
    window.localStorage.setItem(ONBOARDING_DISMISSED_STORAGE_KEY, '1')
    document.documentElement.removeAttribute('data-theme')
    fetchGlobalSearchMock.mockReset()
  })

  it('shows sidebar meta info', () => {
    const authValue = buildAuthContext()

    render(
      <MemoryRouter>
        <AuthContext.Provider value={authValue}>
          <AppShell />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    const versionLine = screen.getByText(/Phiên bản:\s*v1\.0/i)
    const designLine = screen.getByText('Design by Hoc HK')

    expect(versionLine.closest('.brand')).toBeTruthy()
    expect(designLine.closest('.brand')).toBeTruthy()
    expect(screen.queryByText(/Bản quyền/i)).not.toBeInTheDocument()
    expect(screen.queryByText('Debt Management Console')).not.toBeInTheDocument()
    expect(screen.getAllByText('Admin (Quản trị)').length).toBeGreaterThan(0)
    expect(screen.getByRole('heading', { name: /Trang|Tổng quan/i })).toBeInTheDocument()
    expect(screen.getByText('Điều hướng theo vai trò')).toBeInTheDocument()
    expect(screen.getByText('Vai trò chính')).toBeInTheDocument()
  })

  it('toggles mobile navigation state', async () => {
    const authValue = buildAuthContext()
    const user = userEvent.setup()

    const { container } = render(
      <MemoryRouter>
        <AuthContext.Provider value={authValue}>
          <AppShell />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    const shell = container.querySelector('.app-shell')
    expect(shell).toBeTruthy()
    expect(shell).not.toHaveClass('app-shell--nav-open')

    await user.click(screen.getByRole('button', { name: 'Mở menu điều hướng' }))
    expect(shell).toHaveClass('app-shell--nav-open')

    const backdrop = container.querySelector<HTMLButtonElement>('.mobile-nav-backdrop')
    expect(backdrop).toBeTruthy()
    await user.click(backdrop!)
    expect(shell).not.toHaveClass('app-shell--nav-open')
  })

  it('shows onboarding for first-time users and allows skipping', async () => {
    window.localStorage.removeItem(ONBOARDING_DISMISSED_STORAGE_KEY)
    const authValue = buildAuthContext()
    const user = userEvent.setup()

    render(
      <MemoryRouter>
        <AuthContext.Provider value={authValue}>
          <AppShell />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    expect(
      await screen.findByRole('dialog', { name: 'Điều hướng nhanh theo nghiệp vụ' }),
    ).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Bỏ qua' }))

    await waitFor(() => {
      expect(
        screen.queryByRole('dialog', { name: 'Điều hướng nhanh theo nghiệp vụ' }),
      ).not.toBeInTheDocument()
    })
    expect(window.localStorage.getItem(ONBOARDING_DISMISSED_STORAGE_KEY)).toBe('1')
  })

  it('reopens onboarding when clicking guide button', async () => {
    window.localStorage.setItem(ONBOARDING_DISMISSED_STORAGE_KEY, '1')
    const authValue = buildAuthContext()
    const user = userEvent.setup()

    render(
      <MemoryRouter>
        <AuthContext.Provider value={authValue}>
          <AppShell />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Hướng dẫn' }))

    expect(
      await screen.findByRole('dialog', { name: 'Điều hướng nhanh theo nghiệp vụ' }),
    ).toBeInTheDocument()
  })

  it('updates theme preference from header toggle', async () => {
    const authValue = buildAuthContext()
    const user = userEvent.setup()

    render(
      <MemoryRouter>
        <AuthContext.Provider value={authValue}>
          <AppShell />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    const themeSelect = screen.getByRole('combobox', { name: 'Chế độ giao diện' })
    await user.selectOptions(themeSelect, 'dark')

    expect(document.documentElement.getAttribute('data-theme')).toBe('dark')
    expect(window.localStorage.getItem('app.theme.preference')).toBe('"dark"')
  })

  it('opens quick search with hotkey and navigates on Enter', async () => {
    const authValue = buildAuthContext()
    const user = userEvent.setup()
    fetchGlobalSearchMock.mockResolvedValue({
      query: 'alpha',
      total: 1,
      customers: [],
      invoices: [
        {
          id: '7e778973-b3a2-4508-947b-f84b1f2d8a57',
          invoiceNo: 'INV-ALPHA-001',
          customerTaxCode: 'CUST001',
          customerName: 'Cong ty Alpha',
          issueDate: '2025-01-01',
          outstandingAmount: 1500000,
          status: 'OPEN',
        },
      ],
      receipts: [],
    })

    renderInShellRoutes(authValue)

    await user.keyboard('{Control>}k{/Control}')
    expect(await screen.findByRole('dialog', { name: 'Tìm kiếm nhanh' })).toBeInTheDocument()

    await user.type(screen.getByPlaceholderText('VD: MST, số hóa đơn, số phiếu thu...'), 'alpha')
    expect(await screen.findByText('INV-ALPHA-001')).toBeInTheDocument()
    const searchResults = screen.getByRole('listbox', { name: 'Kết quả tìm kiếm nhanh' })
    await waitFor(() => {
      expect(within(searchResults).getByRole('option', { selected: true })).toBeInTheDocument()
    })

    await user.keyboard('{Enter}')

    await waitFor(() => {
      expect(screen.getByTestId('location-probe').textContent).toContain(
        '/customers?taxCode=CUST001&tab=invoices&doc=INV-ALPHA-001',
      )
    })
  })

  it('does not render quick action navigation block', () => {
    const authValue = buildAuthContext(['Admin'])

    renderInShellRoutes(authValue)

    expect(screen.queryByText(/Đã mở gần đây/i)).not.toBeInTheDocument()
    expect(screen.queryByRole('navigation', { name: 'Tác vụ nhanh' })).not.toBeInTheDocument()
    expect(screen.queryByText(/Nhập lô dữ liệu/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/Ghi nhận thu tiền/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/Theo dõi khách hàng/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/Báo cáo điều hành/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/Quản trị người dùng/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/Nhật ký kiểm soát/i)).not.toBeInTheDocument()
  })
})
