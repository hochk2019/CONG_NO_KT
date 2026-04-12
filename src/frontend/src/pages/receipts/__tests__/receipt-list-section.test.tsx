import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import ReceiptListSection from '../ReceiptListSection'

const mocks = vi.hoisted(() => ({
  approveReceiptMock: vi.fn(),
  approveReceiptsBulkMock: vi.fn(),
  correctReceiptMock: vi.fn(),
  fetchCustomerLookupMock: vi.fn(),
  fetchReceiptAllocationsMock: vi.fn(),
  fetchReceiptHistoryMock: vi.fn(),
  fetchReceiptOpenItemsMock: vi.fn(),
  fetchSellerLookupMock: vi.fn(),
  getReceiptMock: vi.fn(),
  listReceiptsMock: vi.fn(),
  mapTaxCodeOptionsMock: vi.fn(),
  navigateMock: vi.fn(),
  unvoidReceiptMock: vi.fn(),
  updateReceiptReminderMock: vi.fn(),
  voidReceiptMock: vi.fn(),
}))

vi.mock('../../../api/receipts', () => ({
  approveReceipt: mocks.approveReceiptMock,
  approveReceiptsBulk: mocks.approveReceiptsBulkMock,
  correctReceipt: mocks.correctReceiptMock,
  fetchReceiptAllocations: mocks.fetchReceiptAllocationsMock,
  fetchReceiptHistory: mocks.fetchReceiptHistoryMock,
  fetchReceiptOpenItems: mocks.fetchReceiptOpenItemsMock,
  getReceipt: mocks.getReceiptMock,
  listReceipts: mocks.listReceiptsMock,
  unvoidReceipt: mocks.unvoidReceiptMock,
  updateReceiptReminder: mocks.updateReceiptReminderMock,
  voidReceipt: mocks.voidReceiptMock,
}))

vi.mock('../../../api/lookups', () => ({
  fetchCustomerLookup: mocks.fetchCustomerLookupMock,
  fetchSellerLookup: mocks.fetchSellerLookupMock,
  mapTaxCodeOptions: mocks.mapTaxCodeOptionsMock,
}))

vi.mock('react-router-dom', () => ({
  useNavigate: () => mocks.navigateMock,
}))

