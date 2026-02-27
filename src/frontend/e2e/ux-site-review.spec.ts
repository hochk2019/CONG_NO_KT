import { expect, test, type Page } from '@playwright/test'
import { loginAsDefaultUser } from './support/auth'

const coreRoutes = [
  '/dashboard',
  '/imports',
  '/customers',
  '/receipts',
  '/reports',
  '/risk',
  '/notifications',
  '/admin/health',
  '/admin/erp-integration',
]

const collectUxSignals = async () => {
  return await Promise.resolve(
    (() => {
      const rootOverflow = Math.max(
        0,
        document.documentElement.scrollWidth - window.innerWidth,
      )
      const bodyOverflow = Math.max(0, document.body.scrollWidth - window.innerWidth)
      const horizontalOverflowPx = Math.max(rootOverflow, bodyOverflow)
      const headingCount = document.querySelectorAll('main h1, main h2, main h3').length
      const unnamedButtons = Array.from(document.querySelectorAll('button')).filter((button) => {
        const label = button.getAttribute('aria-label')
        const text = button.textContent?.trim()
        return !label && !text
      }).length

      return {
        horizontalOverflowPx,
        headingCount,
        unnamedButtons,
      }
    })(),
  )
}

const navigateWithinApp = async (page: Page, route: string) => {
  await page.evaluate((targetRoute: string) => {
    if (window.location.pathname !== targetRoute) {
      window.history.pushState({}, '', targetRoute)
      window.dispatchEvent(new PopStateEvent('popstate'))
    }
  }, route)

  await page.waitForURL((url: URL) => url.pathname === route || url.pathname === '/login', {
    timeout: 10_000,
  })
  await page.waitForLoadState('networkidle')

  if (route === '/imports' && new URL(page.url()).pathname !== '/login') {
    await page
      .waitForURL((url: URL) => url.pathname === '/imports' && Boolean(url.searchParams.get('tab')), {
        timeout: 3_000,
      })
      .catch(() => {})
  }

  if (new URL(page.url()).pathname !== '/login') {
    await page
      .waitForFunction(
        () => document.querySelectorAll('main h1, main h2, main h3').length > 0,
        {
          timeout: 3_000,
        },
      )
      .catch(() => {})
  }
}

test.describe('UX review - site wide', () => {
  test.describe.configure({ timeout: 90_000 })

  test('desktop routes pass baseline UX checks', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 })
    await loginAsDefaultUser(page)

    const failures: string[] = []

    for (const route of coreRoutes) {
      await navigateWithinApp(page, route)

      const currentPath = new URL(page.url()).pathname
      if (currentPath === '/login') {
        failures.push(`${route}: redirected to /login`)
        continue
      }
      if (currentPath !== route) {
        failures.push(`${route}: redirected to ${currentPath}`)
        continue
      }

      const signals = await page.evaluate(collectUxSignals)

      if (signals.headingCount < 1) {
        failures.push(`${route}: missing heading in main content`)
      }
      if (signals.horizontalOverflowPx > 1) {
        failures.push(`${route}: horizontal overflow ${signals.horizontalOverflowPx}px`)
      }
      if (signals.unnamedButtons > 0) {
        failures.push(`${route}: unnamed buttons ${signals.unnamedButtons}`)
      }
    }

    expect(failures, failures.join('\n')).toEqual([])
  })

  test('mobile routes pass baseline UX checks', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 })
    await loginAsDefaultUser(page)

    const failures: string[] = []

    for (const route of coreRoutes) {
      await navigateWithinApp(page, route)

      const currentPath = new URL(page.url()).pathname
      if (currentPath === '/login') {
        failures.push(`${route}: redirected to /login`)
        continue
      }
      if (currentPath !== route) {
        failures.push(`${route}: redirected to ${currentPath}`)
        continue
      }

      const signals = await page.evaluate(collectUxSignals)

      if (signals.headingCount < 1) {
        failures.push(`${route}: missing heading in main content`)
      }
      if (signals.horizontalOverflowPx > 1) {
        failures.push(`${route}: horizontal overflow ${signals.horizontalOverflowPx}px`)
      }
      if (signals.unnamedButtons > 0) {
        failures.push(`${route}: unnamed buttons ${signals.unnamedButtons}`)
      }
    }

    expect(failures, failures.join('\n')).toEqual([])
  })
})
