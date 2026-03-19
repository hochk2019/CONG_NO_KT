import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { vi } from 'vitest'
import { AuthContext, type AuthContextValue } from '../../../context/AuthStore'
import CustomersPage from '../CustomersPage'

const mocks = vi.hoisted(() => ({
  customerListSectionMock: vi.fn(),
  customer360SectionMock: vi.fn(),
  customerTransactionsSectionMock: vi.fn(),
}))

vi.mock('../CustomerListSection', () => ({
  default: (props: unknown) => {
    mocks.customerListSectionMock(props)
    return <div data-testid="customer-list-section" />
  },
}))

vi.mock('../Customer360Section', () => ({
  default: (props: unknown) => {
    mocks.customer360SectionMock(props)
    return <div data-testid="customer-360-section" />
  },
}))

vi.mock('../CustomerTransactionsSection', () => ({
  default: (props: unknown) => {
    mocks.customerTransactionsSectionMock(props)
    return <div data-testid="customer-transactions-section" />
  },
}))

type AuthOverride = {
  roles?: string[]
  permissions?: string[]
}

const buildAuthContext = ({ roles = [], permissions = [] }: AuthOverride = {}): AuthContextValue => ({
  state: {
    accessToken: 'token',
    expiresAt: new Date(Date.now() + 60_000).toISOString(),
    username: 'customer-user',
    roles,
    permissions,
  },
  isAuthenticated: true,
  isBootstrapping: false,
  login: vi.fn(),
  logout: vi.fn(),
})

function renderPage(authOverride?: AuthOverride) {
  return render(
    <MemoryRouter initialEntries={['/customers']}>
      <AuthContext.Provider value={buildAuthContext(authOverride)}>
        <CustomersPage />
      </AuthContext.Provider>
    </MemoryRouter>,
  )
}

describe('CustomersPage permission gating', () => {
  beforeEach(() => {
    mocks.customerListSectionMock.mockReset()
    mocks.customer360SectionMock.mockReset()
    mocks.customerTransactionsSectionMock.mockReset()
  })

  it('keeps manage actions disabled for customer view only access', () => {
    renderPage({ permissions: ['customer.view'] })

    expect(screen.getByTestId('customer-list-section')).toBeInTheDocument()
    expect(screen.getByTestId('customer-transactions-section')).toBeInTheDocument()

    const listCall = mocks.customerListSectionMock.mock.calls.at(-1)?.[0] as { canManageCustomers?: boolean }
    const transactionsCall = mocks.customerTransactionsSectionMock.mock.calls.at(-1)?.[0] as {
      canManageCustomers?: boolean
    }

    expect(listCall?.canManageCustomers).toBe(false)
    expect(transactionsCall?.canManageCustomers).toBe(false)
  })

  it('does not grant manage actions from supervisor role without explicit customer permissions', () => {
    renderPage({ roles: ['Supervisor'] })

    expect(screen.getByTestId('customer-list-section')).toBeInTheDocument()
    expect(screen.getByTestId('customer-transactions-section')).toBeInTheDocument()

    const listCall = mocks.customerListSectionMock.mock.calls.at(-1)?.[0] as { canManageCustomers?: boolean }
    const transactionsCall = mocks.customerTransactionsSectionMock.mock.calls.at(-1)?.[0] as {
      canManageCustomers?: boolean
    }

    expect(listCall?.canManageCustomers).toBe(false)
    expect(transactionsCall?.canManageCustomers).toBe(false)
  })

  it('enables manage actions when any customer edit or assignment permission exists', () => {
    renderPage({ permissions: ['customer.edit.owned'] })

    expect(screen.getByTestId('customer-list-section')).toBeInTheDocument()
    expect(screen.getByTestId('customer-transactions-section')).toBeInTheDocument()

    const listCall = mocks.customerListSectionMock.mock.calls.at(-1)?.[0] as { canManageCustomers?: boolean }
    const transactionsCall = mocks.customerTransactionsSectionMock.mock.calls.at(-1)?.[0] as {
      canManageCustomers?: boolean
    }

    expect(listCall?.canManageCustomers).toBe(true)
    expect(transactionsCall?.canManageCustomers).toBe(true)
  })
})
