import { ApiError, apiFetch } from './client'
import { formatApiErrorMessage, parseApiErrorFromText } from './errorMessages'
import type { PagedResult } from './types'

const parseErrorMessage = async (response: Response, fallback: string) => {
  try {
    const contentType = response.headers.get('content-type') ?? ''
    if (contentType.includes('application/json')) {
      const payload = await response.json()
      return formatApiErrorMessage(payload, fallback)
    }
  } catch {
    return fallback
  }
  return fallback
}

const parseErrorPayload = (text: string) => {
  const message = parseApiErrorFromText(text)
  return message || null
}

export type ImportBatchDto = {
  batchId: string
  status: string
  fileHash?: string
}

export type ImportStagingResult = {
  totalRows: number
  okCount: number
  warnCount: number
  errorCount: number
}

export type ImportUploadResponse = {
  batch: ImportBatchDto
  staging: ImportStagingResult
}

export type ImportPreviewRow = {
  rowNo: number
  validationStatus: string
  rawData: Record<string, unknown>
  validationMessages: string[]
  dedupKey?: string
  actionSuggestion?: string
}

export type ImportPreviewResult = {
  totalRows: number
  okCount: number
  warnCount: number
  errorCount: number
  page: number
  pageSize: number
  rows: ImportPreviewRow[]
}

export type ImportCommitResult = {
  insertedInvoices: number
  insertedAdvances: number
  insertedReceipts: number
  totalEligibleRows?: number
  committedRows?: number
  skippedRows?: number
  progressSteps?: ImportCommitProgressStep[]
}

export type ImportCommitProgressStep = {
  stage: string
  percent: number
  processedRows: number
  totalRows: number
  message: string
}

export type ImportBatchHistoryItem = {
  batchId: string
  type: string
  status: string
  fileName?: string | null
  periodFrom?: string | null
  periodTo?: string | null
  createdAt: string
  createdBy?: string | null
  committedAt?: string | null
  cancelledAt?: string | null
  cancelledBy?: string | null
  cancelReason?: string | null
  summary: ImportCommitResult
}

export const uploadImport = async (params: {
  token: string
  type: string
  file: File
  periodFrom?: string
  periodTo?: string
  idempotencyKey?: string
  onProgress?: (percent: number) => void
}) => {
  const form = new FormData()
  form.append('type', params.type)
  form.append('file', params.file)
  if (params.periodFrom) {
    form.append('periodFrom', params.periodFrom)
  }
  if (params.periodTo) {
    form.append('periodTo', params.periodTo)
  }
  if (params.idempotencyKey) {
    form.append('idempotencyKey', params.idempotencyKey)
  }

  const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? ''

  return new Promise<ImportUploadResponse>((resolve, reject) => {
    const xhr = new XMLHttpRequest()
    xhr.open('POST', `${apiBaseUrl}/imports/upload`)
    xhr.setRequestHeader('Authorization', `Bearer ${params.token}`)
    xhr.withCredentials = true

    if (params.onProgress) {
      xhr.upload.onprogress = (event) => {
        if (!event.lengthComputable) return
        const percent = Math.round((event.loaded / event.total) * 100)
        params.onProgress?.(percent)
      }
    }

    xhr.onload = () => {
      const success = xhr.status >= 200 && xhr.status < 300
      if (success) {
        try {
          const payload = JSON.parse(xhr.responseText) as ImportUploadResponse
          params.onProgress?.(100)
          resolve(payload)
        } catch {
          reject(new ApiError('Không đọc được phản hồi từ máy chủ.', xhr.status))
        }
        return
      }

      const message =
        parseErrorPayload(xhr.responseText) ??
        `Tải file thất bại (HTTP ${xhr.status}).`
      reject(new ApiError(message, xhr.status))
    }

    xhr.onerror = () => {
      reject(new ApiError('Không tải được file. Vui lòng thử lại.', 0))
    }

    xhr.send(form)
  })
}

export const fetchPreview = async (params: {
  token: string
  batchId: string
  status?: string
  page: number
  pageSize: number
}) => {
  const query = new URLSearchParams({
    page: String(params.page),
    pageSize: String(params.pageSize),
  })
  if (params.status) {
    query.append('status', params.status)
  }

  const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
  const response = await fetch(
    `${apiBaseUrl}/imports/${params.batchId}/preview?${query.toString()}`,
    {
      headers: {
        Accept: 'application/json',
        Authorization: `Bearer ${params.token}`,
      },
    },
  )

  if (!response.ok) {
    const message = await parseErrorMessage(
      response,
      `Không lấy được xem trước (HTTP ${response.status}).`,
    )
    throw new ApiError(message, response.status)
  }

  return (await response.json()) as ImportPreviewResult
}

export const commitImport = async (params: {
  token: string
  batchId: string
  idempotencyKey?: string
  overridePeriodLock?: boolean
  overrideReason?: string
}) => {
  const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
  const response = await fetch(`${apiBaseUrl}/imports/${params.batchId}/commit`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${params.token}`,
    },
    body: JSON.stringify({
      idempotency_key: params.idempotencyKey ?? null,
      override_period_lock: params.overridePeriodLock ?? false,
      override_reason: params.overrideReason ?? null,
    }),
  })

  if (!response.ok) {
    const message = await parseErrorMessage(
      response,
      `Ghi dữ liệu thất bại (HTTP ${response.status}).`,
    )
    throw new ApiError(message, response.status)
  }

  return (await response.json()) as ImportCommitResult
}

export const rollbackImport = async (params: {
  token: string
  batchId: string
  overridePeriodLock?: boolean
  overrideReason?: string
}) => {
  const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
  const response = await fetch(`${apiBaseUrl}/imports/${params.batchId}/rollback`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${params.token}`,
    },
    body: JSON.stringify({
      override_period_lock: params.overridePeriodLock ?? false,
      override_reason: params.overrideReason ?? null,
    }),
  })

  if (!response.ok) {
    const message = await parseErrorMessage(
      response,
      `Hoàn tác thất bại (HTTP ${response.status}).`,
    )
    throw new ApiError(message, response.status)
  }

  return response.json()
}

export const cancelImport = async (params: {
  token: string
  batchId: string
  reason?: string
}) => {
  const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
  const response = await fetch(`${apiBaseUrl}/imports/${params.batchId}/cancel`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${params.token}`,
    },
    body: JSON.stringify({
      reason: params.reason ?? null,
    }),
  })

  if (!response.ok) {
    const message = await parseErrorMessage(
      response,
      `Hủy lô thất bại (HTTP ${response.status}).`,
    )
    throw new ApiError(message, response.status)
  }

  return response.json()
}

export const listImportBatches = async (params: {
  token: string
  type?: string
  status?: string
  search?: string
  page: number
  pageSize: number
}) => {
  const query = new URLSearchParams({
    page: String(params.page),
    pageSize: String(params.pageSize),
  })
  if (params.type) query.append('type', params.type)
  if (params.status) query.append('status', params.status)
  if (params.search) query.append('search', params.search)

  return apiFetch<PagedResult<ImportBatchHistoryItem>>(`/imports/batches?${query.toString()}`, {
    token: params.token,
  })
}
