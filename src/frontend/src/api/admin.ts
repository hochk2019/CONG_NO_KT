import { apiFetch } from './client'
import type { PagedResult } from './types'

export type AdminUser = {
  id: string
  username: string
  fullName?: string | null
  email?: string | null
  phone?: string | null
  isActive: boolean
  zaloUserId?: string | null
  zaloLinkedAt?: string | null
  roles: string[]
}

export type AdminRole = {
  id: number
  code: string
  name: string
}

export type AdminUserCreateRequest = {
  username: string
  password: string
  fullName?: string | null
  email?: string | null
  phone?: string | null
  isActive?: boolean
  roles: string[]
}

export type AdminUserCreateResponse = {
  id: string
}

export type AuditLogItem = {
  id: string
  action: string
  entityType: string
  entityId: string
  userName?: string | null
  createdAt: string
  beforeData?: string | null
  afterData?: string | null
}

export const fetchAdminUsers = async (params: {
  token: string
  search?: string
  page: number
  pageSize: number
}) => {
  const query = new URLSearchParams({
    page: String(params.page),
    pageSize: String(params.pageSize),
  })
  if (params.search) query.append('search', params.search)

  return apiFetch<PagedResult<AdminUser>>(`/admin/users?${query.toString()}`, {
    token: params.token,
  })
}

export const fetchAdminRoles = async (token: string) => {
  return apiFetch<AdminRole[]>('/admin/roles', { token })
}

export const createAdminUser = async (
  token: string,
  payload: AdminUserCreateRequest,
) => {
  return apiFetch<AdminUserCreateResponse>('/admin/users', {
    method: 'POST',
    token,
    body: payload,
  })
}

export const updateUserRoles = async (
  token: string,
  userId: string,
  roles: string[],
) => {
  return apiFetch<void>(`/admin/users/${userId}/roles`, {
    method: 'PUT',
    token,
    body: { roles },
  })
}

export const updateUserStatus = async (
  token: string,
  userId: string,
  isActive: boolean,
) => {
  return apiFetch<void>(`/admin/users/${userId}/status`, {
    method: 'PUT',
    token,
    body: { isActive },
  })
}

export const updateUserZalo = async (
  token: string,
  userId: string,
  zaloUserId: string | null,
) => {
  return apiFetch<void>(`/admin/users/${userId}/zalo`, {
    method: 'PUT',
    token,
    body: { zaloUserId },
  })
}

export const fetchAuditLogs = async (params: {
  token: string
  entityType?: string
  entityId?: string
  action?: string
  from?: string
  to?: string
  page: number
  pageSize: number
}) => {
  const query = new URLSearchParams({
    page: String(params.page),
    pageSize: String(params.pageSize),
  })
  if (params.entityType) query.append('entityType', params.entityType)
  if (params.entityId) query.append('entityId', params.entityId)
  if (params.action) query.append('action', params.action)
  if (params.from) query.append('from', params.from)
  if (params.to) query.append('to', params.to)

  return apiFetch<PagedResult<AuditLogItem>>(`/admin/audit-logs?${query.toString()}`, {
    token: params.token,
  })
}
