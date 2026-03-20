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
          batchId: 'BATCH-BLOCKED',
          type: 'INVOICE',
          status: 'STAGING',
          fileName: 'blocked.xlsx',
          createdAt: '2026-03-03T00:00:00.000Z',
          createdBy: 'supervisor',
          stagingSummary: {
            totalRows: 4,
            okCount: 2,
            warnCount: 1,
            errorCount: 1,
          },
          summary: {
            insertedInvoices: 0,
            insertedAdvances: 0,
            insertedReceipts: 0,
          },
        },
        {
          batchId: 'BATCH-REVIEW',
          type: 'INVOICE',
          status: 'STAGING',
          fileName: 'review.xlsx',
          createdAt: '2026-03-03T01:00:00.000Z',
          createdBy: 'supervisor',
          stagingSummary: {
            totalRows: 5,
            okCount: 3,
            warnCount: 2,
            errorCount: 0,
          },
          summary: {
            insertedInvoices: 0,
            insertedAdvances: 0,
            insertedReceipts: 0,
          },
        },
        {
          batchId: 'BATCH-READY',
          type: 'INVOICE',
          status: 'STAGING',
          fileName: 'ready.xlsx',
          createdAt: '2026-03-03T02:00:00.000Z',
          createdBy: 'supervisor',
          stagingSummary: {
            totalRows: 6,
            okCount: 6,
            warnCount: 0,
            errorCount: 0,
          },
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
      total: 4,
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

    await user.click(screen.getByLabelText('Chọn lô BATCH-BLOCKED'))
    await user.click(screen.getByRole('button', { name: 'Hủy đã chọn (1)' }))

    expect(screen.getByText('Hủy các lô đã chọn')).toBeInTheDocument()
    await user.type(screen.getByLabelText('Lý do hủy lô'), 'Dữ liệu sai mẫu')
    await user.click(screen.getByRole('button', { name: 'Xác nhận hủy' }))

    await waitFor(() => {
      expect(mocks.cancelImport).toHaveBeenCalledWith({
        token: 'token',
        batchId: 'BATCH-BLOCKED',
        reason: 'Dữ liệu sai mẫu',
      })
    })
  })

  it('shows next-best actions for blocked, review, and ready staging batches', async () => {
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

    expect(await screen.findByText('1 lỗi cần sửa')).toBeInTheDocument()
    expect(await screen.findByText('2 cảnh báo cần rà soát')).toBeInTheDocument()
    expect(await screen.findByText('Sẵn sàng ghi dữ liệu')).toBeInTheDocument()
    expect(await screen.findByText('Tổng 4 · Hợp lệ 2 · Cảnh báo 1 · Lỗi 1')).toBeInTheDocument()
    expect(await screen.findByRole('button', { name: 'Xem lỗi' })).toBeInTheDocument()
    expect(await screen.findByRole('button', { name: 'Rà soát cảnh báo' })).toBeInTheDocument()
    expect(await screen.findByRole('button', { name: 'Mở để ghi' })).toBeInTheDocument()
  })
})
