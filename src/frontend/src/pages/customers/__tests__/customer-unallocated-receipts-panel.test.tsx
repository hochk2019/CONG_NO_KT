import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import CustomerUnallocatedReceiptsPanel from '../CustomerUnallocatedReceiptsPanel'
import { fetchCustomerReceipts } from '../../../api/customers'
import {
  allocateApprovedReceipt,
  fetchReceiptOpenItems,
  previewReceipt,
  updateReceiptAutoAllocation,
} from '../../../api/receipts'

vi.mock('../../../api/customers', () => ({
  fetchCustomerReceipts: vi.fn(),
}))

vi.mock('../../../api/receipts', () => ({
  allocateApprovedReceipt: vi.fn(),
  fetchReceiptAllocations: vi.fn(),
  fetchReceiptOpenItems: vi.fn(),
  previewReceipt: vi.fn(),
  updateReceiptAutoAllocation: vi.fn(),
}))

const mockedFetchCustomerReceipts = vi.mocked(fetchCustomerReceipts)
const mockedFetchReceiptOpenItems = vi.mocked(fetchReceiptOpenItems)
const mockedPreviewReceipt = vi.mocked(previewReceipt)
const mockedUpdateReceiptAutoAllocation = vi.mocked(updateReceiptAutoAllocation)
const mockedAllocateApprovedReceipt = vi.mocked(allocateApprovedReceipt)

describe('customer-unallocated-receipts-panel', () => {
  beforeEach(() => {
    mockedFetchCustomerReceipts.mockReset()
    mockedFetchReceiptOpenItems.mockReset()
    mockedPreviewReceipt.mockReset()
    mockedUpdateReceiptAutoAllocation.mockReset()
    mockedAllocateApprovedReceipt.mockReset()

    mockedFetchCustomerReceipts.mockResolvedValue({
      items: [
        {
          id: 'rcp-1',
          receiptNo: 'PT-001',
          receiptDate: '2026-03-01',
          amount: 1000000,
          unallocatedAmount: 400000,
          status: 'APPROVED',
          version: 7,
          sellerTaxCode: '2301098313',
          sellerShortName: 'Hoàng Minh',
          autoAllocateEnabled: false,
        },
      ],
      page: 1,
      pageSize: 10,
      total: 1,
    })

    mockedFetchReceiptOpenItems.mockResolvedValue([
      {
        targetId: 'inv-open-1',
        targetType: 'INVOICE',
        documentNo: 'HD-OPEN-001',
        issueDate: '2026-03-02',
        dueDate: '2026-03-15',
        outstandingAmount: 400000,
        sellerTaxCode: '2301098313',
        customerTaxCode: '2300328765',
      },
    ])

    mockedPreviewReceipt.mockResolvedValue({
      lines: [{ targetId: 'inv-open-1', targetType: 'INVOICE', amount: 400000 }],
      unallocatedAmount: 0,
    })

    mockedUpdateReceiptAutoAllocation.mockResolvedValue({
      id: 'rcp-1',
      status: 'APPROVED',
      version: 8,
      amount: 1000000,
      unallocatedAmount: 400000,
      receiptNo: 'PT-001',
      receiptDate: '2026-03-01',
      autoAllocateEnabled: true,
      appliedPeriodStart: null,
      allocationMode: 'AUTO',
      allocationStatus: 'PENDING',
      allocationPriority: 'ISSUE_DATE',
      allocationSource: null,
      allocationSuggestedAt: null,
      selectedTargets: null,
      method: 'BANK',
      sellerTaxCode: '2301098313',
      customerTaxCode: '2300328765',
    })

    mockedAllocateApprovedReceipt.mockResolvedValue({
      lines: [{ targetId: 'inv-open-1', targetType: 'INVOICE', amount: 400000 }],
      unallocatedAmount: 0,
    })
  })

  it('loads only unallocated approved receipts and seeds search from initial doc', async () => {
    render(
      <CustomerUnallocatedReceiptsPanel
        token="token-123"
        canManageCustomers
        selectedTaxCode="2300328765"
        initialDoc="PT-001"
      />,
    )

    expect(screen.getByRole('textbox', { name: /Tìm phiếu thu/i })).toHaveValue('PT-001')
    await screen.findByText('PT-001')

    await waitFor(() => {
      expect(mockedFetchCustomerReceipts).toHaveBeenCalledWith(
        expect.objectContaining({
          token: 'token-123',
          taxCode: '2300328765',
          search: 'PT-001',
          unallocatedOnly: true,
        }),
      )
    })
  })

  it('updates auto-allocation state from the unallocated receipts tab', async () => {
    const user = userEvent.setup()

    render(
      <CustomerUnallocatedReceiptsPanel
        token="token-123"
        canManageCustomers
        selectedTaxCode="2300328765"
      />,
    )

    await screen.findByText('PT-001')
    await user.click(screen.getByRole('button', { name: 'Bật tự phân bổ' }))

    await waitFor(() => {
      expect(mockedUpdateReceiptAutoAllocation).toHaveBeenCalledWith('token-123', 'rcp-1', {
        autoAllocateEnabled: true,
        version: 7,
      })
    })
  })

  it('opens manual allocation for the remaining unallocated amount', async () => {
    const user = userEvent.setup()

    render(
      <CustomerUnallocatedReceiptsPanel
        token="token-123"
        canManageCustomers
        selectedTaxCode="2300328765"
      />,
    )

    await screen.findByText('PT-001')
    await user.click(screen.getByRole('button', { name: 'Áp tay' }))

    const allocationDialog = await screen.findByRole('dialog', { name: /Phân bổ phiếu thu/i })
    await screen.findByText('HD-OPEN-001')
    await user.click(within(allocationDialog).getByRole('button', { name: 'Áp tay' }))

    await waitFor(() => {
      expect(mockedAllocateApprovedReceipt).toHaveBeenCalledWith('token-123', 'rcp-1', {
        selectedTargets: [{ id: 'inv-open-1', targetType: 'INVOICE' }],
        version: 7,
      })
    })
  })
})
