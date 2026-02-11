import { formatApiErrorMessage } from './errorMessages'

export class ApiError extends Error {
  status: number
  detail?: string

  constructor(message: string, status: number, detail?: string) {
    super(message)
    this.status = status
    this.detail = detail
  }
}

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? ''

type FetchOptions = {
  method?: string
  token?: string | null
  body?: unknown
  headers?: Record<string, string>
  signal?: AbortSignal
}

const parseErrorMessage = async (response: Response) => {
  const fallback = response.statusText || 'Request failed'
  try {
    const contentType = response.headers.get('content-type') ?? ''
    if (contentType.includes('json')) {
      const payload = await response.json()
      return formatApiErrorMessage(payload, fallback)
    }
  } catch {
    return fallback
  }
  return fallback
}

const buildRequest = (path: string, options: FetchOptions) => {
  const url = path.startsWith('http') ? path : `${apiBaseUrl}${path}`
  const headers: Record<string, string> = {
    Accept: 'application/json',
    ...options.headers,
  }

  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }

  if (options.token) {
    headers.Authorization = `Bearer ${options.token}`
  }

  return { url, headers }
}

export const apiFetch = async <T>(path: string, options: FetchOptions = {}) => {
  const { url, headers } = buildRequest(path, options)
  const hasBody = options.body !== undefined
  const resolvedBody =
    options.body === undefined
      ? undefined
      : typeof options.body === 'string'
        ? options.body
        : JSON.stringify(options.body)

  const response = await fetch(url, {
    method: options.method ?? (hasBody ? 'POST' : 'GET'),
    headers,
    body: resolvedBody,
    credentials: 'include',
    signal: options.signal,
  })

  if (!response.ok) {
    const message = await parseErrorMessage(response)
    throw new ApiError(message, response.status)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

export const apiFetchBlob = async (path: string, options: FetchOptions = {}) => {
  const { url, headers } = buildRequest(path, options)
  const hasBody = options.body !== undefined
  const resolvedBody =
    options.body === undefined
      ? undefined
      : typeof options.body === 'string'
        ? options.body
        : JSON.stringify(options.body)

  const response = await fetch(url, {
    method: options.method ?? (hasBody ? 'POST' : 'GET'),
    headers,
    body: resolvedBody,
    credentials: 'include',
    signal: options.signal,
  })

  if (!response.ok) {
    const message = await parseErrorMessage(response)
    throw new ApiError(message, response.status)
  }

  const blob = await response.blob()
  return { blob, headers: response.headers }
}
