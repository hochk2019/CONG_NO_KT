import { render, screen } from '@testing-library/react'
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
})
