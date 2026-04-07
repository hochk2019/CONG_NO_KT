import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { chromium, type FullConfig } from '@playwright/test'

const e2eUsername = process.env.E2E_USERNAME ?? 'admin'
const e2ePassword = process.env.E2E_PASSWORD ?? 'Sam0905@'
const onboardingDismissedStorageKey = 'pref.app.onboarding.dismissed.v1'

const currentFilePath = fileURLToPath(import.meta.url)
const currentDirPath = path.dirname(currentFilePath)
const authFilePath = path.resolve(currentDirPath, '.auth', 'user.json')

const ensureAuthDir = () => {
  fs.mkdirSync(path.dirname(authFilePath), { recursive: true })
}

async function globalSetup(config: FullConfig) {
  ensureAuthDir()

  const baseURL = config.projects[0]?.use?.baseURL
  if (typeof baseURL !== 'string' || !baseURL) {
    throw new Error('Playwright baseURL is required for global auth setup.')
  }

  const browser = await chromium.launch()
  const context = await browser.newContext({ baseURL })
  const page = await context.newPage()

  await page.addInitScript((storageKey: string) => {
    window.localStorage.setItem(storageKey, '1')
  }, onboardingDismissedStorageKey)

  await page.goto('/login')
  await page.getByLabel('Tên đăng nhập').fill(e2eUsername)
  await page.getByLabel('Mật khẩu').fill(e2ePassword)
  await page.getByRole('button', { name: 'Đăng nhập' }).click()
  await page.waitForURL(/\/dashboard$/, { timeout: 15_000 })
  await page.waitForLoadState('domcontentloaded')
  await page.waitForLoadState('networkidle', { timeout: 15_000 }).catch(() => undefined)

  const logoutButton = page.getByRole('button', { name: 'Đăng xuất' }).first()
  const dashboardHeading = page.getByRole('heading', { level: 1 }).first()
  await Promise.race([
    logoutButton.waitFor({ state: 'visible', timeout: 10_000 }),
    dashboardHeading.waitFor({ state: 'visible', timeout: 10_000 }),
  ]).catch(() => undefined)

  await context.storageState({ path: authFilePath })
  await browser.close()
}

export default globalSetup
