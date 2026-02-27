import { useEffect, useState } from 'react'

type UsePersistedStateOptions<T> = {
  storage?: Storage
  serialize?: (value: T) => string
  deserialize?: (raw: string) => T
  validate?: (value: unknown) => value is T
}

const defaultSerialize = <T,>(value: T) => JSON.stringify(value)

const defaultDeserialize = <T,>(raw: string): T => {
  try {
    return JSON.parse(raw) as T
  } catch {
    return raw as unknown as T
  }
}

const resolveInitialValue = <T,>(initialValue: T | (() => T)) =>
  typeof initialValue === 'function' ? (initialValue as () => T)() : initialValue

export function usePersistedState<T>(
  key: string,
  initialValue: T | (() => T),
  options: UsePersistedStateOptions<T> = {},
) {
  const {
    storage = typeof window !== 'undefined' ? window.localStorage : undefined,
    serialize = defaultSerialize,
    deserialize = defaultDeserialize,
    validate,
  } = options

  const [value, setValue] = useState<T>(() => {
    const fallback = resolveInitialValue(initialValue)
    if (!storage) return fallback

    try {
      const raw = storage.getItem(key)
      if (raw === null) return fallback
      const parsed = deserialize(raw)
      if (validate && !validate(parsed)) return fallback
      return parsed
    } catch {
      return fallback
    }
  })

  useEffect(() => {
    if (!storage) return
    try {
      storage.setItem(key, serialize(value))
    } catch {
      // ignore storage write errors
    }
  }, [key, serialize, storage, value])

  return [value, setValue] as const
}
