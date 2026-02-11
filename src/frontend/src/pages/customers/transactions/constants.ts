export const invoiceStatusLabels: Record<string, string> = {
  OPEN: 'Chưa thanh toán',
  PARTIAL: 'Thanh toán một phần',
  PAID: 'Đã thanh toán',
  VOID: 'Đã hủy',
}

export const advanceStatusLabels: Record<string, string> = {
  DRAFT: 'Nháp',
  APPROVED: 'Đã phê duyệt',
  PAID: 'Đã tất toán',
  VOID: 'Đã hủy',
}

export const receiptStatusLabels: Record<string, string> = {
  DRAFT: 'Nháp',
  APPROVED: 'Đã phê duyệt',
  VOID: 'Đã hủy',
}

export const allocationTypeLabels: Record<string, string> = {
  INVOICE: 'Hóa đơn',
  ADVANCE: 'Khoản trả hộ KH',
}

export const DEFAULT_PAGE_SIZE = 10
export const PAGE_SIZE_STORAGE_KEY = 'pref.table.pageSize'
export const CUSTOMER_INVOICE_STATUS_KEY = 'pref.customers.invoiceStatus'
export const CUSTOMER_ADVANCE_STATUS_KEY = 'pref.customers.advanceStatus'
export const CUSTOMER_RECEIPT_STATUS_KEY = 'pref.customers.receiptStatus'

export const quickRangeOptions = [
  { value: '', label: 'Tùy chọn' },
  { value: 'this_month', label: 'Tháng này' },
  { value: 'last_month', label: 'Tháng trước' },
  { value: 'this_quarter', label: 'Quý này' },
  { value: 'last_quarter', label: 'Quý trước' },
]
