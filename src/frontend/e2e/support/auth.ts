import { expect, type Page } from '@playwright/test'
import fs from 'node:fs'
import path from 'node:path'

export const e2eUsername = process.env.E2E_USERNAME ?? 'admin'
export const e2ePassword = process.env.E2E_PASSWORD ?? 'Sam0905@'

const refreshCookieName = 'congno_refresh'
const refreshCookieDomain = '127.0.0.1'
const onboardingDismissedStorageKey = 'pref.app.onboarding.dismissed.v1'
const authSessionStorageKey = 'cng.auth.session.v1'
const refreshCookieCachePath = path.resolve(process.cwd(), '.tmp', 'e2e-refresh-cookie.json')
const authSessionCachePath = path.resolve(process.cwd(), '.tmp', 'e2e-auth-session.json')

type CachedRefreshCookie = {
  name: string
  value: string
  domain: string
  path: string
  httpOnly: boolean
  secure: boolean
  sameSite: 'Strict' | 'Lax' | 'None'
  expires: number
}

let cachedRefreshCookie: CachedRefreshCookie | null = null
let cachedAuthSessionJson: string | null = null

const hasUsableAuthSession = (sessionJson: string): boolean => {
  try {
    const parsed = JSON.parse(sessionJson) as { expiresAt?: unknown }
    if (typeof parsed.expiresAt !== 'string' || !parsed.expiresAt.trim()) {
      return false
    }
    const expiresAtMs = Date.parse(parsed.expiresAt)
    if (!Number.isFinite(expiresAtMs)) {
      return false
    }
    // Keep a small safety margin; larger margins force unnecessary re-logins.
    return expiresAtMs - Date.now() > 15 * 1000
  } catch {
    return false
  }
}

const ensureCacheDir = () => {
  fs.mkdirSync(path.dirname(refreshCookieCachePath), { recursive: true })
}

const loadCachedRefreshCookieFromDisk = (): CachedRefreshCookie | null => {
  if (cachedRefreshCookie) {
    return cachedRefreshCookie
  }
  if (!fs.existsSync(refreshCookieCachePath)) {
    return null
  }
  try {
    const parsed = JSON.parse(fs.readFileSync(refreshCookieCachePath, 'utf8')) as CachedRefreshCookie
    if (!parsed?.name || !parsed?.value || !parsed?.domain) {
      return null
    }
    if (parsed.expires > 0 && parsed.expires <= Math.floor(Date.now() / 1000)) {
      return null
    }
    cachedRefreshCookie = parsed
    return cachedRefreshCookie
  } catch {
    return null
  }
}

const persistRefreshCookieToDisk = (cookie: CachedRefreshCookie) => {
  ensureCacheDir()
  fs.writeFileSync(refreshCookieCachePath, JSON.stringify(cookie), 'utf8')
}

const loadCachedAuthSessionFromDisk = (): string | null => {
  if (cachedAuthSessionJson) {
    return cachedAuthSessionJson
  }
  if (!fs.existsSync(authSessionCachePath)) {
    return null
  }
  try {
    const raw = fs.readFileSync(authSessionCachePath, 'utf8').trim()
    if (!raw || !hasUsableAuthSession(raw)) {
      fs.rmSync(authSessionCachePath, { force: true })
      return null
    }
    cachedAuthSessionJson = raw
    return cachedAuthSessionJson
  } catch {
    return null
  }
}

const persistAuthSessionToDisk = (sessionJson: string) => {
  if (!hasUsableAuthSession(sessionJson)) {
    return
  }
  ensureCacheDir()
  cachedAuthSessionJson = sessionJson
  fs.writeFileSync(authSessionCachePath, sessionJson, 'utf8')
}

const prepareStableClientState = async (page: Page) => {
  const cachedAuthSession = loadCachedAuthSessionFromDisk()
  await page.addInitScript(
    ({ onboardingKey, authKey, authSession }) => {
      window.localStorage.setItem(onboardingKey, '1')
      if (authSession) {
        window.sessionStorage.setItem(authKey, authSession)
      }
    },
    {
      onboardingKey: onboardingDismissedStorageKey,
      authKey: authSessionStorageKey,
      authSession: cachedAuthSession,
    },
  )
}

