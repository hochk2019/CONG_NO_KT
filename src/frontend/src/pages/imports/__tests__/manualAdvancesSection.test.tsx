import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import ManualAdvancesSection from '../ManualAdvancesSection'

const mocks = vi.hoisted(() => ({
  approveAdvanceMock: vi.fn(),
  createAdvanceMock: vi.fn(),
  fetchAdvanceHistoryMock: vi.fn(),
  listAdvancesMock: vi.fn(),
  unvoidAdvanceMock: vi.fn(),
  updateAdvanceMock: vi.fn(),
  voidAdvanceMock: vi.fn(),
  fetchCustomerLookupMock: vi.fn(),
  fetchSellerLookupMock: vi.fn(),
  mapTaxCodeOptionsMock: vi.fn(),
}))

vi.mock('../../../api/advances', () => ({
  approveAdvance: mocks.approveAdvanceMock,
  createAdvance: mocks.createAdvanceMock,
  fetchAdvanceHistory: mocks.fetchAdvanceHistoryMock,
  listAdvances: mocks.listAdvancesMock,
  unvoidAdvance: mocks.unvoidAdvanceMock,
  updateAdvance: mocks.updateAdvanceMock,
  voidAdvance: mocks.voidAdvanceMock,
}))

vi.mock('../../../api/lookups', () => ({
  fetchCustomerLookup: mocks.fetchCustomerLookupMock,
  fetchSellerLookup: mocks.fetchSellerLookupMock,
  mapTaxCodeOptions: mocks.mapTaxCodeOptionsMock,
}))

