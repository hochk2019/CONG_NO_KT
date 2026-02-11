import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import { AuthContext, type AuthContextValue } from '../../../context/AuthStore'
import ReceiptsPage from '../ReceiptsPage'
import ReceiptFormSection from '../ReceiptFormSection'
import ReceiptListSection from '../ReceiptListSection'
import ReceiptAllocationModal from '../ReceiptAllocationModal'
import ReceiptAdvancedModal from '../ReceiptAdvancedModal'
import ReceiptCancelModal from '../ReceiptCancelModal'
import ReceiptViewAllocationsModal from '../ReceiptViewAllocationsModal'

const baseAuth: AuthContextValue = {
  state: {
    accessToken: null,
    expiresAt: null,
    username: 'tester',
    roles: [],
  },
  isAuthenticated: false,
  isBootstrapping: false,
  login: async () => undefined,
  logout: () => undefined,
}

describe('receipts modules', () => {
  it('renders receipt form section', () => {
    render(<ReceiptFormSection token="" onReload={vi.fn()} />)
    expect(screen.getByText('Thông tin khách hàng')).toBeInTheDocument()
  })

  it('renders receipt list section', () => {
    render(<ReceiptListSection token="" reloadSignal={0} />)
    expect(screen.getByText('Danh sách phiếu thu')).toBeInTheDocument()
  })

  it('renders allocation modal', () => {
    render(
      <ReceiptAllocationModal
        isOpen
        token=""
        sellerTaxCode="2301098313"
        customerTaxCode="2300328765"
        amount={1000000}
        allocationPriority="ISSUE_DATE"
        onPriorityChange={vi.fn()}
        openItems={[
          {
            targetId: 'inv-1',
            targetType: 'INVOICE',
            documentNo: 'HD-001',
            issueDate: '2025-01-01',
            dueDate: '2025-02-01',
            outstandingAmount: 500000,
            sellerTaxCode: '2301098313',
            customerTaxCode: '2300328765',
          },
        ]}
        selectedTargets={[]}
        onApply={vi.fn()}
        onClose={vi.fn()}
      />,
    )
    expect(screen.getByText('Phân bổ phiếu thu')).toBeInTheDocument()
    expect(screen.getByText('Chọn theo ưu tiên')).toBeInTheDocument()
  })

  it('renders cancel modal', () => {
    render(
      <ReceiptCancelModal
        isOpen
        onConfirm={vi.fn()}
        onClose={vi.fn()}
        loading={false}
        error={null}
      />,
    )
    expect(screen.getByText('Hủy phiếu thu')).toBeInTheDocument()
    expect(screen.getByText('Lý do hủy')).toBeInTheDocument()
  })

  it('closes cancel modal via scrim', async () => {
    const user = userEvent.setup()
    const onClose = vi.fn()

    render(
      <ReceiptCancelModal
        isOpen
        onConfirm={vi.fn()}
        onClose={onClose}
        loading={false}
        error={null}
      />,
    )

    await user.click(screen.getByRole('button', { name: 'Đóng hộp thoại' }))
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('renders receipt allocations view modal', () => {
    render(
      <ReceiptViewAllocationsModal
        isOpen
        receipt={{
          id: 'receipt-1',
          status: 'APPROVED',
          version: 0,
          receiptNo: 'PT-001',
          receiptDate: '2025-01-05',
          amount: 1000000,
          unallocatedAmount: 200000,
          allocationMode: 'MANUAL',
          allocationStatus: 'PARTIAL',
          allocationPriority: 'ISSUE_DATE',
          method: 'BANK',
          sellerTaxCode: '2301098313',
          customerTaxCode: '2300328765',
          canManage: true,
        }}
        allocations={[
          {
            targetType: 'INVOICE',
            targetId: 'inv-1',
            targetNo: 'HD-001',
            targetDate: '2025-01-01',
            amount: 800000,
          },
        ]}
        onClose={vi.fn()}
      />,
    )
    expect(screen.getByText('Chi tiết phân bổ phiếu thu')).toBeInTheDocument()
    expect(screen.getByText('Số tiền phân bổ')).toBeInTheDocument()
  })

  it('renders advanced options modal', () => {
    render(
      <ReceiptAdvancedModal
        isOpen
        overridePeriodLock
        overrideReason="Cần duyệt gấp"
        onSave={vi.fn()}
        onClose={vi.fn()}
      />,
    )
    expect(screen.getByText('Tùy chọn nâng cao')).toBeInTheDocument()
    expect(screen.getByText('Vượt khóa kỳ khi duyệt')).toBeInTheDocument()
  })

  it('renders receipts page with auth context', () => {
    render(
      <AuthContext.Provider value={baseAuth}>
        <ReceiptsPage />
      </AuthContext.Provider>,
    )
    expect(screen.getByText('Nhập phiếu thu')).toBeInTheDocument()
  })
})
