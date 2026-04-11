import { render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import Customer360Section from '../Customer360Section'
import { fetchCustomer360, type Customer360 } from '../../../api/customers'

vi.mock('../../../api/customers', () => ({
  fetchCustomer360: vi.fn(),
}))

const mockedFetchCustomer360 = vi.mocked(fetchCustomer360)

const buildCustomer360 = (summary: Partial<Customer360['summary']> = {}): Customer360 => ({
  taxCode: '2301098313',
  name: 'Công ty Demo',
  status: 'ACTIVE',
  currentBalance: 500000000,
  paymentTermsDays: 30,
  creditLimit: 1000000000,
  ownerName: 'Nguyễn Văn A',
  managerName: 'Lê Thị B',
  summary: {
    totalOutstanding: 470000000,
    invoiceOutstanding: 450000000,
    advanceOutstanding: 50000000,
    openOutstanding: 500000000,
    unallocatedCredit: 30000000,
    netPosition: 470000000,
    netAdjustment: -30000000,
    overdueAmount: 300000000,
    overdueRatio: 0.6667,
    maxDaysPastDue: 45,
    openInvoiceCount: 5,
    nextDueDate: '2026-03-10',
    ...summary,
  },
  riskSnapshot: {
    score: 0.82,
    signal: 'HIGH',
    asOfDate: '2026-02-26',
    modelVersion: 'v2',
    createdAt: '2026-02-26T10:00:00Z',
  },
  reminderTimeline: [
    {
      id: 'd1c753f0-ec9e-4550-b0f1-ec8f3474d0d0',
      channel: 'ZALO',
      status: 'SENT',
      riskLevel: 'HIGH',
      escalationLevel: 2,
      escalationReason: null,
      message: null,
      sentAt: '2026-02-26T09:00:00Z',
      createdAt: '2026-02-26T09:00:00Z',
    },
  ],
  responseStates: [
    {
      channel: 'EMAIL',
      responseStatus: 'RESPONDED',
      latestResponseAt: '2026-02-26T09:30:00Z',
      escalationLocked: true,
      attemptCount: 1,
      currentEscalationLevel: 1,
      lastSentAt: '2026-02-26T09:00:00Z',
      updatedAt: '2026-02-26T09:30:00Z',
    },
  ],
})

describe('customer-360-section', () => {
  beforeEach(() => {
    mockedFetchCustomer360.mockReset()
  })

  it('loads and renders customer 360 data with open debt and unallocated credit', async () => {
    mockedFetchCustomer360.mockResolvedValue(buildCustomer360())

    render(
      <Customer360Section token="token-123" selectedTaxCode="2301098313" selectedName="Công ty Demo" />,
    )

    expect(screen.getByText('Customer 360 View')).toBeInTheDocument()
    await screen.findByText('Vị thế ròng')
    expect(screen.getAllByText('Công nợ mở').length).toBeGreaterThan(0)
    expect(screen.getAllByText('Tiền chưa phân bổ').length).toBeGreaterThan(0)
    expect(screen.getByText('Cấu phần vị thế ròng')).toBeInTheDocument()
    expect(screen.getByText(/Khách còn phải thanh toán/)).toBeInTheDocument()
    expect(screen.getByText(/credit chưa phân bổ/)).toBeInTheDocument()
    expect(screen.getByText('Risk Snapshot')).toBeInTheDocument()
    expect(screen.getByText('Reminder Timeline')).toBeInTheDocument()
    expect(screen.getByText('Response States')).toBeInTheDocument()
    await waitFor(() => {
      expect(mockedFetchCustomer360).toHaveBeenCalledWith('token-123', '2301098313')
    })
  })

  it('shows credit wording instead of debt wording when net position is negative', async () => {
    mockedFetchCustomer360.mockResolvedValue(
      buildCustomer360({
        totalOutstanding: -12000000,
        invoiceOutstanding: 0,
        advanceOutstanding: 0,
        openOutstanding: 0,
        unallocatedCredit: 12000000,
        netPosition: -12000000,
        netAdjustment: -12000000,
        overdueAmount: 0,
        overdueRatio: 0,
        maxDaysPastDue: 0,
        openInvoiceCount: 0,
        nextDueDate: null,
      }),
    )

    render(
      <Customer360Section token="token-123" selectedTaxCode="2301098313" selectedName="Công ty Demo" />,
    )

    await screen.findByText('Vị thế ròng')
    expect(screen.getByText(/Khách đang dư/)).toBeInTheDocument()
    expect(screen.getByText(/chờ phân bổ/)).toBeInTheDocument()
    expect(screen.getAllByText('Tiền chưa phân bổ').length).toBeGreaterThan(0)
    expect(screen.queryByText(/nợ âm/i)).not.toBeInTheDocument()
  })

  it('shows fallback error when loading fails', async () => {
    mockedFetchCustomer360.mockRejectedValue(new Error('boom'))

    render(<Customer360Section token="token-123" selectedTaxCode="2301098313" selectedName="Công ty Demo" />)

    await screen.findByText('Không tải được hồ sơ Customer 360.')
  })
})
