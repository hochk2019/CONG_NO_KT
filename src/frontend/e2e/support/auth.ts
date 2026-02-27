import { expect, type Page } from '@playwright/test'

export const e2eUsername = process.env.E2E_USERNAME ?? 'admin'
export const e2ePassword = process.env.E2E_PASSWORD ?? 'Sam0905@'

const refreshCookieName = 'congno_refresh'
const refreshCookieDomain = '127.0.0.1'
const onboardingDismissedStorageKey = 'pref.app.onboarding.dismissed.v1'

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

const prepareStableClientState = async (page: Page) => {
  await page.addInitScript((key: string) => {
    window.localStorage.setItem(key, '1')
  }, onboardingDismissedStorageKey)
}

const hasAuthenticatedShell = async (page: Page) => {
  const currentUrl = page.url()
  if (/\/login(?:[/?#]|$)/.test(currentUrl)) {
    return false
  }

  const logoutButton = page.getByRole('button', { name: 'Đăng xuất' }).first()
  const logoutVisible = await logoutButton.isVisible().catch(() => false)
  if (logoutVisible) {
    return true
  }

  const dashboardLinkCount = await page.locator('.app-nav a[href="/dashboard"]').count()
  if (dashboardLinkCount > 0) {
    return true
  }

  return false
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
}

const seedRefreshCookie = async (page: Page) => {
  if (!cachedRefreshCookie) {
    return
  }

  await page.context().addCookies([
    {
      name: cachedRefreshCookie.name,
      value: cachedRefreshCookie.value,
      domain: cachedRefreshCookie.domain,
      path: cachedRefreshCookie.path,
      httpOnly: cachedRefreshCookie.httpOnly,
      secure: cachedRefreshCookie.secure,
      sameSite: cachedRefreshCookie.sameSite,
      expires: cachedRefreshCookie.expires,
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

export const loginAsDefaultUser = async (page: Page) => {
  await prepareStableClientState(page)
  await seedRefreshCookie(page)
  await page.goto('/dashboard')
  await page.waitForLoadState('domcontentloaded')

  if (await hasAuthenticatedShell(page)) {
    await captureRefreshCookie(page)
    return
  }

  if (!/\/login$/.test(page.url())) {
    await page.waitForTimeout(800)
    if (await hasAuthenticatedShell(page)) {
      await captureRefreshCookie(page)
      return
    }
  }

  try {
    await expect(page).toHaveURL(/\/dashboard$/, { timeout: 5_000 })
    if (await hasAuthenticatedShell(page)) {
      await captureRefreshCookie(page)
      return
    }
  } catch {
    // Continue to interactive login flow below when refresh cookie is unavailable/expired.
  }

  const maxAttempts = 8
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
      await expect(page.locator('.app-content').first()).toBeVisible({ timeout: 10_000 })
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
