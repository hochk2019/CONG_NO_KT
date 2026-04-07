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
    const typedProps = props as { onImportTemplate?: () => void }
    return (
      <div data-testid="manual-advances-section">
        {typeof typedProps.onImportTemplate === 'function' ? (
          <button type="button" onClick={typedProps.onImportTemplate}>
            Import từ template
          </button>
        ) : null}
      </div>
    )
  },
}))

const buildAuthContext = (
  stateOverrides: Partial<AuthContextValue['state']> = {},
): AuthContextValue => ({
  state: {
    accessToken: 'token',
    expiresAt: new Date(Date.now() + 60_000).toISOString(),
    username: 'accountant',
    roles: ['Accountant'],
    permissions: [],
    ...stateOverrides,
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

function renderPage(
  initialEntry: string,
  stateOverrides: Partial<AuthContextValue['state']> = {},
) {
  const authValue = buildAuthContext(stateOverrides)
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

  it('renders the manual advances section without the workspace summary hero', async () => {
    renderPage('/advances')

    expect(await screen.findByTestId('manual-advances-section')).toBeInTheDocument()
    expect(
      screen.queryByRole('heading', {
        level: 2,
        name: 'Workspace nhập liệu và xử lý khoản trả hộ KH',
      }),
    ).not.toBeInTheDocument()
    expect(screen.queryByText('Khoản trả hộ KH')).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Xem danh sách' })).not.toBeInTheDocument()
    expect(screen.queryByRole('tab')).not.toBeInTheDocument()
    expect(screen.getByTestId('location-probe').textContent).toBe('/advances')
  })

  it('routes the import template CTA from the manual advances header', async () => {
    const user = userEvent.setup()
    renderPage('/advances')

    const importButton = await screen.findByRole('button', { name: 'Import từ template' })
    await user.click(importButton)

    expect(mocks.manualAdvancesSectionMock).toHaveBeenLastCalledWith(
      expect.objectContaining({
        onImportTemplate: expect.any(Function),
      }),
    )
    expect(screen.getByTestId('location-probe').textContent).toBe('/imports?tab=batch&type=ADVANCE')
  })

  it('redirects legacy advances import tab query to centralized imports page', async () => {
    renderPage('/advances?tab=import')

    await waitFor(() => {
      expect(screen.getByTestId('location-probe').textContent).toBe('/imports?tab=batch&type=ADVANCE')
    })
  })

  it('allows manual approval when advance.manage is granted', async () => {
    renderPage('/advances', { permissions: ['advance.manage'] })

    expect(await screen.findByTestId('manual-advances-section')).toBeInTheDocument()
    expect(mocks.manualAdvancesSectionMock).toHaveBeenLastCalledWith(
      expect.objectContaining({
        token: 'token',
        canApprove: true,
      }),
    )
  })

  it('keeps manual approval disabled without advance.manage even if role is supervisor', async () => {
    renderPage('/advances', { roles: ['Supervisor'], permissions: [] })

    expect(await screen.findByTestId('manual-advances-section')).toBeInTheDocument()
    expect(mocks.manualAdvancesSectionMock).toHaveBeenLastCalledWith(
      expect.objectContaining({
        token: 'token',
        canApprove: false,
      }),
    )
  })
})
