type ApiErrorPayload = {
  detail?: string
  title?: string
  error?: string
  code?: string
  extensions?: {
    code?: string
    errorCode?: string
  }
}

const errorCodeMessages: Record<string, string> = {
  INVALID_REQUEST: 'Yêu cầu không hợp lệ.',
  INVALID_OPERATION: 'Thao tác không hợp lệ.',
  UNAUTHORIZED: 'Phiên đăng nhập không hợp lệ.',
  FORBIDDEN: 'Bạn không có quyền thực hiện thao tác này.',
  NOT_FOUND: 'Không tìm thấy dữ liệu.',
  CONFLICT: 'Dữ liệu bị trùng hoặc xung đột.',
  CONCURRENCY_CONFLICT: 'Dữ liệu đã thay đổi, vui lòng tải lại.',
  IMPORT_PARSE_FAILED: 'File import không đúng định dạng.',
  SERVER_ERROR: 'Có lỗi hệ thống, vui lòng thử lại.',
}

const resolveCode = (payload?: ApiErrorPayload | null) =>
  payload?.code ?? payload?.extensions?.code ?? payload?.extensions?.errorCode ?? null

const resolveMessage = (payload?: ApiErrorPayload | null) =>
  payload?.detail ?? payload?.title ?? payload?.error ?? null

export const formatApiErrorMessage = (payload: ApiErrorPayload | null, fallback: string) => {
  const code = resolveCode(payload)
  const message = resolveMessage(payload)
  if (code && errorCodeMessages[code]) {
    if (!message) {
      return errorCodeMessages[code]
    }
    if (message.toLowerCase() === errorCodeMessages[code].toLowerCase()) {
      return message
    }
    return `${errorCodeMessages[code]} ${message}`
  }
  if (message) {
    return code ? `${message} (${code})` : message
  }
  return fallback
}

export const parseApiErrorFromText = (text: string) => {
  try {
    const payload = JSON.parse(text) as ApiErrorPayload
    return formatApiErrorMessage(payload, '')
  } catch {
    return null
  }
}
