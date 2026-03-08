import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import CustomerHeldCreditsPanel from '../CustomerHeldCreditsPanel'
import { fetchCustomerHeldCredits, fetchCustomerInvoices } from '../../../api/customers'
import { applyHeldCredit, releaseHeldCredit } from '../../../api/heldCredits'

vi.mock('../../../api/customers', () => ({
  fetchCustomerHeldCredits: vi.fn(),
  fetchCustomerInvoices: vi.fn(),
}))

vi.mock('../../../api/heldCredits', () => ({
  applyHeldCredit: vi.fn(),
  releaseHeldCredit: vi.fn(),
}))

const mockedFetchCustomerHeldCredits = vi.mocked(fetchCustomerHeldCredits)
const mockedFetchCustomerInvoices = vi.mocked(fetchCustomerInvoices)
const mockedApplyHeldCredit = vi.mocked(applyHeldCredit)
const mockedReleaseHeldCredit = vi.mocked(releaseHeldCredit)

describe('customer-held-credits-panel', () => {
  beforeEach(() => {
    mockedFetchCustomerHeldCredits.mockReset()
    mockedFetchCustomerInvoices.mockReset()
    mockedApplyHeldCredit.mockReset()
    mockedReleaseHeldCredit.mockReset()

    mockedFetchCustomerHeldCredits.mockResolvedValue({
      items: [
        {
          id: 'hc-1',
          version: 3,
          status: 'HOLDING',
          receiptId: 'rcp-1',
          receiptNo: 'PT-001',
          receiptDate: '2026-03-01',
          originalInvoiceId: 'inv-old-1',
          originalInvoiceNo: 'HD-OLD-001',
          originalInvoiceDate: '2026-02-15',
          originalAmount: 1000000,
          amountRemaining: 1000000,
          appliedAmount: 0,
          createdAt: '2026-03-01T08:00:00Z',
          updatedAt: '2026-03-01T08:00:00Z',
        },
      ],
      page: 1,
      pageSize: 10,
      total: 1,
    })

    mockedFetchCustomerInvoices.mockResolvedValue({
      items: [
        {
          id: 'inv-new-1',
          invoiceNo: 'HD-NEW-001',
          issueDate: '2026-03-05',
          totalAmount: 1500000,
          outstandingAmount: 500000,
          status: 'OPEN',
          version: 1,
          sellerTaxCode: '2301098313',
          sellerShortName: 'Hoàng Minh',
          receiptRefs: [],
        },
      ],
      page: 1,
      pageSize: 20,
      total: 1,
    })

    mockedApplyHeldCredit.mockResolvedValue({
      heldCreditId: 'hc-1',
      version: 4,
      status: 'REAPPLIED',
      invoiceId: 'inv-new-1',
      appliedHeldAmount: 1000000,
      appliedGeneralCreditAmount: 500000,
      remainingHeldAmount: 0,
      invoiceOutstandingAmount: 0,
    })

    mockedReleaseHeldCredit.mockResolvedValue({
      heldCreditId: 'hc-1',
      version: 4,
      status: 'RELEASED',
      receiptId: 'rcp-1',
      releasedAmount: 1000000,
      remainingHeldAmount: 0,
      receiptUnallocatedAmount: 1000000,
    })
  })

  it('loads held credits and seeds the search filter from initial doc', async () => {
    render(
      <CustomerHeldCreditsPanel
        token="token-123"
        canManageCustomers
        selectedTaxCode="2301098313"
        initialDoc="PT-001"
      />,
    )

    expect(screen.getByRole('textbox', { name: /Tìm phiếu thu \/ hóa đơn gốc/i })).toHaveValue('PT-001')
    await screen.findByText('PT-001')
    await waitFor(() => {
      expect(mockedFetchCustomerHeldCredits).toHaveBeenCalledWith(
        expect.objectContaining({
          token: 'token-123',
          taxCode: '2301098313',
          search: 'PT-001',
        }),
      )
    })
  })

  it('applies held credit to a replacement invoice with general credit top-up enabled by default', async () => {
    const user = userEvent.setup()

    render(
      <CustomerHeldCreditsPanel
        token="token-123"
        canManageCustomers
        selectedTaxCode="2301098313"
      />,
    )

    await screen.findByText('PT-001')
    await user.click(screen.getByRole('button', { name: 'Áp sang HĐ' }))

    await screen.findByRole('dialog', { name: /Áp tiền thừa do hủy HĐ/i })
    await screen.findByText('HD-NEW-001')
    await user.click(screen.getByRole('radio', { name: /HD-NEW-001/i }))
    await user.click(screen.getByRole('button', { name: /Xác nhận áp/i }))

    await waitFor(() => {
      expect(mockedApplyHeldCredit).toHaveBeenCalledWith('token-123', 'hc-1', {
        invoiceId: 'inv-new-1',
        useGeneralCreditTopUp: true,
        version: 3,
      })
    })
  })
})
