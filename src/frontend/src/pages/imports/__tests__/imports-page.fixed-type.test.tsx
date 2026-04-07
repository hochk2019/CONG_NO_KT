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

type AuthOverride = {
  roles?: string[]
  permissions?: string[]
}

const buildAuthContext = ({ roles = ['Accountant'], permissions = [] }: AuthOverride = {}): AuthContextValue => ({
  state: {
    accessToken: 'token',
    expiresAt: new Date(Date.now() + 60_000).toISOString(),
    username: 'accountant',
    roles,
    permissions,
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

function renderPage(initialEntry: string, authOverride?: AuthOverride) {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <AuthContext.Provider value={buildAuthContext(authOverride)}>
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

  it('maps stage and commit permission by fixed import type', async () => {
    renderPage('/imports?tab=batch&type=ADVANCE', {
      permissions: ['import.upload', 'import.commit.advance'],
    })

    expect(await screen.findByTestId('import-batch-section')).toBeInTheDocument()
    const latestCall = mocks.importBatchSectionMock.mock.calls.at(-1)?.[0] as {
      canStage?: boolean
      canCommit?: boolean
      fixedType?: string
    }

    expect(latestCall?.fixedType).toBe('ADVANCE')
    expect(latestCall?.canStage).toBe(true)
    expect(latestCall?.canCommit).toBe(true)
  })

  it('does not grant batch staging from accountant role without import permissions', async () => {
    renderPage('/imports?tab=batch&type=ADVANCE', {
      roles: ['Accountant'],
      permissions: [],
    })

    expect(await screen.findByTestId('import-batch-section')).toBeInTheDocument()
    const latestCall = mocks.importBatchSectionMock.mock.calls.at(-1)?.[0] as {
      canStage?: boolean
      canCommit?: boolean
      fixedType?: string
    }

    expect(latestCall?.fixedType).toBe('ADVANCE')
    expect(latestCall?.canStage).toBe(false)
    expect(latestCall?.canCommit).toBe(false)
  })

  it('does not grant commit from supervisor role without explicit commit permission', async () => {
    renderPage('/imports?tab=batch&type=INVOICE', {
      roles: ['Supervisor'],
      permissions: ['import.upload'],
    })

    expect(await screen.findByTestId('import-batch-section')).toBeInTheDocument()
    const latestCall = mocks.importBatchSectionMock.mock.calls.at(-1)?.[0] as {
      canStage?: boolean
      canCommit?: boolean
      fixedType?: string
    }

    expect(latestCall?.fixedType).toBe('INVOICE')
    expect(latestCall?.canStage).toBe(true)
    expect(latestCall?.canCommit).toBe(false)
  })

  it('blocks invoice commit when user lacks invoice permission', async () => {
    renderPage('/imports?tab=batch&type=INVOICE', {
      permissions: ['import.upload', 'import.commit.advance'],
    })

    expect(await screen.findByTestId('import-batch-section')).toBeInTheDocument()
    const latestCall = mocks.importBatchSectionMock.mock.calls.at(-1)?.[0] as {
      canStage?: boolean
      canCommit?: boolean
      fixedType?: string
    }

    expect(latestCall?.fixedType).toBe('INVOICE')
    expect(latestCall?.canStage).toBe(true)
    expect(latestCall?.canCommit).toBe(false)
  })

  it('keeps manual invoice commit gated by invoice permission', async () => {
    renderPage('/imports?tab=manual', {
      permissions: ['import.upload', 'import.commit.advance'],
    })

    expect(await screen.findByTestId('manual-invoices-section')).toBeInTheDocument()
    const latestCall = mocks.manualInvoicesSectionMock.mock.calls.at(-1)?.[0] as { canCommit?: boolean }
    expect(latestCall?.canCommit).toBe(false)
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
