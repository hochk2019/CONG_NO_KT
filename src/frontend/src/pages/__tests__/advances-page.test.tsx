import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, useLocation } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AuthContext, type AuthContextValue } from '../../context/AuthStore'
import AdvancesPage from '../AdvancesPage'

const mocks = vi.hoisted(() => ({
  manualAdvancesSectionMock: vi.fn(),
}))

vi.mock('../imports/ManualAdvancesSection', () => ({
  default: (props: unknown) => {
    mocks.manualAdvancesSectionMock(props)
    return <div data-testid="manual-advances-section" />
  },
}))

const buildAuthContext = (): AuthContextValue => ({
  state: {
    accessToken: 'token',
    expiresAt: new Date(Date.now() + 60_000).toISOString(),
    username: 'accountant',
    roles: ['Accountant'],
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

function renderPage(initialEntry: string) {
  const authValue = buildAuthContext()
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <AuthContext.Provider value={authValue}>
        <LocationProbe />
        <AdvancesPage />
      </AuthContext.Provider>
    </MemoryRouter>,
  )
}

describe('AdvancesPage', () => {
  beforeEach(() => {
    mocks.manualAdvancesSectionMock.mockReset()
  })

  it('renders the redesigned hero and manual advances section without legacy tabs', async () => {
    renderPage('/advances')

    expect(
      screen.getByRole('heading', {
        level: 2,
        name: 'Workspace nhập liệu và xử lý khoản trả hộ KH',
      }),
    ).toBeInTheDocument()
    expect(screen.getByText('Khoản trả hộ KH')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Xem danh sách' })).toHaveAttribute(
      'href',
      '#advances-worklist',
    )

    expect(await screen.findByTestId('manual-advances-section')).toBeInTheDocument()
    expect(screen.queryByRole('tab')).not.toBeInTheDocument()
    expect(screen.getByTestId('location-probe').textContent).toBe('/advances')
  })

  it('navigates to centralized import page with ADVANCE type', async () => {
    const user = userEvent.setup()
    renderPage('/advances')

    await user.click(screen.getByRole('button', { name: 'Import từ template' }))

    await waitFor(() => {
      expect(screen.getByTestId('location-probe').textContent).toBe('/imports?tab=batch&type=ADVANCE')
    })
  })

  it('redirects legacy advances import tab query to centralized imports page', async () => {
    renderPage('/advances?tab=import')

    await waitFor(() => {
      expect(screen.getByTestId('location-probe').textContent).toBe('/imports?tab=batch&type=ADVANCE')
    })
  })
})
