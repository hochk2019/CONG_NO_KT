import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import ReceiptSurplusQueuePanel from '../ReceiptSurplusQueuePanel'
import { listReceiptSurplusQueue } from '../../../api/receipts'

vi.mock('../../../api/receipts', () => ({
  listReceiptSurplusQueue: vi.fn(),
}))

const mockedListReceiptSurplusQueue = vi.mocked(listReceiptSurplusQueue)

describe('receipt-surplus-queue-panel', () => {
  beforeEach(() => {
    mockedListReceiptSurplusQueue.mockReset()
    mockedListReceiptSurplusQueue.mockResolvedValue({
      items: [
        {
          id: 'receipt-item-1',
          itemType: 'UNALLOCATED_RECEIPT',
          version: 2,
          status: 'UNALLOCATED',
          receiptId: 'receipt-1',
          receiptNo: 'PT-UN-001',
          receiptDate: '2026-03-01',
          sellerTaxCode: '2301098313',
          customerTaxCode: '2300328765',
          customerName: 'Cong ty A',
          ownerName: 'Nguyen Van A',
          originalInvoiceNo: null,
          originalInvoiceDate: null,
          amountRemaining: 400000,
          ageDays: 7,
          canManage: true,
        },
        {
          id: 'held-credit-1',
          itemType: 'HELD_CREDIT',
          version: 1,
          status: 'HOLDING',
          receiptId: 'receipt-2',
          receiptNo: 'PT-HC-001',
          receiptDate: '2026-02-25',
          sellerTaxCode: '2301098313',
          customerTaxCode: '2300328765',
          customerName: 'Cong ty A',
          ownerName: 'Nguyen Van B',
          originalInvoiceNo: 'INV-HC-001',
          originalInvoiceDate: '2026-02-20',
          amountRemaining: 300000,
          ageDays: 12,
          canManage: false,
        },
      ],
      page: 1,
      pageSize: 10,
      total: 2,
    })
  })

  it('loads global surplus items and renders summary plus deep links', async () => {
    render(
      <MemoryRouter>
        <ReceiptSurplusQueuePanel token="token-123" />
      </MemoryRouter>,
    )

    expect(await screen.findByText('PT-UN-001')).toBeInTheDocument()
    expect(screen.getByText('PT-HC-001')).toBeInTheDocument()
    expect(screen.getByText('700.000 đ')).toBeInTheDocument()

    await waitFor(() => {
      expect(mockedListReceiptSurplusQueue).toHaveBeenCalledWith({
        token: 'token-123',
        itemType: undefined,
        search: undefined,
        page: 1,
        pageSize: 10,
      })
    })

    expect(
      screen.getByRole('link', { name: 'Mở chi tiết PT-UN-001' }),
    ).toHaveAttribute(
      'href',
      '/customers?taxCode=2300328765&tab=unallocatedReceipts&doc=PT-UN-001',
    )
    expect(
      screen.getByRole('link', { name: 'Mở chi tiết INV-HC-001' }),
    ).toHaveAttribute(
      'href',
      '/customers?taxCode=2300328765&tab=heldCredits&doc=INV-HC-001',
    )
  })

  it('refetches when filtering by item type', async () => {
    const user = userEvent.setup()

    render(
      <MemoryRouter>
        <ReceiptSurplusQueuePanel token="token-123" />
      </MemoryRouter>,
    )

    await screen.findByText('PT-UN-001')
    await user.selectOptions(screen.getByLabelText('Loại khoản'), 'HELD_CREDIT')

    await waitFor(() => {
      expect(mockedListReceiptSurplusQueue).toHaveBeenLastCalledWith({
        token: 'token-123',
        itemType: 'HELD_CREDIT',
        search: undefined,
        page: 1,
        pageSize: 10,
      })
    })
  })
})