describe('ReceiptListSection', () => {
  const baseRow = {
    id: 'receipt-1',
    status: 'APPROVED',
    version: 1,
    receiptNo: 'PT-001',
    receiptDate: '2026-03-20',
    amount: 1000000,
    unallocatedAmount: 0,
    autoAllocateEnabled: true,
    allocationMode: 'MANUAL',
    allocationStatus: 'ALLOCATED',
    allocationPriority: 'ISSUE_DATE',
    appliedPeriodStart: '2026-03-01',
    selectedTargets: [{ id: 'invoice-1', targetType: 'INVOICE' }],
    method: 'BANK',
    description: 'ghi chu cu',
    sellerTaxCode: '0312345678',
    customerTaxCode: '0101234567',
    customerName: 'ACME',
    ownerName: 'Owner',
    canManage: true,
  }

  const receiptDetail = {
    id: 'receipt-1',
    status: 'APPROVED',
    version: 1,
    amount: 1000000,
    unallocatedAmount: 0,
    autoAllocateEnabled: true,
    receiptNo: 'PT-001',
    receiptDate: '2026-03-20',
    allocationMode: 'MANUAL',
    allocationStatus: 'ALLOCATED',
    allocationPriority: 'ISSUE_DATE',
    appliedPeriodStart: '2026-03-01',
    selectedTargets: [{ id: 'invoice-1', targetType: 'INVOICE' }],
    method: 'BANK',
    description: 'ghi chu cu',
    sellerTaxCode: '0312345678',
    customerTaxCode: '0101234567',
  }

  beforeEach(() => {
    mocks.approveReceiptMock.mockReset()
    mocks.approveReceiptsBulkMock.mockReset()
    mocks.correctReceiptMock.mockReset()
    mocks.fetchCustomerLookupMock.mockReset()
    mocks.fetchReceiptAllocationsMock.mockReset()
    mocks.fetchReceiptHistoryMock.mockReset()
    mocks.fetchReceiptOpenItemsMock.mockReset()
    mocks.fetchSellerLookupMock.mockReset()
    mocks.getReceiptMock.mockReset()
    mocks.listReceiptsMock.mockReset()
    mocks.mapTaxCodeOptionsMock.mockReset()
    mocks.navigateMock.mockReset()
    mocks.unvoidReceiptMock.mockReset()
    mocks.updateReceiptReminderMock.mockReset()
    mocks.voidReceiptMock.mockReset()

    mocks.listReceiptsMock.mockResolvedValue({ items: [], total: 0 })
    mocks.fetchCustomerLookupMock.mockResolvedValue([])
    mocks.fetchReceiptAllocationsMock.mockResolvedValue([])
    mocks.fetchSellerLookupMock.mockResolvedValue([])
    mocks.mapTaxCodeOptionsMock.mockReturnValue([])
    mocks.fetchReceiptHistoryMock.mockResolvedValue([])
  })

  it('submits receipt correction from the modal', async () => {
    const user = userEvent.setup()
    mocks.listReceiptsMock.mockResolvedValueOnce({ items: [baseRow], total: 1 })
    mocks.getReceiptMock.mockResolvedValueOnce(receiptDetail)
    mocks.correctReceiptMock.mockResolvedValueOnce({
      ...receiptDetail,
      description: 'ghi chu moi',
      version: 2,
    })

    render(<ReceiptListSection token="token-receipt" reloadSignal={0} />)

    await user.click(await screen.findByRole('button', { name: 'Sửa' }))

    const dialog = await screen.findByRole('dialog')
    await user.clear(within(dialog).getByLabelText('Ghi chú'))
    await user.type(within(dialog).getByLabelText('Ghi chú'), 'ghi chu moi')
    await user.selectOptions(within(dialog).getByLabelText('Hình thức'), 'CASH')
    await user.type(within(dialog).getByLabelText('Lý do điều chỉnh'), 'Điều chỉnh test')
    await user.click(within(dialog).getByRole('button', { name: 'Lưu điều chỉnh' }))

    expect(mocks.getReceiptMock).toHaveBeenCalledWith('token-receipt', 'receipt-1')
    expect(mocks.correctReceiptMock).toHaveBeenCalledWith(
      'token-receipt',
      'receipt-1',
        expect.objectContaining({
          description: 'ghi chu moi',
          allocationMode: 'MANUAL',
          appliedPeriodStart: '2026-03-01',
          allocationPriority: 'ISSUE_DATE',
          selectedTargets: [{ id: 'invoice-1', targetType: 'INVOICE' }],
          method: 'CASH',
          reason: 'Điều chỉnh test',
          version: 1,
        }),
      )
    expect(await screen.findByText('Đã cập nhật phiếu thu PT-001.')).toBeInTheDocument()
  })

  it('loads receipt history into the history modal', async () => {
    const user = userEvent.setup()
    mocks.listReceiptsMock.mockResolvedValueOnce({ items: [baseRow], total: 1 })
    mocks.fetchReceiptHistoryMock.mockResolvedValueOnce([
      {
        id: 'hist-1',
        action: 'CORRECTED',
        entityType: 'RECEIPT',
        entityId: 'receipt-1',
        userName: 'tester',
        createdAt: '2026-03-20T10:30:00Z',
        beforeData: '{"description":"ghi chu cu"}',
        afterData: '{"description":"ghi chu moi"}',
      },
    ])

    render(<ReceiptListSection token="token-receipt" reloadSignal={0} />)

    await user.click(await screen.findByRole('button', { name: 'Lịch sử sửa' }))

    expect(mocks.fetchReceiptHistoryMock).toHaveBeenCalledWith('token-receipt', 'receipt-1')
    expect(await screen.findByText('CORRECTED')).toBeInTheDocument()
    expect(screen.getByText('tester')).toBeInTheDocument()
  })

  it('reveals and navigates to the receipt deeplink from the document cell', async () => {
    const user = userEvent.setup()
    mocks.listReceiptsMock.mockResolvedValueOnce({ items: [baseRow], total: 1 })

    render(<ReceiptListSection token="token-receipt" reloadSignal={0} />)

    const documentButton = await screen.findByRole('button', {
      name: 'Mở liên kết chứng từ PT-001',
    })
    await user.click(documentButton)

    const deeplinkButton = await screen.findByRole('button', { name: 'Xem chứng từ PT-001' })
    await user.click(deeplinkButton)

    expect(mocks.navigateMock).toHaveBeenCalledWith(
      '/customers?taxCode=0101234567&tab=receipts&doc=PT-001',
    )
  })

  it('reveals and navigates to customer 360 from the customer cell', async () => {
    const user = userEvent.setup()
    mocks.listReceiptsMock.mockResolvedValueOnce({ items: [baseRow], total: 1 })

    render(<ReceiptListSection token="token-receipt" reloadSignal={0} />)

    const customerButton = await screen.findByRole('button', {
      name: 'Mở liên kết khách hàng ACME',
    })
    expect(customerButton).toHaveClass('table-deeplink-trigger')
    const customerContent = customerButton.textContent ?? ''
    expect(customerContent).toContain('0101234567')
    expect(customerContent).toContain('ACME')
    expect(customerContent.indexOf('0101234567')).toBeLessThan(customerContent.indexOf('ACME'))
    await user.click(customerButton)

    const deeplinkButton = await screen.findByRole('button', { name: 'Xem khách hàng 0101234567' })
    await user.click(deeplinkButton)

    expect(mocks.navigateMock).toHaveBeenCalledWith('/customers?taxCode=0101234567')
  })

  it('keeps receipt cells as plain text when deeplink data is incomplete', async () => {
    mocks.listReceiptsMock.mockResolvedValueOnce({
      items: [{ ...baseRow, customerTaxCode: '' }],
      total: 1,
    })

    render(<ReceiptListSection token="token-receipt" reloadSignal={0} />)

    await screen.findByText('PT-001')
    expect(screen.getByText('ACME')).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Mở liên kết chứng từ PT-001' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Mở liên kết khách hàng ACME' }),
    ).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Xem chứng từ PT-001' })).not.toBeInTheDocument()
  })

  it('keeps the existing action-column view button opening allocations modal flow', async () => {
    const user = userEvent.setup()
    mocks.listReceiptsMock.mockResolvedValueOnce({ items: [baseRow], total: 1 })
    mocks.fetchReceiptAllocationsMock.mockResolvedValueOnce([
      {
        id: 'alloc-1',
        receiptId: 'receipt-1',
        invoiceId: 'invoice-1',
        amount: 1000000,
        allocatedAt: '2026-03-20T10:00:00Z',
        invoiceNo: 'HD-001',
      },
    ])

    render(<ReceiptListSection token="token-receipt" reloadSignal={0} />)

    const actionButtons = await screen.findAllByRole('button', { name: 'Xem' })
    await user.click(actionButtons.at(-1) as HTMLButtonElement)

    expect(mocks.fetchReceiptAllocationsMock).toHaveBeenCalledWith('token-receipt', 'receipt-1')
    expect(await screen.findByRole('dialog')).toBeInTheDocument()
    expect(mocks.navigateMock).not.toHaveBeenCalled()
  })
})
