export type PagedResult<T> = {
  items: T[]
  page: number
  pageSize: number
  total: number
}