const hasAuthenticatedShell = async (page: Page) => {
  const currentUrl = page.url()
  if (/\/login(?:[/?#]|$)/.test(currentUrl)) {
    return false
  }

  const logoutButton = page.getByRole('button', { name: 'Đăng xuất' }).first()
  const logoutVisible = await logoutButton.isVisible().catch(() => false)
  return logoutVisible
}

const captureRefreshCookie = async (page: Page) => {
  const cookies = await page.context().cookies()
  const refreshCookie = cookies.find(
    (cookie) => cookie.name === refreshCookieName && cookie.domain.includes(refreshCookieDomain),
  )
  if (!refreshCookie) {
    return
  }

  cachedRefreshCookie = {
    name: refreshCookie.name,
    value: refreshCookie.value,
    domain: refreshCookie.domain,
    path: refreshCookie.path,
    httpOnly: refreshCookie.httpOnly,
    secure: refreshCookie.secure,
    sameSite: refreshCookie.sameSite,
    // Session cookie can surface as -1; promote to a short-lived persistent cookie for test reuse.
    expires:
      refreshCookie.expires > 0
        ? refreshCookie.expires
        : Math.floor(Date.now() / 1000) + 30 * 60,
  }
  persistRefreshCookieToDisk(cachedRefreshCookie)
}

const captureAuthSession = async (page: Page) => {
  const sessionJson = await page.evaluate((storageKey: string) => {
    return window.sessionStorage.getItem(storageKey)
  }, authSessionStorageKey)
  if (!sessionJson) {
    return
  }
  persistAuthSessionToDisk(sessionJson)
}

const seedRefreshCookie = async (page: Page) => {
  const cookieToSeed = loadCachedRefreshCookieFromDisk()
  if (!cookieToSeed) {
    return
  }

  await page.context().addCookies([
    {
      name: cookieToSeed.name,
      value: cookieToSeed.value,
      domain: cookieToSeed.domain,
      path: cookieToSeed.path,
      httpOnly: cookieToSeed.httpOnly,
      secure: cookieToSeed.secure,
      sameSite: cookieToSeed.sameSite,
      expires: cookieToSeed.expires,
    },
  ])
}

const isRateLimitedAlert = async (page: Page) => {
  const alert = page.getByRole('alert')
  const alertVisible = await alert.isVisible().catch(() => false)
  const alertText = alertVisible ? ((await alert.textContent()) ?? '').trim() : ''
  const isRateLimited = /too many requests|quá nhiều yêu cầu/i.test(alertText)
  return { isRateLimited, alertText }
}

const waitForAuthResolution = async (
  page: Page,
  timeoutMs: number,
): Promise<'authenticated' | 'login' | 'unknown'> => {
  const logoutButton = page.getByRole('button', { name: 'Đăng xuất' }).first()
  const resolution = await Promise.race([
    logoutButton
      .waitFor({ state: 'visible', timeout: timeoutMs })
      .then(() => 'authenticated' as const)
      .catch(() => 'unknown' as const),
    page
      .waitForURL(/\/login(?:[/?#]|$)/, { timeout: timeoutMs })
      .then(() => 'login' as const)
      .catch(() => 'unknown' as const),
  ])

  if (resolution !== 'unknown') {
    return resolution
  }
  if (await hasAuthenticatedShell(page)) {
    return 'authenticated'
  }
  if (/\/login(?:[/?#]|$)/.test(page.url())) {
    return 'login'
  }
  return 'unknown'
}

export const loginAsDefaultUser = async (page: Page) => {
  await prepareStableClientState(page)
  await seedRefreshCookie(page)
  await page.goto('/dashboard')
  await page.waitForLoadState('domcontentloaded')

  const initialResolution = await waitForAuthResolution(page, 10_000)
  if (initialResolution === 'authenticated') {
    await captureAuthSession(page)
    await captureRefreshCookie(page)
    return
  }

  if (initialResolution === 'unknown') {
    // Give client-side bootstrap one extra window before attempting interactive login.
    await page.waitForTimeout(1_000)
    const followupResolution = await waitForAuthResolution(page, 6_000)
    if (followupResolution === 'authenticated') {
      await captureAuthSession(page)
      await captureRefreshCookie(page)
      return
    }
  }

  try {
    await expect(page).toHaveURL(/\/dashboard$/, { timeout: 5_000 })
    if (await hasAuthenticatedShell(page)) {
      await captureAuthSession(page)
      await captureRefreshCookie(page)
      return
    }
  } catch {
    // Continue to interactive login flow below when refresh cookie is unavailable/expired.
  }

  const maxAttempts = 6
  for (let attempt = 1; attempt <= maxAttempts; attempt += 1) {
    await page.goto('/login')
    await page.getByLabel('Tên đăng nhập').fill(e2eUsername)
    await page.getByLabel('Mật khẩu').fill(e2ePassword)
    await page.getByRole('button', { name: 'Đăng nhập' }).click()

    const loginResult = await Promise.race([
      page.waitForURL(/\/dashboard$/, { timeout: 8_000 }).then(() => 'ok' as const),
      page
        .getByRole('alert')
        .waitFor({ state: 'visible', timeout: 8_000 })
        .then(() => 'alert' as const)
        .catch(() => 'timeout' as const),
    ])

    if (loginResult === 'ok') {
      const postLoginResolution = await waitForAuthResolution(page, 10_000)
      if (postLoginResolution !== 'authenticated') {
        if (attempt < maxAttempts) {
          await page.waitForTimeout(1_000)
          continue
        }
        throw new Error(`Login reached /dashboard but auth shell was not ready (resolution=${postLoginResolution})`)
      }
      await captureAuthSession(page)
      await captureRefreshCookie(page)
      return
    }

    const { isRateLimited, alertText } = await isRateLimitedAlert(page)
    if (isRateLimited && attempt < maxAttempts) {
      await page.waitForTimeout(Math.min(attempt * 2_000, 12_000))
      continue
    }

    if (loginResult === 'timeout' && attempt < maxAttempts) {
      await page.waitForTimeout(1_000)
      continue
    }

    throw new Error(`Login failed on attempt ${attempt}: ${alertText || loginResult}`)
  }
}
