import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import ImportBatchSection from '../ImportBatchSection'

const MAX_IMPORT_FILE_SIZE_BYTES = 20 * 1024 * 1024

const {
  uploadImportMock,
  fetchPreviewMock,
  commitImportMock,
  rollbackImportMock,
  cancelImportMock,
} = vi.hoisted(() => ({
  uploadImportMock: vi.fn(),
  fetchPreviewMock: vi.fn(),
  commitImportMock: vi.fn(),
  rollbackImportMock: vi.fn(),
  cancelImportMock: vi.fn(),
}))

vi.mock('../../../api/imports', () => ({
  uploadImport: uploadImportMock,
  fetchPreview: fetchPreviewMock,
  commitImport: commitImportMock,
  rollbackImport: rollbackImportMock,
  cancelImport: cancelImportMock,
}))

vi.mock('../ImportHistorySection', () => ({
  default: () => <div data-testid="import-history-section" />,
}))

vi.mock('../ImportPreviewModal', () => ({
  default: () => null,
}))

const createDropPayload = (file: File) => ({
  dataTransfer: {
    files: [file],
    items: [],
    types: ['Files'],
    dropEffect: 'copy',
  },
})

const createOversizedXlsx = () =>
  new File([new Uint8Array(MAX_IMPORT_FILE_SIZE_BYTES + 1)], 'invoice-large.xlsx', {
    type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  })

describe('ImportBatchSection drag and drop', () => {
  beforeEach(() => {
    uploadImportMock.mockReset()
    fetchPreviewMock.mockReset()
    commitImportMock.mockReset()
    rollbackImportMock.mockReset()
    cancelImportMock.mockReset()
  })

  it('accepts dropped file and uploads it', async () => {
    const user = userEvent.setup()
    const file = new File(['demo'], 'invoice-import.xlsx', {
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    })

    uploadImportMock.mockResolvedValue({
      batch: { batchId: 'batch-01', status: 'STAGING' },
      staging: { totalRows: 1, okCount: 1, warnCount: 0, errorCount: 0 },
    })

    render(<ImportBatchSection token="token-1" canStage canCommit />)

    const dropzone = screen.getByTestId('import-dropzone')
    fireEvent.dragEnter(dropzone, createDropPayload(file))
    expect(dropzone).toHaveClass('upload-dropzone--active')

    fireEvent.drop(dropzone, createDropPayload(file))
    expect(dropzone).not.toHaveClass('upload-dropzone--active')
    expect(screen.getByText('Đã chọn: invoice-import.xlsx')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Tải file' }))

    await waitFor(() => {
      expect(uploadImportMock).toHaveBeenCalledWith(
        expect.objectContaining({
          token: 'token-1',
          type: 'INVOICE',
          file,
        }),
      )
    })
  })

  it('rejects dropped non-xlsx file before upload', async () => {
    const user = userEvent.setup()
    const textFile = new File(['demo'], 'invoice-import.txt', { type: 'text/plain' })

    render(<ImportBatchSection token="token-1" canStage canCommit />)

    const dropzone = screen.getByTestId('import-dropzone')
    fireEvent.drop(dropzone, createDropPayload(textFile))

    expect(screen.getAllByText('Chỉ hỗ trợ file .xlsx.').length).toBeGreaterThan(0)
    expect(dropzone).toHaveClass('upload-dropzone--error')
    expect(screen.getByText('Chưa có file nào được chọn.')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Tải file' }))
    expect(uploadImportMock).not.toHaveBeenCalled()
  })

  it('rejects oversized xlsx from file input before upload', async () => {
    const user = userEvent.setup()
    const tooLargeFile = createOversizedXlsx()

    render(<ImportBatchSection token="token-1" canStage canCommit />)

    const fileInput = screen.getByLabelText('Chọn file')
    fireEvent.change(fileInput, { target: { files: [tooLargeFile] } })

    expect(screen.getAllByText('File vượt quá 20MB.').length).toBeGreaterThan(0)
    expect(screen.getByTestId('import-dropzone')).toHaveClass('upload-dropzone--error')

    await user.click(screen.getByRole('button', { name: 'Tải file' }))
    expect(uploadImportMock).not.toHaveBeenCalled()
  })

  it('rejects oversized xlsx dropped file before upload', async () => {
    const user = userEvent.setup()
    const tooLargeFile = createOversizedXlsx()

    render(<ImportBatchSection token="token-1" canStage canCommit />)

    const dropzone = screen.getByTestId('import-dropzone')
    fireEvent.drop(dropzone, createDropPayload(tooLargeFile))

    expect(screen.getAllByText('File vượt quá 20MB.').length).toBeGreaterThan(0)
    expect(dropzone).toHaveClass('upload-dropzone--error')

    await user.click(screen.getByRole('button', { name: 'Tải file' }))
    expect(uploadImportMock).not.toHaveBeenCalled()
  })
})
