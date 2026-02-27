import { useCallback, useState } from 'react'

type UseMutationOptions<TResult> = {
  onSuccess?: (result: TResult) => void
  onError?: (error: unknown) => void
}

export function useMutation<TArgs extends unknown[], TResult>(
  mutationFn: (...args: TArgs) => Promise<TResult>,
  options: UseMutationOptions<TResult> = {},
) {
  const [data, setData] = useState<TResult | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<unknown>(null)

  const mutate = useCallback(
    async (...args: TArgs) => {
      setLoading(true)
      setError(null)
      try {
        const result = await mutationFn(...args)
        setData(result)
        options.onSuccess?.(result)
        return result
      } catch (err) {
        setError(err)
        options.onError?.(err)
        throw err
      } finally {
        setLoading(false)
      }
    },
    [mutationFn, options],
  )

  const reset = useCallback(() => {
    setData(null)
    setError(null)
    setLoading(false)
  }, [])

  return { data, loading, error, mutate, setData, reset }
}
