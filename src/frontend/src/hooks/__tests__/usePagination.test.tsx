import { act, renderHook } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { usePagination } from '../usePagination'

describe('usePagination', () => {
  it('computes total pages and bounds current page', () => {
    const { result } = renderHook(() => usePagination({ initialPage: 1, initialPageSize: 10 }))

    act(() => {
      result.current.setTotal(95)
    })

    expect(result.current.totalPages).toBe(10)

    act(() => {
      result.current.setPage(100)
    })

    expect(result.current.page).toBe(10)
  })

  it('resets page to 1 when page size changes', () => {
    const { result } = renderHook(() => usePagination({ initialPage: 3, initialPageSize: 20 }))

    act(() => {
      result.current.setPageSize(50)
    })

    expect(result.current.page).toBe(1)
    expect(result.current.pageSize).toBe(50)
  })
})