describe('ManualAdvancesSection', () => {
  beforeEach(() => {
    mocks.approveAdvanceMock.mockReset()
    mocks.createAdvanceMock.mockReset()
    mocks.fetchAdvanceHistoryMock.mockReset()
    mocks.listAdvancesMock.mockReset()
    mocks.unvoidAdvanceMock.mockReset()
    mocks.updateAdvanceMock.mockReset()
    mocks.voidAdvanceMock.mockReset()
    mocks.fetchCustomerLookupMock.mockReset()
    mocks.fetchSellerLookupMock.mockReset()
    mocks.mapTaxCodeOptionsMock.mockReset()

    mocks.listAdvancesMock.mockResolvedValue({ items: [], total: 0 })
    mocks.fetchAdvanceHistoryMock.mockResolvedValue([])
    mocks.fetchCustomerLookupMock.mockResolvedValue([])
    mocks.fetchSellerLookupMock.mockResolvedValue([])
    mocks.mapTaxCodeOptionsMock.mockReturnValue([])
  })

  const baseAdvance = {
    id: 'adv-1',
    status: 'DRAFT',
    version: 1,
    advanceNo: 'TH-001',
    advanceDate: '2026-02-12',
    amount: 100000,
    outstandingAmount: 100000,
    sellerTaxCode: '0312345678',
    customerTaxCode: '0101234567',
    description: 'ghi chu cu',
    customerName: 'ACME',
    ownerName: 'Owner',
    sourceType: 'MANUAL',
    canManage: true,
  }

  it('renders the import template CTA in the create header and forwards clicks', async () => {
    const user = userEvent.setup()
    const onImportTemplate = vi.fn()

    render(
      <ManualAdvancesSection
        token="token-advance"
        canApprove={false}
        onImportTemplate={onImportTemplate}
      />,
    )

    const importButton = await screen.findByRole('button', { name: 'Import từ template' })
    expect(importButton.closest('.advances-section-header__actions')).not.toBeNull()

    await user.click(importButton)
    expect(onImportTemplate).toHaveBeenCalledTimes(1)
  })

  it('keeps the advances create and worklist areas compact while marking advance number as required', async () => {
    render(<ManualAdvancesSection token="token-advance" canApprove />)

    await screen.findByRole('button', { name: 'Tạo & duyệt' })

    expect(
      screen.queryByText(
        /Ưu tiên hoàn thành MST bên bán, MST bên mua, số chứng từ, ngày trả hộ và số tiền trước/i,
      ),
    ).not.toBeInTheDocument()
    expect(screen.queryByText('Tạo xong có thể chốt ngay.')).not.toBeInTheDocument()
    expect(document.querySelector('.advances-submit-row__copy')).toBeNull()
    expect(
      screen.queryByText(
        /Tập trung các khoản cần theo dõi, phê duyệt, hủy hoặc bỏ hủy trên cùng một mặt bàn thao tác\./i,
      ),
    ).not.toBeInTheDocument()
    expect(screen.queryByText('Bộ lọc vận hành')).not.toBeInTheDocument()
    expect(
      screen.queryByText(/Lọc nhanh theo đối tượng, trạng thái và chỉ mở rộng khi cần truy vết sâu hơn\./i),
    ).not.toBeInTheDocument()

    const advanceNoInput = screen.getByPlaceholderText('VD: CT-001')
    const advanceNoField = advanceNoInput.closest('label')

    expect(advanceNoField).not.toBeNull()
    expect(within(advanceNoField as HTMLLabelElement).getByText('Bắt buộc')).toBeInTheDocument()
    expect(advanceNoInput).toBeRequired()
  })

  it('requires advance number before creating a draft', async () => {
    const user = userEvent.setup()

    render(<ManualAdvancesSection token="token-advance" canApprove={false} />)

    fireEvent.change(screen.getByLabelText('MST bên bán'), { target: { value: 'SELLER01' } })
    fireEvent.change(screen.getByLabelText('MST bên mua'), { target: { value: 'CUST01' } })
    fireEvent.change(screen.getByLabelText('Ngày trả hộ'), { target: { value: '2026-03-20' } })
    fireEvent.change(screen.getByLabelText('Số tiền'), { target: { value: '100000' } })

    await user.click(screen.getByRole('button', { name: 'Tạo nháp' }))

    expect(mocks.createAdvanceMock).not.toHaveBeenCalled()
    expect(await screen.findByText('Vui lòng nhập số chứng từ.')).toBeInTheDocument()
  })

  it('submits advance correction from the modal', async () => {
    const user = userEvent.setup()
    mocks.listAdvancesMock.mockResolvedValueOnce({ items: [baseAdvance], total: 1 })
    mocks.updateAdvanceMock.mockResolvedValue({
      ...baseAdvance,
      description: 'ghi chu moi',
      version: 2,
    })

    render(<ManualAdvancesSection token="token-advance" canApprove={false} />)

    await user.click(await screen.findByRole('button', { name: 'Sửa' }))

    const dialog = screen.getByRole('dialog')
    await user.clear(within(dialog).getByLabelText('Ghi chú'))
    await user.type(within(dialog).getByLabelText('Ghi chú'), 'ghi chu moi')
    await user.type(within(dialog).getByLabelText('Lý do điều chỉnh'), 'Điều chỉnh test')
    await user.click(within(dialog).getByRole('button', { name: 'Lưu điều chỉnh' }))

    expect(mocks.updateAdvanceMock).toHaveBeenCalledWith(
      'token-advance',
      'adv-1',
      expect.objectContaining({
        description: 'ghi chu moi',
        reason: 'Điều chỉnh test',
        version: 1,
      }),
    )
    await waitFor(() => expect(mocks.listAdvancesMock).toHaveBeenCalledTimes(2))
    expect(await screen.findByText('Đã cập nhật khoản trả hộ adv-1.')).toBeInTheDocument()
  })

  it('allows editing advance correction amount by single dong units', async () => {
    const user = userEvent.setup()
    mocks.listAdvancesMock.mockResolvedValueOnce({
      items: [{ ...baseAdvance, amount: 25554, outstandingAmount: 25554 }],
      total: 1,
    })
    mocks.updateAdvanceMock.mockResolvedValue({
      ...baseAdvance,
      amount: 25551,
      outstandingAmount: 25551,
      version: 2,
    })

    render(<ManualAdvancesSection token="token-advance" canApprove={false} />)

    await user.click(await screen.findByRole('button', { name: 'Sửa' }))

    const dialog = screen.getByRole('dialog')
    const amountInput = within(dialog).getByLabelText('Số tiền')
    expect(amountInput).toHaveAttribute('step', '1')

    await user.clear(amountInput)
    await user.type(amountInput, '25551')
    await user.type(within(dialog).getByLabelText('Lý do điều chỉnh'), 'Điều chỉnh lẻ')
    await user.click(within(dialog).getByRole('button', { name: 'Lưu điều chỉnh' }))

    expect(mocks.updateAdvanceMock).toHaveBeenCalledWith(
      'token-advance',
      'adv-1',
      expect.objectContaining({
        amount: 25551,
        reason: 'Điều chỉnh lẻ',
        version: 1,
      }),
    )
  })

  it('loads advance history into the history modal', async () => {
    const user = userEvent.setup()
    mocks.listAdvancesMock.mockResolvedValueOnce({ items: [baseAdvance], total: 1 })
    mocks.fetchAdvanceHistoryMock.mockResolvedValueOnce([
      {
        id: 'hist-1',
        action: 'CORRECTED',
        entityType: 'ADVANCE',
        entityId: 'adv-1',
        userName: 'tester',
        createdAt: '2026-03-20T10:30:00Z',
        beforeData: '{"description":"ghi chu cu"}',
        afterData: '{"description":"ghi chu moi"}',
      },
    ])

    render(<ManualAdvancesSection token="token-advance" canApprove={false} />)

    await user.click(await screen.findByRole('button', { name: 'Lịch sử sửa' }))

    expect(mocks.fetchAdvanceHistoryMock).toHaveBeenCalledWith('token-advance', 'adv-1')
    expect(await screen.findByText('CORRECTED')).toBeInTheDocument()
    expect(screen.getByText('tester')).toBeInTheDocument()
  })
})
