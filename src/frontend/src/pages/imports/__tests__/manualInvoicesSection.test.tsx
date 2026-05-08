import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, vi } from 'vitest'
import ManualInvoicesSection from '../ManualInvoicesSection'

const { uploadImportMock, commitImportMock, fetchCustomerLookupMock, fetchSellerLookupMock } =
  vi.hoisted(() => ({
  uploadImportMock: vi.fn(),
  commitImportMock: vi.fn(),
  fetchCustomerLookupMock: vi.fn(),
  fetchSellerLookupMock: vi.fn(),
}))

vi.mock('../../../api/imports', () => ({
  uploadImport: uploadImportMock,
  commitImport: commitImportMock,
}))

vi.mock('../../../api/lookups', async () => {
  const actual = await vi.importActual<typeof import('../../../api/lookups')>('../../../api/lookups')

  return {
    ...actual,
    fetchCustomerLookup: fetchCustomerLookupMock,
    fetchSellerLookup: fetchSellerLookupMock,
  }
})

const fillRequiredFields = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.type(screen.getByLabelText('MST bên bán'), '0312345678')
  await user.type(screen.getByLabelText('MST bên mua'), '0101234567')
  await user.type(screen.getByLabelText('Số hóa đơn'), 'INV-2026-0001')
  fireEvent.change(screen.getByLabelText('Ngày phát hành'), { target: { value: '2026-03-05' } })
  fireEvent.change(screen.getByLabelText('Doanh số chưa thuế'), { target: { value: '1000000' } })
  fireEvent.change(screen.getByLabelText('Tiền VAT'), { target: { value: '100000' } })
}

describe('ManualInvoicesSection', () => {
  beforeEach(() => {
    uploadImportMock.mockReset()
    commitImportMock.mockReset()
    fetchCustomerLookupMock.mockReset()
    fetchSellerLookupMock.mockReset()

    fetchCustomerLookupMock.mockResolvedValue([])
    fetchSellerLookupMock.mockResolvedValue([])
  })

  it('validates required invoice fields before upload', async () => {
    const user = userEvent.setup()
    render(<ManualInvoicesSection token="token-1" canCommit />)

    await user.click(screen.getByRole('button', { name: 'Tạo lô nháp' }))

    expect(uploadImportMock).not.toHaveBeenCalled()
    expect(screen.getByText('Vui lòng kiểm tra lại thông tin hóa đơn.')).toBeInTheDocument()
    expect(screen.getByText('Vui lòng nhập MST bên bán.')).toBeInTheDocument()
    expect(screen.getByText('Vui lòng nhập MST bên mua.')).toBeInTheDocument()
  })

  it('renders import template CTA when centralized import handler is provided', () => {
    const onImportTemplate = vi.fn()
    render(
      <ManualInvoicesSection token="token-1" canCommit onImportTemplate={onImportTemplate} />,
    )

    expect(screen.getByRole('button', { name: 'Import từ Template' })).toBeInTheDocument()
  })

  it('stages one manual invoice via import pipeline', async () => {
    const user = userEvent.setup()
    uploadImportMock.mockResolvedValue({
      batch: { batchId: 'batch-manual-1', status: 'STAGING' },
      staging: { totalRows: 1, okCount: 1, warnCount: 0, errorCount: 0 },
    })

    render(<ManualInvoicesSection token="token-1" canCommit />)
    await fillRequiredFields(user)
    await user.click(screen.getByRole('button', { name: 'Tạo lô nháp' }))

    await waitFor(() => expect(uploadImportMock).toHaveBeenCalledTimes(1))
    expect(commitImportMock).not.toHaveBeenCalled()

    const uploadArgs = uploadImportMock.mock.calls[0]?.[0]
    expect(uploadArgs).toEqual(
      expect.objectContaining({
        token: 'token-1',
        type: 'INVOICE',
        periodFrom: '2026-03-01',
        periodTo: '2026-03-31',
      }),
    )
    expect(uploadArgs.file).toBeInstanceOf(File)
    expect(uploadArgs.file.name).toMatch(/^manual-invoice-.*\.xlsx$/)

    expect(
      await screen.findByText('Đã tạo lô nháp batch-manual-1. Bạn có thể kiểm tra lại trước khi ghi dữ liệu.'),
    ).toBeInTheDocument()
    expect(screen.getByText(/Batch:/)).toBeInTheDocument()
    expect(screen.getByText('batch-manual-1')).toBeInTheDocument()
  })

  it('commits staged manual invoice when user has commit permission', async () => {
    const user = userEvent.setup()
    uploadImportMock.mockResolvedValue({
      batch: { batchId: 'batch-manual-2', status: 'STAGING' },
      staging: { totalRows: 1, okCount: 1, warnCount: 0, errorCount: 0 },
    })
    commitImportMock.mockResolvedValue({
      insertedInvoices: 1,
      insertedAdvances: 0,
      insertedReceipts: 0,
    })

    render(<ManualInvoicesSection token="token-2" canCommit />)
    await fillRequiredFields(user)
    await user.click(screen.getByRole('button', { name: 'Nhập và ghi dữ liệu' }))

    await waitFor(() => {
      expect(uploadImportMock).toHaveBeenCalledTimes(1)
      expect(commitImportMock).toHaveBeenCalledWith(
        expect.objectContaining({
          token: 'token-2',
          batchId: 'batch-manual-2',
        }),
      )
    })

    expect(
      await screen.findByText('Đã ghi 1 hóa đơn vào hệ thống (batch batch-manual-2).'),
    ).toBeInTheDocument()
  })

  it('uses quick lookup inputs for seller and customer tax codes', async () => {
    fetchSellerLookupMock.mockResolvedValue([{ taxCode: '0312345678', name: 'Công ty bán A' }])
    fetchCustomerLookupMock.mockResolvedValue([{ taxCode: '0101234567', name: 'Công ty mua B' }])

    render(<ManualInvoicesSection token="token-lookup" canCommit />)

    await waitFor(() => {
      expect(fetchSellerLookupMock).toHaveBeenCalledWith({
        token: 'token-lookup',
        search: undefined,
        limit: 200,
      })
      expect(fetchCustomerLookupMock).toHaveBeenCalledWith({
        token: 'token-lookup',
        search: undefined,
        limit: 200,
      })
    })

    expect(screen.getByLabelText('MST bên bán')).toHaveAttribute('list')
    expect(screen.getByLabelText('MST bên mua')).toHaveAttribute('list')
    expect(screen.getByText('0312345678 - Công ty bán A')).toBeInTheDocument()
    expect(screen.getByText('0101234567 - Công ty mua B')).toBeInTheDocument()
  })

  it('auto-fills customer name after choosing an existing customer tax code', async () => {
    const user = userEvent.setup()
    fetchCustomerLookupMock.mockResolvedValue([{ taxCode: '0101234567', name: 'Công ty mua B' }])

    render(<ManualInvoicesSection token="token-lookup" canCommit />)

    await waitFor(() => expect(fetchCustomerLookupMock).toHaveBeenCalled())

    await user.clear(screen.getByLabelText('MST bên mua'))
    await user.type(screen.getByLabelText('MST bên mua'), '0101234567')

    await waitFor(() => {
      expect(screen.getByLabelText('Tên bên mua')).toHaveValue('Công ty mua B')
    })
  })
})
