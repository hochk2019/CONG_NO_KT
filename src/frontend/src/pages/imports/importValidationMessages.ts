export const validationMessageLabels: Record<string, string> = {
  DUP_IN_DB: 'Trùng hóa đơn đã có trong hệ thống',
  DUP_IN_FILE: 'Trùng trong file',
  BUYER_TAX_REQUIRED: 'Thiếu MST người mua',
  BUYER_NAME_REQUIRED: 'Thiếu tên người mua',
  INVOICE_NO_REQUIRED: 'Thiếu số hóa đơn',
  ISSUE_DATE_REQUIRED: 'Thiếu ngày phát hành',
  SELLER_TAX_REQUIRED: 'Thiếu MST người bán',
  CUSTOMER_TAX_REQUIRED: 'Thiếu MST khách hàng',
  ADVANCE_DATE_REQUIRED: 'Thiếu ngày trả hộ',
  RECEIPT_DATE_REQUIRED: 'Thiếu ngày thu',
  APPLIED_PERIOD_REQUIRED: 'Thiếu kỳ áp dụng',
  APPLIED_PERIOD_NOT_FIRST_DAY: 'Kỳ áp dụng phải là ngày đầu tháng',
  METHOD_INVALID: 'Hình thức thu không hợp lệ',
  AMOUNT_REQUIRED: 'Thiếu số tiền',
  NEGATIVE_AMOUNT: 'Số tiền âm không hợp lệ',
  INFO_REPLACED_INVOICE: 'Hóa đơn thông tin đã bị thay thế, không ghi nhận công nợ',
}

export const formatValidationMessages = (messages: string[]) =>
  messages.map((message) => validationMessageLabels[message] ?? message).join(', ')
