import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, useLocation } from 'react-router-dom'
import { vi } from 'vitest'
import { AuthContext, type AuthContextValue } from '../../context/AuthStore'
import AdvancesPage from '../AdvancesPage'

const mocks = vi.hoisted(() => ({
  importBatchSectionMock: vi.fn(),
  manualAdvancesSectionMock: vi.fn(),
}))

vi.mock('../imports/ImportBatchSection', () => ({
  default: (props: unknown) => {
    mocks.importBatchSectionMock(props)
    return <div data-testid="import-batch-section" />
  },
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
    mocks.importBatchSectionMock.mockReset()
    mocks.manualAdvancesSectionMock.mockReset()
    window.localStorage.clear()
  })

  it('defaults to manual tab and writes tab query', async () => {
    renderPage('/advances')

    expect(await screen.findByTestId('manual-advances-section')).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: 'Nhập thủ công' })).toHaveAttribute('aria-selected', 'true')

    await waitFor(() => {
      expect(screen.getByTestId('location-probe').textContent).toBe('/advances?tab=manual')
    })
  })

  it('renders import tab with fixed ADVANCE mode and switches query', async () => {
    const user = userEvent.setup()
    renderPage('/advances?tab=import')

    expect(await screen.findByTestId('import-batch-section')).toBeInTheDocument()
    const latestImportCall = mocks.importBatchSectionMock.mock.calls.at(-1)?.[0] as {
      fixedType?: string
    }
    expect(latestImportCall?.fixedType).toBe('ADVANCE')
    expect(screen.getByRole('tab', { name: 'Import từ template' })).toHaveAttribute('aria-selected', 'true')

    await user.click(screen.getByRole('tab', { name: 'Nhập thủ công' }))
    await waitFor(() => {
      expect(screen.getByTestId('location-probe').textContent).toBe('/advances?tab=manual')
    })
  })
})
