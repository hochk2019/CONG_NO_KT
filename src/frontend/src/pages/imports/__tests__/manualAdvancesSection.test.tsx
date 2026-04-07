import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import ManualAdvancesSection from '../ManualAdvancesSection'

const mocks = vi.hoisted(() => ({
  approveAdvanceMock: vi.fn(),
  createAdvanceMock: vi.fn(),
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
    mocks.listAdvancesMock.mockReset()
    mocks.unvoidAdvanceMock.mockReset()
    mocks.updateAdvanceMock.mockReset()
    mocks.voidAdvanceMock.mockReset()
    mocks.fetchCustomerLookupMock.mockReset()
    mocks.fetchSellerLookupMock.mockReset()
    mocks.mapTaxCodeOptionsMock.mockReset()

    mocks.listAdvancesMock.mockResolvedValue({ items: [], total: 0 })
    mocks.fetchCustomerLookupMock.mockResolvedValue([])
    mocks.fetchSellerLookupMock.mockResolvedValue([])
    mocks.mapTaxCodeOptionsMock.mockReturnValue([])
  })

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
})
