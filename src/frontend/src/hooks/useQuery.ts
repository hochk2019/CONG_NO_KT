import { useCallback, useRef, useState } from 'react'

type UseQueryOptions<TResult> = {
  initialData?: TResult | null
}

export function useQuery<TArgs extends unknown[], TResult>(
  queryFn: (...args: TArgs) => Promise<TResult>,
  options: UseQueryOptions<TResult> = {},
) {
  const [data, setData] = useState<TResult | null>(options.initialData ?? null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<unknown>(null)
  const requestIdRef = useRef(0)

  const execute = useCallback(
    async (...args: TArgs) => {
      requestIdRef.current += 1
      const requestId = requestIdRef.current
      setLoading(true)
      setError(null)
      try {
        const result = await queryFn(...args)
        if (requestIdRef.current === requestId) {
          setData(result)
        }
        return result
      } catch (err) {
        if (requestIdRef.current === requestId) {
          setError(err)
        }
        throw err
      } finally {
        if (requestIdRef.current === requestId) {
          setLoading(false)
        }
      }
    },
    [queryFn],
  )

  const reset = useCallback(() => {
    setData(options.initialData ?? null)
    setError(null)
    setLoading(false)
  }, [options.initialData])

  return { data, loading, error, execute, setData, reset }
}
