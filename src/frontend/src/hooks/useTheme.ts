import { useEffect, useMemo, useState } from 'react'
import { usePersistedState } from './usePersistedState'

export type ThemePreference = 'light' | 'dark' | 'system'
export type ResolvedTheme = 'light' | 'dark'

const storageKey = 'app.theme.preference'
const mediaQuery = '(prefers-color-scheme: dark)'

const isThemePreference = (value: unknown): value is ThemePreference =>
  value === 'light' || value === 'dark' || value === 'system'

const getSystemTheme = (): ResolvedTheme => {
  if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
    return 'light'
  }

  return window.matchMedia(mediaQuery).matches ? 'dark' : 'light'
}

const applyThemeToDocument = (theme: ResolvedTheme) => {
  if (typeof document === 'undefined') return

  const root = document.documentElement
  root.setAttribute('data-theme', theme)
  root.style.colorScheme = theme
}

export const bootstrapTheme = () => {
  if (typeof window === 'undefined') return

  let preference: ThemePreference = 'system'
  const raw = window.localStorage.getItem(storageKey)
  if (raw) {
    try {
      const parsed = JSON.parse(raw)
      if (isThemePreference(parsed)) {
        preference = parsed
      }
    } catch {
      if (isThemePreference(raw)) {
        preference = raw
      }
    }
  }

  const resolved = preference === 'system' ? getSystemTheme() : preference
  applyThemeToDocument(resolved)
}

export function useTheme() {
  const [preference, setPreference] = usePersistedState<ThemePreference>(storageKey, 'system', {
    validate: isThemePreference,
  })
  const [systemTheme, setSystemTheme] = useState<ResolvedTheme>(() => getSystemTheme())

  useEffect(() => {
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') return

    const media = window.matchMedia(mediaQuery)
    const onChange = (event: MediaQueryListEvent) => {
      setSystemTheme(event.matches ? 'dark' : 'light')
    }

    if (typeof media.addEventListener === 'function') {
      media.addEventListener('change', onChange)
      return () => media.removeEventListener('change', onChange)
    }

    media.addListener(onChange)
    return () => media.removeListener(onChange)
  }, [])

  const resolvedTheme = useMemo<ResolvedTheme>(
    () => (preference === 'system' ? systemTheme : preference),
    [preference, systemTheme],
  )

  useEffect(() => {
    applyThemeToDocument(resolvedTheme)
  }, [resolvedTheme])

  return {
    preference,
    setPreference,
    resolvedTheme,
  } as const
}
