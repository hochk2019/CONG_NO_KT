import { useCallback, useMemo, useState } from 'react'

type PaginationOptions = {
  initialPage?: number
  initialPageSize?: number
  initialTotal?: number
}

const normalizePositive = (value: number, fallback: number) => {
  if (!Number.isFinite(value)) return fallback
  const rounded = Math.floor(value)
  return rounded > 0 ? rounded : fallback
}

export function usePagination(options: PaginationOptions = {}) {
  const [page, setPage] = useState(() => normalizePositive(options.initialPage ?? 1, 1))
  const [pageSize, setPageSizeState] = useState(() =>
    normalizePositive(options.initialPageSize ?? 20, 20),
  )
  const [total, setTotal] = useState(() => Math.max(0, Math.floor(options.initialTotal ?? 0)))

  const totalPages = useMemo(() => Math.max(1, Math.ceil(total / pageSize)), [pageSize, total])

  const setPageSafe = useCallback(
    (next: number) => {
      const normalized = normalizePositive(next, 1)
      setPage(Math.min(normalized, totalPages))
    },
    [totalPages],
  )

  const setPageSize = useCallback((next: number) => {
    const normalized = normalizePositive(next, 20)
    setPageSizeState(normalized)
    setPage(1)
  }, [])

  const setTotalSafe = useCallback((next: number) => {
    setTotal(Math.max(0, Math.floor(next)))
  }, [])

  const update = useCallback((next: { page?: number; pageSize?: number; total?: number }) => {
    if (typeof next.pageSize === 'number') {
      const normalizedPageSize = normalizePositive(next.pageSize, 20)
      setPageSizeState(normalizedPageSize)
      setPage(1)
    }

    if (typeof next.total === 'number') {
      setTotal(Math.max(0, Math.floor(next.total)))
    }

    if (typeof next.page === 'number') {
      setPage((prev) => normalizePositive(next.page ?? prev, 1))
    }
  }, [])

  const reset = useCallback(() => setPage(1), [])

  return {
    page,
    pageSize,
    total,
    totalPages,
    setPage: setPageSafe,
    setPageSize,
    setTotal: setTotalSafe,
    update,
    reset,
  }
}
