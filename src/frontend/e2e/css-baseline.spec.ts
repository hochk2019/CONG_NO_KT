import { test } from '@playwright/test'
import fs from 'node:fs/promises'
import path from 'node:path'
import { loginAsDefaultUser } from './support/auth'

const baselineRoutes = [
  { route: '/dashboard', file: 'dashboard.png' },
  { route: '/customers', file: 'customers.png' },
  { route: '/reports', file: 'reports.png' },
  { route: '/admin/health', file: 'admin-health.png' },
]

test.describe('CSS baseline capture', () => {
  test('capture core route screenshots', async ({ page }, testInfo) => {
    test.setTimeout(90_000)
    await page.setViewportSize({ width: 1440, height: 900 })
    await loginAsDefaultUser(page)

    const outputDir = path.resolve(testInfo.config.rootDir, 'test-results', 'css-baseline')
    await fs.mkdir(outputDir, { recursive: true })

    for (const item of baselineRoutes) {
      await page.goto(item.route, { waitUntil: 'domcontentloaded' })
      await page.waitForTimeout(1200)
      await page.screenshot({
        path: path.join(outputDir, item.file),
        fullPage: true,
        animations: 'disabled',
      })
    }
  })
})
