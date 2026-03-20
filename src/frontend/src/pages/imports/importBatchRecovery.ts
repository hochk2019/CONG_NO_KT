export type ImportBatchQualitySnapshot = {
  totalRows: number
  okCount: number
  warnCount: number
  errorCount: number
}

export type ImportBatchRecoveryState = {
  status: 'idle' | 'loading' | 'blocked' | 'review' | 'ready'
  previewButtonLabel: string
  hasBlockingErrors: boolean
  errorCount: number
  warnCount: number
  title: string | null
  message: string | null
  commitBlockedReason: string | null
}

type GetImportBatchRecoveryStateParams = {
  batchId: string
  summary: ImportBatchQualitySnapshot | null
  previewLoading: boolean
}

export const getImportBatchRecoveryState = ({
  batchId,
  summary,
  previewLoading,
}: GetImportBatchRecoveryStateParams): ImportBatchRecoveryState => {
  if (!batchId) {
    return {
      status: 'idle',
      previewButtonLabel: 'Xem trước',
      hasBlockingErrors: false,
      errorCount: 0,
      warnCount: 0,
      title: null,
      message: null,
      commitBlockedReason: null,
    }
  }

  if (previewLoading) {
    return {
      status: 'loading',
      previewButtonLabel: 'Đang tải…',
      hasBlockingErrors: false,
      errorCount: 0,
      warnCount: 0,
      title: 'Đang tải chi tiết lô nhập liệu…',
      message: 'Hệ thống đang nạp lại kết quả kiểm tra để kế toán rà soát trước khi ghi dữ liệu.',
      commitBlockedReason: null,
    }
  }

  if (!summary) {
    return {
      status: 'review',
      previewButtonLabel: 'Mở xem trước',
      hasBlockingErrors: false,
      errorCount: 0,
      warnCount: 0,
      title: 'Mở lô để kiểm tra trước khi ghi dữ liệu.',
      message: 'Hãy xem trước dữ liệu để kiểm tra lỗi hoặc cảnh báo trước khi ghi vào hệ thống.',
      commitBlockedReason: null,
    }
  }

  if (summary.errorCount > 0) {
    return {
      status: 'blocked',
      previewButtonLabel: 'Xem lỗi',
      hasBlockingErrors: true,
      errorCount: summary.errorCount,
      warnCount: summary.warnCount,
      title: `Lô này còn ${summary.errorCount} dòng lỗi nên chưa thể ghi dữ liệu.`,
      message:
        'Mở xem trước để xem từng dòng lỗi, chỉnh file nguồn rồi tải lại trước khi ghi. Nếu không dùng lô này nữa, hãy hủy lô.',
      commitBlockedReason: `Lô ${batchId} còn ${summary.errorCount} dòng lỗi nên chưa thể ghi dữ liệu.`,
    }
  }

  if (summary.warnCount > 0) {
    return {
      status: 'review',
      previewButtonLabel: 'Rà soát cảnh báo',
      hasBlockingErrors: false,
      errorCount: 0,
      warnCount: summary.warnCount,
      title: `Lô không còn lỗi chặn ghi, nhưng còn ${summary.warnCount} dòng cảnh báo.`,
      message: 'Kế toán nên rà soát các cảnh báo trước khi ghi để tránh phải hoàn tác hoặc nhập lại.',
      commitBlockedReason: null,
    }
  }

  return {
    status: 'ready',
    previewButtonLabel: 'Xem trước lần cuối',
    hasBlockingErrors: false,
    errorCount: 0,
    warnCount: 0,
    title: 'Lô đã sẵn sàng để ghi dữ liệu.',
    message: 'Không có dòng lỗi chặn ghi. Bạn có thể xem trước lần cuối rồi ghi dữ liệu.',
    commitBlockedReason: null,
  }
}
