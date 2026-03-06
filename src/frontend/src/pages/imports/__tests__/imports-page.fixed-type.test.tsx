import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, useLocation } from 'react-router-dom'
import { vi } from 'vitest'
import { AuthContext, type AuthContextValue } from '../../../context/AuthStore'
import ImportsPage from '../ImportsPage'

const mocks = vi.hoisted(() => ({
  importBatchSectionMock: vi.fn(),
  manualInvoicesSectionMock: vi.fn(),
}))

vi.mock('../ImportBatchSection', () => ({
  default: (props: unknown) => {
    mocks.importBatchSectionMock(props)
    return <div data-testid="import-batch-section" />
  },
}))

vi.mock('../ManualInvoicesSection', () => ({
  default: (props: unknown) => {
    mocks.manualInvoicesSectionMock(props)
    return <div data-testid="manual-invoices-section" />
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
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <AuthContext.Provider value={buildAuthContext()}>
        <LocationProbe />
        <ImportsPage />
      </AuthContext.Provider>
    </MemoryRouter>,
  )
}

describe('ImportsPage deep-link type', () => {
  beforeEach(() => {
    mocks.importBatchSectionMock.mockReset()
    mocks.manualInvoicesSectionMock.mockReset()
    window.localStorage.clear()
  })

  it('maps query type into fixedType for import batch section', async () => {
    renderPage('/imports?tab=batch&type=ADVANCE')

    expect(await screen.findByTestId('import-batch-section')).toBeInTheDocument()
    const latestCall = mocks.importBatchSectionMock.mock.calls.at(-1)?.[0] as { fixedType?: string }
    expect(latestCall?.fixedType).toBe('ADVANCE')
    expect(screen.getByRole('tab', { name: 'Nhập file' })).toHaveAttribute('aria-selected', 'true')
  })

  it('preserves type query when switching tab and auto-fills missing tab', async () => {
    const user = userEvent.setup()
    renderPage('/imports?type=ADVANCE')

    await waitFor(() => {
      expect(screen.getByTestId('location-probe').textContent).toBe('/imports?tab=batch&type=ADVANCE')
    })

    await user.click(screen.getByRole('tab', { name: 'Nhập thủ công hóa đơn' }))
    await waitFor(() => {
      expect(screen.getByTestId('location-probe').textContent).toBe('/imports?tab=manual&type=ADVANCE')
    })
  })
})
