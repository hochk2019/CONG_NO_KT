import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import ReceiptFormSection from '../ReceiptFormSection'

const mocks = vi.hoisted(() => ({
  approveReceiptMock: vi.fn(),
  createSellerMock: vi.fn(),
  createReceiptMock: vi.fn(),
  fetchReceiptOpenItemsMock: vi.fn(),
  fetchCustomerLookupMock: vi.fn(),
  fetchSellerLookupMock: vi.fn(),
  mapTaxCodeOptionsMock: vi.fn(),
}))

vi.mock('../../../api/receipts', () => ({
  approveReceipt: mocks.approveReceiptMock,
  createReceipt: mocks.createReceiptMock,
  fetchReceiptOpenItems: mocks.fetchReceiptOpenItemsMock,
}))

vi.mock('../../../api/lookups', () => ({
  createSeller: mocks.createSellerMock,
  fetchCustomerLookup: mocks.fetchCustomerLookupMock,
  fetchSellerLookup: mocks.fetchSellerLookupMock,
  mapTaxCodeOptions: mocks.mapTaxCodeOptionsMock,
}))

describe('ReceiptFormSection validation', () => {
  beforeEach(() => {
    mocks.approveReceiptMock.mockReset()
    mocks.createSellerMock.mockReset()
    mocks.createReceiptMock.mockReset()
    mocks.fetchReceiptOpenItemsMock.mockReset()
    mocks.fetchCustomerLookupMock.mockReset()
    mocks.fetchSellerLookupMock.mockReset()
    mocks.mapTaxCodeOptionsMock.mockReset()

    mocks.createReceiptMock.mockResolvedValue({
      id: 'receipt-1',
      status: 'DRAFT',
      version: 0,
      amount: 100000,
      unallocatedAmount: 100000,
      autoAllocateEnabled: true,
      receiptNo: 'PT-001',
      receiptDate: '2026-03-20',
      allocationMode: 'MANUAL',
      allocationStatus: 'UNALLOCATED',
      allocationPriority: 'ISSUE_DATE',
      method: 'BANK',
      sellerTaxCode: 'SELLER01',
      customerTaxCode: 'CUST01',
    })
    mocks.fetchReceiptOpenItemsMock.mockResolvedValue([])
    mocks.fetchCustomerLookupMock.mockResolvedValue([])
    mocks.fetchSellerLookupMock.mockResolvedValue([])
    mocks.mapTaxCodeOptionsMock.mockReturnValue([])
  })

  it('requires receipt number before saving a draft', async () => {
    const user = userEvent.setup()

    render(<ReceiptFormSection token="token-receipt" onReload={vi.fn()} />)

    fireEvent.change(screen.getByPlaceholderText('VD: 2301098313'), { target: { value: 'SELLER01' } })
    fireEvent.change(screen.getByPlaceholderText('VD: 2300328765'), { target: { value: 'CUST01' } })
    fireEvent.change(screen.getByLabelText('Ngày thu'), { target: { value: '2026-03-20' } })
    fireEvent.change(screen.getByLabelText('Số tiền'), { target: { value: '100000' } })

    await waitFor(() =>
      expect(mocks.fetchReceiptOpenItemsMock).toHaveBeenCalledWith({
        token: 'token-receipt',
        sellerTaxCode: 'SELLER01',
        customerTaxCode: 'CUST01',
      }),
    )

    await user.click(screen.getByRole('button', { name: 'Lưu nháp' }))

    expect(mocks.createReceiptMock).not.toHaveBeenCalled()
    expect(await screen.findByText('Vui lòng nhập số chứng từ.')).toBeInTheDocument()
  })

  it('allows quick-adding a seller from the receipt form', async () => {
    const user = userEvent.setup()

    mocks.createSellerMock.mockResolvedValue({
      taxCode: '2301098313',
      name: 'Công ty ABC',
      shortName: 'ABC',
      address: 'Hà Nội',
      status: 'ACTIVE',
    })

    render(<ReceiptFormSection token="token-receipt" onReload={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: 'Thêm bên bán' }))

    await user.clear(screen.getByLabelText('Mã số thuế'))
    await user.type(screen.getByLabelText('Mã số thuế'), '2301098313')
    await user.type(screen.getByLabelText('Tên bên bán'), 'Công ty ABC')
    await user.type(screen.getByLabelText('Tên viết tắt'), 'ABC')

    await user.click(screen.getByRole('button', { name: 'Tạo bên bán' }))

    await waitFor(() => {
      expect(mocks.createSellerMock).toHaveBeenCalledWith('token-receipt', {
        taxCode: '2301098313',
        name: 'Công ty ABC',
        shortName: 'ABC',
        address: null,
        status: 'ACTIVE',
      })
    })

    expect(screen.getByPlaceholderText('VD: 2301098313')).toHaveValue('2301098313')
  })
})
