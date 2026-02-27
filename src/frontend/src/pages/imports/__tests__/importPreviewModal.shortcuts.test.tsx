import { fireEvent, render, screen } from '@testing-library/react'
import type { ComponentProps } from 'react'
import { describe, expect, it, vi } from 'vitest'
import ImportPreviewModal from '../ImportPreviewModal'

const basePreview = {
  page: 2,
  pageSize: 10,
  totalRows: 30,
  okCount: 10,
  warnCount: 10,
  errorCount: 10,
  rows: [
    {
      rowNo: 1,
      validationStatus: 'OK',
      rawData: { invoiceNo: 'HD001' },
      validationMessages: [],
      actionSuggestion: 'INSERT',
    },
  ],
}

const renderModal = (overrides?: Partial<ComponentProps<typeof ImportPreviewModal>>) => {
  const onClose = vi.fn()
  const onPrevPage = vi.fn()
  const onNextPage = vi.fn()

  render(
    <ImportPreviewModal
      isOpen
      onClose={onClose}
      batchId="batch-1"
      previewStatus=""
      onPreviewStatusChange={vi.fn()}
      previewPageSize={10}
      onPreviewPageSizeChange={vi.fn()}
      previewPageSizes={[10, 20]}
      previewLoading={false}
      previewError={null}
      preview={basePreview}
      previewTotalPages={3}
      onPrevPage={onPrevPage}
      onNextPage={onNextPage}
      formatValidationMessages={(messages) => messages.join(', ')}
      previewStatusLabels={{ OK: 'Hợp lệ', WARN: 'Cảnh báo', ERROR: 'Lỗi' }}
      actionSuggestionLabels={{ INSERT: 'Ghi', SKIP: 'Bỏ qua' }}
      {...overrides}
    />,
  )

  return { onClose, onPrevPage, onNextPage }
}

describe('ImportPreviewModal keyboard shortcuts', () => {
  it('closes on Escape', () => {
    const { onClose } = renderModal()

    fireEvent.keyDown(window, { key: 'Escape' })

    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('navigates pages with ArrowLeft/ArrowRight', () => {
    const { onPrevPage, onNextPage } = renderModal()

    fireEvent.keyDown(window, { key: 'ArrowLeft' })
    fireEvent.keyDown(window, { key: 'ArrowRight' })

    expect(onPrevPage).toHaveBeenCalledTimes(1)
    expect(onNextPage).toHaveBeenCalledTimes(1)
  })

  it('navigates pages with Enter and Shift+Enter', () => {
    const { onPrevPage, onNextPage } = renderModal()

    fireEvent.keyDown(window, { key: 'Enter' })
    fireEvent.keyDown(window, { key: 'Enter', shiftKey: true })

    expect(onNextPage).toHaveBeenCalledTimes(1)
    expect(onPrevPage).toHaveBeenCalledTimes(1)
  })

  it('does not trigger shortcuts while focus is inside an interactive field', () => {
    const { onNextPage } = renderModal()
    const statusSelect = screen.getByLabelText('Trạng thái')
    statusSelect.focus()

    fireEvent.keyDown(statusSelect, { key: 'ArrowRight' })
    fireEvent.keyDown(statusSelect, { key: 'Enter' })

    expect(onNextPage).not.toHaveBeenCalled()
  })
})
