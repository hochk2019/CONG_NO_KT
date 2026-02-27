import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { vi } from 'vitest'
import { AuthContext, type AuthContextValue } from '../../../context/AuthStore'
import CustomerEditModal from '../CustomerEditModal'
import CustomerListSection from '../CustomerListSection'
import CustomerTransactionModals from '../CustomerTransactionModals'
import CustomerTransactionsSection from '../CustomerTransactionsSection'
import CustomersPage from '../CustomersPage'
import TransactionFilters from '../transactions/TransactionFilters'

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

const detail = {
  taxCode: '2301098313',
  name: 'Công ty Demo',
  address: 'Hà Nội',
  email: 'demo@example.com',
  phone: '0123456789',
  status: 'ACTIVE',
  paymentTermsDays: 30,
  creditLimit: 1000000,
  currentBalance: 500000,
  ownerId: null,
  ownerName: 'Nguyễn Văn A',
  managerId: null,
  managerName: 'Lê Thị B',
  createdAt: '2025-01-01',
  updatedAt: '2025-01-01',
}

describe('customers modules', () => {
  it('renders transaction filters and triggers status change', async () => {
    const user = userEvent.setup()
    const onStatusChange = vi.fn()
    render(
      <TransactionFilters
        searchLabel="Tìm chứng từ"
        searchValue=""
        onSearchChange={vi.fn()}
        dateFrom=""
        dateTo=""
        onDateFromChange={vi.fn()}
        onDateToChange={vi.fn()}
        quickRange=""
        onQuickRangeChange={vi.fn()}
        statusValue=""
        statusOptions={[
          { value: 'PAID', label: 'Đã thanh toán' },
          { value: 'OPEN', label: 'Chưa thanh toán' },
        ]}
        onStatusChange={onStatusChange}
        hasFilters={false}
        onClear={vi.fn()}
        helperText="Chọn bộ lọc"
      />,
    )

    await user.selectOptions(screen.getByLabelText('Trạng thái'), 'PAID')
    expect(onStatusChange).toHaveBeenCalledWith('PAID')
    expect(screen.getByText('Chọn bộ lọc')).toBeInTheDocument()
  })

  it('renders customer edit modal and closes', async () => {
    const user = userEvent.setup()
    const onClose = vi.fn()
    render(
      <CustomerEditModal
        open
        onClose={onClose}
        onCopy={vi.fn()}
        canManageCustomers
        selectedName="Công ty Demo"
        selectedTaxCode="2301098313"
        detail={detail}
        detailLoading={false}
        detailError={null}
        copyMessage={null}
        ownerOptions={[]}
        managerOptions={[]}
        ownerLoading={false}
        managerLoading={false}
        ownerError={null}
        managerError={null}
        editName="Công ty Demo"
        editAddress=""
        editEmail=""
        editPhone=""
        editStatus="ACTIVE"
        editPaymentTermsDays="30"
        editCreditLimit="1000000"
        editOwnerId=""
        editManagerId=""
        setEditName={vi.fn()}
        setEditAddress={vi.fn()}
        setEditEmail={vi.fn()}
        setEditPhone={vi.fn()}
        setEditStatus={vi.fn()}
        setEditPaymentTermsDays={vi.fn()}
        setEditCreditLimit={vi.fn()}
        setEditOwnerId={vi.fn()}
        setEditManagerId={vi.fn()}
        editLoading={false}
        editSuccess={null}
        editError={null}
        statusLabels={{ ACTIVE: 'Đang hoạt động' }}
        onSave={vi.fn()}
        onReset={vi.fn()}
      />,
    )

    expect(screen.getByText('Công ty Demo')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Đóng' }))
    expect(onClose).toHaveBeenCalled()
  })

  it('renders customer list section without token', () => {
    render(
      <CustomerListSection
        token=""
        canManageCustomers={false}
        selectedTaxCode={null}
        selectedName=""
        onSelectCustomer={vi.fn()}
      />,
    )
    expect(screen.getByText('Danh sách khách hàng')).toBeInTheDocument()
  })

  it('renders customer transactions section with tabs', () => {
    render(
      <CustomerTransactionsSection
        token=""
        canManageCustomers={false}
        selectedTaxCode="2301098313"
        selectedName="Công ty Demo"
        onClearSelection={vi.fn()}
      />,
    )
    expect(screen.getByText('Giao dịch khách hàng')).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: 'Hóa đơn' })).toBeInTheDocument()
  })

  it('renders transaction modals for invoice view', () => {
    render(
      <CustomerTransactionModals
        invoiceModal={{
          mode: 'view',
          row: {
            id: 'inv-1',
            invoiceNo: 'INV-1',
            issueDate: '2025-01-01',
            totalAmount: 1000000,
            outstandingAmount: 500000,
            status: 'OPEN',
            version: 1,
            sellerTaxCode: '2301098313',
            sellerShortName: 'Hoàng Minh',
            receiptRefs: [],
          },
        }}
        advanceModal={null}
        receiptModal={null}
        token=""
        invoiceStatusLabels={{ OPEN: 'Chưa thanh toán' }}
        advanceStatusLabels={{}}
        allocationTypeLabels={{}}
        onCloseInvoice={vi.fn()}
        onCloseAdvance={vi.fn()}
        onCloseReceipt={vi.fn()}
        onVoidInvoice={vi.fn()}
        onVoidAdvance={vi.fn()}
        shortId={(value) => value.slice(0, 6)}
        invoiceVoidReason=""
        onInvoiceVoidReasonChange={vi.fn()}
        invoiceReplacementId=""
        onInvoiceReplacementChange={vi.fn()}
        invoiceVoidLoading={false}
        invoiceVoidError={null}
        invoiceVoidSuccess={null}
        advanceVoidReason=""
        onAdvanceVoidReasonChange={vi.fn()}
        advanceOverrideLock={false}
        onAdvanceOverrideLockChange={vi.fn()}
        advanceOverrideReason=""
        onAdvanceOverrideReasonChange={vi.fn()}
        advanceVoidLoading={false}
        advanceVoidError={null}
        advanceVoidSuccess={null}
        receiptAllocations={[]}
        receiptAllocLoading={false}
        receiptAllocError={null}
      />,
    )
    expect(screen.getByText('Phiếu thu liên quan')).toBeInTheDocument()
  })

  it('renders customers page with auth context', () => {
    render(
      <MemoryRouter initialEntries={['/customers']}>
        <AuthContext.Provider value={baseAuth}>
          <CustomersPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )
    expect(screen.getByText('Tra cứu MST và công nợ')).toBeInTheDocument()
  })

  it('applies deep-link taxCode/tab/doc in customers page', () => {
    render(
      <MemoryRouter initialEntries={['/customers?taxCode=2301098313&tab=receipts&doc=PT-001']}>
        <AuthContext.Provider value={baseAuth}>
          <CustomersPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    expect(screen.getByRole('tab', { name: 'Phiếu thu' })).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByRole('textbox', { name: /Tìm chứng từ \(PT \/ HD \/ TH\)/i })).toHaveValue('PT-001')
  })
})
