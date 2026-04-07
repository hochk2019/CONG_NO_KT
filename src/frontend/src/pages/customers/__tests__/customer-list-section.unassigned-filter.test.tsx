import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const {
  fetchCustomersMock,
  fetchCustomerDetailMock,
  updateCustomerMock,
  fetchOwnerLookupMock,
  fetchUserLookupMock,
} = vi.hoisted(() => ({
  fetchCustomersMock: vi.fn(),
  fetchCustomerDetailMock: vi.fn(),
  updateCustomerMock: vi.fn(),
  fetchOwnerLookupMock: vi.fn(),
  fetchUserLookupMock: vi.fn(),
}))

vi.mock('../../../api/customers', async () => {
  const actual = await vi.importActual<typeof import('../../../api/customers')>(
    '../../../api/customers',
  )

  return {
    ...actual,
    fetchCustomers: fetchCustomersMock,
    fetchCustomerDetail: fetchCustomerDetailMock,
    updateCustomer: updateCustomerMock,
  }
})

vi.mock('../../../api/lookups', async () => {
  const actual = await vi.importActual<typeof import('../../../api/lookups')>(
    '../../../api/lookups',
  )

  return {
    ...actual,
    fetchOwnerLookup: fetchOwnerLookupMock,
    fetchUserLookup: fetchUserLookupMock,
  }
})

import CustomerListSection from '../CustomerListSection'

describe('CustomerListSection unassigned owner filter', () => {
  beforeEach(() => {
    fetchCustomersMock.mockReset()
    fetchCustomerDetailMock.mockReset()
    updateCustomerMock.mockReset()
    fetchOwnerLookupMock.mockReset()
    fetchUserLookupMock.mockReset()

    fetchCustomersMock.mockResolvedValue({
      items: [],
      page: 1,
      pageSize: 10,
      total: 0,
    })

    fetchOwnerLookupMock.mockResolvedValue([
      { id: 'owner-1', name: 'Nguyen Van A', username: 'nguyenvana' },
    ])

    fetchUserLookupMock.mockResolvedValue([])
  })

  it('requests unassigned customers when selecting Chưa phân công', async () => {
    const user = userEvent.setup()

    render(
      <CustomerListSection
        token="token-demo"
        canManageCustomers={false}
        selectedTaxCode={null}
        selectedName=""
        onSelectCustomer={vi.fn()}
      />,
    )

    const unassignedOption = await screen.findByRole('option', { name: 'Chưa phân công' })
    expect(unassignedOption).toBeInTheDocument()

    await waitFor(() => {
      expect(fetchCustomersMock).toHaveBeenCalledWith(
        expect.objectContaining({
          token: 'token-demo',
          page: 1,
          pageSize: 10,
        }),
      )
    })

    fetchCustomersMock.mockClear()

    const ownerSelect = unassignedOption.closest('select')
    expect(ownerSelect).not.toBeNull()

    await user.selectOptions(ownerSelect as HTMLSelectElement, '__unassigned__')

    await waitFor(() => {
      expect(fetchCustomersMock).toHaveBeenCalledWith(
        expect.objectContaining({
          token: 'token-demo',
          ownerId: undefined,
          unassignedOnly: true,
          page: 1,
          pageSize: 10,
        }),
      )
    })

    expect(screen.getByText('Phụ trách: Chưa phân công')).toBeInTheDocument()
  })
})
