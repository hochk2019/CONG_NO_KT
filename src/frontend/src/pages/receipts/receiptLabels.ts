export const receiptStatusLabels: Record<string, string> = {
  DRAFT: 'Nháp',
  APPROVED: 'Đã duyệt',
  VOID: 'Đã hủy',
}

export const allocationStatusLabels: Record<string, string> = {
  UNALLOCATED: 'Treo',
  SELECTED: 'Đã chọn',
  SUGGESTED: 'Chờ duyệt',
  PARTIAL: 'Phân bổ một phần',
  ALLOCATED: 'Đã phân bổ',
  VOID: 'Đã hủy',
}

export const allocationPriorityLabels: Record<string, string> = {
  ISSUE_DATE: 'Ngày chứng từ',
  DUE_DATE: 'Ngày đến hạn',
}

export const methodLabels: Record<string, string> = {
  BANK: 'Chuyển khoản',
  CASH: 'Tiền mặt',
  OTHER: 'Khác',
}

export const targetTypeLabels: Record<string, string> = {
  INVOICE: 'Hóa đơn',
  ADVANCE: 'Khoản trả hộ KH',
}
