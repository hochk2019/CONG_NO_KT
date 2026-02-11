import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { invoiceStatusLabels } from '../constants'
import { buildInvoiceColumns, buildReceiptColumns } from '../transactionColumns'
import { getStoredFilter, renderSellerLabel, shortId, storeFilter } from '../utils'
import { useReceiptModal } from '../useReceiptModal'

const ReceiptHarness = () => {
  const { renderReceiptRefs, receiptModal } = useReceiptModal('')
  return (
    <div>
      {renderReceiptRefs([
        { id: 'r-1', receiptNo: 'PT-01', receiptDate: '2025-01-01', amount: 100000 },
      ])}
      <span data-testid="modal-id">{receiptModal?.id ?? ''}</span>
    </div>
  )
}

describe('transaction helpers', () => {
  it('provides status labels', () => {
    expect(invoiceStatusLabels.PAID).toBe('Đã thanh toán')
  })

  it('stores and clears filters', () => {
    storeFilter('test.filter', 'ACTIVE')
    expect(getStoredFilter('test.filter')).toBe('ACTIVE')
    storeFilter('test.filter', '')
    expect(getStoredFilter('test.filter')).toBe('')
  })

  it('shortens ids', () => {
    expect(shortId('1234567890')).toBe('12345678')
  })

  it('renders seller label', () => {
    render(renderSellerLabel('2301098313', 'Hoang Minh'))
    expect(screen.getByText('2301098313')).toBeInTheDocument()
    expect(screen.getByText('(Hoang Minh)')).toBeInTheDocument()
  })

  it('builds transaction columns', () => {
    const columns = buildInvoiceColumns({
      canManageCustomers: false,
      openInvoiceModal: () => undefined,
      renderReceiptRefs: () => <span>-</span>,
    })
    expect(columns.some((column) => column.key === 'actions')).toBe(true)

    const receiptColumns = buildReceiptColumns({
      openReceiptModal: () => undefined,
    })
    expect(receiptColumns.some((column) => column.key === 'receiptNo')).toBe(true)
  })

  it('opens receipt modal from receipt refs', async () => {
    const user = userEvent.setup()
    render(<ReceiptHarness />)
    await user.click(screen.getByRole('button', { name: /PT-01/i }))
    expect(screen.getByTestId('modal-id')).toHaveTextContent('r-1')
  })
})
