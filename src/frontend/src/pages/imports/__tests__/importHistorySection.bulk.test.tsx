import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import ImportHistorySection from '../ImportHistorySection'

const mocks = vi.hoisted(() => ({
  listImportBatches: vi.fn(),
  rollbackImport: vi.fn(),
  cancelImport: vi.fn(),
}))

vi.mock('../../../api/imports', () => ({
  listImportBatches: mocks.listImportBatches,
  rollbackImport: mocks.rollbackImport,
  cancelImport: mocks.cancelImport,
}))

const importTypeLabels = {
  INVOICE: 'Hóa đơn',
  ADVANCE: 'Khoản trả hộ KH',
  RECEIPT: 'Phiếu thu',
}

const historyStatusLabels = {
  STAGING: 'Đang chờ',
  COMMITTED: 'Đã ghi',
  ROLLED_BACK: 'Đã hoàn tác',
  CANCELLED: 'Đã hủy',
}

describe('ImportHistorySection bulk actions', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    window.localStorage.clear()
    mocks.listImportBatches.mockResolvedValue({
      items: [
        {
          batchId: 'BATCH-STAGING',
          type: 'INVOICE',
          status: 'STAGING',
          fileName: 'staging.xlsx',
          createdAt: '2026-03-03T00:00:00.000Z',
          createdBy: 'supervisor',
          summary: {
            insertedInvoices: 0,
            insertedAdvances: 0,
            insertedReceipts: 0,
          },
        },
        {
          batchId: 'BATCH-COMMITTED',
          type: 'INVOICE',
          status: 'COMMITTED',
          fileName: 'committed.xlsx',
          createdAt: '2026-03-02T00:00:00.000Z',
          createdBy: 'supervisor',
          summary: {
            insertedInvoices: 10,
            insertedAdvances: 0,
            insertedReceipts: 0,
          },
        },
      ],
      page: 1,
      pageSize: 10,
      total: 2,
    })
    mocks.cancelImport.mockResolvedValue({})
    mocks.rollbackImport.mockResolvedValue({})
  })

  it('bulk-cancels selected staging batch', async () => {
    const user = userEvent.setup()

    render(
      <ImportHistorySection
        token="token"
        canStage
        canCommit
        importTypeLabels={importTypeLabels}
        historyStatusLabels={historyStatusLabels}
        refreshKey={0}
        onResumeBatch={() => undefined}
      />,
    )

    await waitFor(() => {
      expect(mocks.listImportBatches).toHaveBeenCalled()
    })

    await user.click(screen.getByLabelText('Chọn lô BATCH-STAGING'))
    await user.click(screen.getByRole('button', { name: 'Hủy đã chọn (1)' }))

    expect(screen.getByText('Hủy các lô đã chọn')).toBeInTheDocument()
    await user.type(screen.getByLabelText('Lý do hủy lô'), 'Dữ liệu sai mẫu')
    await user.click(screen.getByRole('button', { name: 'Xác nhận hủy' }))

    await waitFor(() => {
      expect(mocks.cancelImport).toHaveBeenCalledWith({
        token: 'token',
        batchId: 'BATCH-STAGING',
        reason: 'Dữ liệu sai mẫu',
      })
    })
  })
})
