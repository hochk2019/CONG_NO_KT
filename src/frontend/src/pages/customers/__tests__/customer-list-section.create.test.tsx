import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const {
  createCustomerMock,
  fetchCustomersMock,
  fetchCustomerDetailMock,
  updateCustomerMock,
  fetchOwnerLookupMock,
  fetchUserLookupMock,
} = vi.hoisted(() => ({
  createCustomerMock: vi.fn(),
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
    createCustomer: createCustomerMock,
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

describe('CustomerListSection create flow', () => {
  beforeEach(() => {
    createCustomerMock.mockReset()
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
    fetchOwnerLookupMock.mockResolvedValue([])
    fetchUserLookupMock.mockResolvedValue([])
  })

  it('creates a customer and selects it immediately', async () => {
    const user = userEvent.setup()
    const onSelectCustomer = vi.fn()

    createCustomerMock.mockResolvedValue({
      taxCode: '0101234567',
      name: 'Công ty Khách hàng',
      address: 'Hà Nội',
      email: 'contact@example.com',
      phone: '0901234567',
      status: 'ACTIVE',
      paymentTermsDays: 15,
      creditLimit: 5000000,
      currentBalance: 0,
      ownerId: null,
      ownerName: null,
      managerId: null,
      managerName: null,
      createdAt: '2026-04-11T00:00:00Z',
      updatedAt: '2026-04-11T00:00:00Z',
    })

    render(
      <CustomerListSection
        token="token-customer"
        canManageCustomers
        selectedTaxCode={null}
        selectedName=""
        onSelectCustomer={onSelectCustomer}
      />,
    )

    await user.click(await screen.findByRole('button', { name: 'Thêm khách hàng' }))

    await user.type(screen.getByLabelText('Mã số thuế'), '0101234567')
    await user.type(screen.getByLabelText('Tên khách hàng'), 'Công ty Khách hàng')
    await user.type(screen.getByLabelText('Email'), 'contact@example.com')
    await user.clear(screen.getByLabelText('Số ngày công nợ'))
    await user.type(screen.getByLabelText('Số ngày công nợ'), '15')
    await user.type(screen.getByLabelText('Hạn mức công nợ'), '5000000')

    await user.click(screen.getByRole('button', { name: 'Tạo khách hàng' }))

    await waitFor(() => {
      expect(createCustomerMock).toHaveBeenCalledWith('token-customer', {
        taxCode: '0101234567',
        name: 'Công ty Khách hàng',
        address: null,
        email: 'contact@example.com',
        phone: null,
        status: 'ACTIVE',
        paymentTermsDays: 15,
        creditLimit: 5000000,
        ownerId: null,
        managerId: null,
      })
    })

    expect(onSelectCustomer).toHaveBeenCalledWith({
      taxCode: '0101234567',
      name: 'Công ty Khách hàng',
      ownerName: null,
      currentBalance: 0,
      status: 'ACTIVE',
    })
  })
})
