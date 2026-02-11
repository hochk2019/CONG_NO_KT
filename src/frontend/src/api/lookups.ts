import { apiFetch } from './client'

export type LookupOption = {
  value: string
  label: string
}

export type SellerLookupItem = {
  taxCode: string
  name: string
}

export type CustomerLookupItem = {
  taxCode: string
  name: string
}

export type OwnerLookupItem = {
  id: string
  name: string
  username: string
}

export type UserLookupItem = {
  id: string
  name: string
  username: string
}

export const fetchSellerLookup = async (params: {
  token: string
  search?: string
  limit?: number
}) => {
  const query = new URLSearchParams()
  if (params.search) query.append('search', params.search)
  if (params.limit) query.append('limit', String(params.limit))

  return apiFetch<SellerLookupItem[]>(`/lookups/sellers?${query.toString()}`, {
    token: params.token,
  })
}

export const fetchCustomerLookup = async (params: {
  token: string
  search?: string
  limit?: number
}) => {
  const query = new URLSearchParams()
  if (params.search) query.append('search', params.search)
  if (params.limit) query.append('limit', String(params.limit))

  return apiFetch<CustomerLookupItem[]>(`/lookups/customers?${query.toString()}`, {
    token: params.token,
  })
}

export const fetchOwnerLookup = async (params: {
  token: string
  search?: string
  limit?: number
}) => {
  const query = new URLSearchParams()
  if (params.search) query.append('search', params.search)
  if (params.limit) query.append('limit', String(params.limit))

  return apiFetch<OwnerLookupItem[]>(`/lookups/owners?${query.toString()}`, {
    token: params.token,
  })
}

export const fetchUserLookup = async (params: {
  token: string
  search?: string
  limit?: number
}) => {
  const query = new URLSearchParams()
  if (params.search) query.append('search', params.search)
  if (params.limit) query.append('limit', String(params.limit))

  return apiFetch<UserLookupItem[]>(`/lookups/users?${query.toString()}`, {
    token: params.token,
  })
}

export const mapTaxCodeOptions = (items: { taxCode: string; name: string }[]) => {
  return items.map((item) => ({
    value: item.taxCode,
    label: item.name ? `${item.taxCode} - ${item.name}` : item.taxCode,
  }))
}

export const mapOwnerOptions = (items: OwnerLookupItem[]) => {
  return items.map((item) => ({
    value: item.id,
    label: item.name ? `${item.name} (${item.username})` : item.username,
  }))
}
