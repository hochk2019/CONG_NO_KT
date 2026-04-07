import { describe, expect, it } from 'vitest'
import { getImportBatchRecoveryState } from '../importBatchRecovery'

describe('getImportBatchRecoveryState', () => {
  it('returns idle state when there is no active batch', () => {
    expect(
      getImportBatchRecoveryState({
        batchId: '',
        summary: null,
        previewLoading: false,
      }),
    ).toMatchObject({
      status: 'idle',
      previewButtonLabel: 'Xem trước',
      hasBlockingErrors: false,
    })
  })

  it('returns loading state while an active batch is reloading preview data', () => {
    expect(
      getImportBatchRecoveryState({
        batchId: 'batch-01',
        summary: null,
        previewLoading: true,
      }),
    ).toMatchObject({
      status: 'loading',
      previewButtonLabel: 'Đang tải…',
      hasBlockingErrors: false,
    })
  })

  it('returns blocked state when there are validation errors', () => {
    expect(
      getImportBatchRecoveryState({
        batchId: 'batch-err',
        summary: {
          totalRows: 5,
          okCount: 2,
          warnCount: 1,
          errorCount: 2,
        },
        previewLoading: false,
      }),
    ).toMatchObject({
      status: 'blocked',
      previewButtonLabel: 'Xem lỗi',
      hasBlockingErrors: true,
      errorCount: 2,
    })
  })

  it('returns review state when there are only warnings', () => {
    expect(
      getImportBatchRecoveryState({
        batchId: 'batch-warn',
        summary: {
          totalRows: 4,
          okCount: 3,
          warnCount: 1,
          errorCount: 0,
        },
        previewLoading: false,
      }),
    ).toMatchObject({
      status: 'review',
      previewButtonLabel: 'Rà soát cảnh báo',
      hasBlockingErrors: false,
      warnCount: 1,
    })
  })

  it('returns ready state when the batch has no blocking issues', () => {
    expect(
      getImportBatchRecoveryState({
        batchId: 'batch-ready',
        summary: {
          totalRows: 4,
          okCount: 4,
          warnCount: 0,
          errorCount: 0,
        },
        previewLoading: false,
      }),
    ).toMatchObject({
      status: 'ready',
      previewButtonLabel: 'Xem trước',
      hasBlockingErrors: false,
    })
  })
})
