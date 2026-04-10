import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, useLocation } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { InvoiceListItem } from '../../api/invoices'
import { AuthContext, type AuthContextValue } from '../../context/AuthStore'
import InvoicesPage from '../InvoicesPage'

const mocks = vi.hoisted(() => ({
  listInvoicesMock: vi.fn(),
  manualInvoicesSectionMock: vi.fn(),
}))

vi.mock('../../api/invoices', async () => {
  const actual = await vi.importActual<typeof import('../../api/invoices')>('../../api/invoices')
  return {
    ...actual,
    listInvoices: (...args: unknown[]) => mocks.listInvoicesMock(...args),
  }
})

vi.mock('../imports/ManualInvoicesSection', () => ({
  default: (props: unknown) => {
    mocks.manualInvoicesSectionMock(props)
    const typedProps = props as { onImportTemplate?: () => void }
    return (
      <div data-testid="manual-invoices-section">
        {typeof typedProps.onImportTemplate === 'function' ? (
          <button type="button" onClick={typedProps.onImportTemplate}>
            Import từ Template
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
        <InvoicesPage />
      </AuthContext.Provider>
    </MemoryRouter>,
  )
}

const buildInvoiceListItem = (overrides: Partial<InvoiceListItem> = {}): InvoiceListItem => ({
  id: 'invoice-1',
  invoiceNo: 'INV-001',
  issueDate: '2026-03-15',
  totalAmount: 5_000_000,
  outstandingAmount: 2_500_000,
  status: 'OPEN',
  version: 1,
  customerTaxCode: '0312345678',
  customerName: 'Công ty TNHH Minh An',
  sellerTaxCode: '0309876543',
  sellerShortName: 'Seller A',
  receiptRefs: [],
  reductionInvoiceRefs: [],
  reducedInvoiceRefs: [],
  ...overrides,
})

describe('InvoicesPage', () => {
  beforeEach(() => {
    mocks.listInvoicesMock.mockReset()
    mocks.listInvoicesMock.mockResolvedValue({
      items: [],
      page: 1,
      pageSize: 10,
      total: 0,
    })
    mocks.manualInvoicesSectionMock.mockReset()
  })

  it('renders manual entry and root invoice list without the old import workspace tabs', async () => {
    renderPage('/invoices')

    expect(await screen.findByTestId('manual-invoices-section')).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 3, name: 'Danh sách hóa đơn' })).toBeInTheDocument()
    expect(screen.queryByRole('tab')).not.toBeInTheDocument()
    expect(screen.getByTestId('location-probe').textContent).toBe('/invoices')
    expect(mocks.listInvoicesMock).toHaveBeenCalledWith(
      'token',
      expect.objectContaining({
        page: 1,
        pageSize: 10,
      }),
    )
  })

  it('routes import template CTA to centralized batch imports', async () => {
    const user = userEvent.setup()
    renderPage('/invoices')

    const importButton = await screen.findByRole('button', { name: 'Import từ Template' })
    await user.click(importButton)

    expect(mocks.manualInvoicesSectionMock).toHaveBeenLastCalledWith(
      expect.objectContaining({
        onImportTemplate: expect.any(Function),
      }),
    )
    expect(screen.getByTestId('location-probe').textContent).toBe('/imports?tab=batch&type=INVOICE')
  })

  it('redirects legacy invoice import tab query to centralized imports page', async () => {
    renderPage('/invoices?tab=import')

    await waitFor(() => {
      expect(screen.getByTestId('location-probe').textContent).toBe('/imports?tab=batch&type=INVOICE')
    })
  })

  it('keeps manual invoice commit gated by invoice permission', async () => {
    renderPage('/invoices', { permissions: [] })

    expect(await screen.findByTestId('manual-invoices-section')).toBeInTheDocument()
    expect(mocks.manualInvoicesSectionMock).toHaveBeenLastCalledWith(
      expect.objectContaining({
        token: 'token',
        canCommit: false,
      }),
    )
  })

  it('enables manual invoice commit when invoice import permission is granted', async () => {
    renderPage('/invoices', { permissions: ['import.commit.invoice'] })

    expect(await screen.findByTestId('manual-invoices-section')).toBeInTheDocument()
    expect(mocks.manualInvoicesSectionMock).toHaveBeenLastCalledWith(
      expect.objectContaining({
        token: 'token',
        canCommit: true,
      }),
    )
  })

  it('reveals invoice deeplink action and navigates to the matching customer invoice tab', async () => {
    const user = userEvent.setup()
    mocks.listInvoicesMock.mockResolvedValueOnce({
      items: [buildInvoiceListItem({ invoiceNo: 'INV-693' })],
      page: 1,
      pageSize: 10,
      total: 1,
    })

    renderPage('/invoices')

    const invoiceTrigger = await screen.findByRole('button', { name: 'INV-693' })
    await user.click(invoiceTrigger)

    const row = invoiceTrigger.closest('tr')
    expect(row).not.toBeNull()
    const viewButton = within(row as HTMLTableRowElement).getByRole('button', { name: 'Xem' })
    await user.click(viewButton)

    expect(screen.getByTestId('location-probe').textContent).toBe(
      '/customers?taxCode=0312345678&tab=invoices&doc=INV-693',
    )
  })

  it('reveals customer deeplink action and navigates to Customer 360 View', async () => {
    const user = userEvent.setup()
    mocks.listInvoicesMock.mockResolvedValueOnce({
      items: [buildInvoiceListItem()],
      page: 1,
      pageSize: 10,
      total: 1,
    })

    renderPage('/invoices')

    const customerTrigger = await screen.findByRole('button', {
      name: '0312345678 Công ty TNHH Minh An',
    })
    await user.click(customerTrigger)

    const row = customerTrigger.closest('tr')
    expect(row).not.toBeNull()
    const viewButton = within(row as HTMLTableRowElement).getByRole('button', { name: 'Xem' })
    await user.click(viewButton)

    expect(screen.getByTestId('location-probe').textContent).toBe('/customers?taxCode=0312345678')
  })

  it('keeps invoice and customer cells as plain text when tax code is missing', async () => {
    mocks.listInvoicesMock.mockResolvedValueOnce({
      items: [buildInvoiceListItem({ customerTaxCode: '' })],
      page: 1,
      pageSize: 10,
      total: 1,
    })

    renderPage('/invoices')

    expect(await screen.findByText('INV-001')).toBeInTheDocument()
    expect(screen.getByText('Công ty TNHH Minh An')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'INV-001' })).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Công ty TNHH Minh An' }),
    ).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Xem' })).not.toBeInTheDocument()
  })
})
