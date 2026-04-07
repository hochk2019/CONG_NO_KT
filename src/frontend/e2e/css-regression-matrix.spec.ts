import { expect, test, type Page } from '@playwright/test'
import { loginAsDefaultUser } from './support/auth'

type ViewportCase = {
  label: string
  width: number
  height: number
}

const viewportCases: ViewportCase[] = [
  { label: 'desktop-1920x1080', width: 1920, height: 1080 },
  { label: 'desktop-1366x768', width: 1366, height: 768 },
  { label: 'mobile-390x844', width: 390, height: 844 },
]

const openRoute = async (page: Page, route: string) => {
  await page.goto(route, { waitUntil: 'domcontentloaded' })
  await page.waitForTimeout(400)
}

const verifyDashboard = async (page: Page) => {
  await openRoute(page, '/dashboard')
  await expect(page.getByRole('heading', { name: 'Tổng quan công nợ' })).toBeVisible()

  const cashflowCard = page
    .locator('section.card', {
      has: page.getByRole('heading', { name: /Dòng tiền Expected vs Actual/i }),
    })
    .first()
  await expect(cashflowCard).toBeVisible()

  const box = await cashflowCard.boundingBox()
  if (box) {
    await page.mouse.move(box.x + box.width * 0.5, box.y + box.height * 0.5)
    await page.waitForTimeout(120)
  }
}

const verifyCustomersTable = async (page: Page) => {
  await openRoute(page, '/customers')
  await expect(page.getByRole('heading', { name: 'Danh sách khách hàng' })).toBeVisible()
  await expect(page.locator('table').first()).toBeVisible()
}

const verifyReports = async (page: Page) => {
  await openRoute(page, '/reports')
  await expect(page.getByRole('heading', { name: 'Tổng quan công nợ & báo cáo chi tiết' })).toBeVisible()
}

const verifyAdvancesFilterGrid = async (page: Page) => {
  await openRoute(page, '/advances')
  await expect(
    page.getByRole('heading', { name: 'Workspace nhập liệu và xử lý khoản trả hộ KH', level: 2 }),
  ).toBeVisible()
  await page.getByRole('button', { name: 'Bộ lọc nâng cao' }).click()
  await expect(page.locator('.filters-grid--compact').first()).toBeVisible()
}

const verifyReceiptsModal = async (page: Page) => {
  await openRoute(page, '/receipts')
  await expect(page.getByRole('heading', { name: 'Nhập phiếu thu', level: 1 })).toBeVisible()

  const advancedButton = page.getByRole('button', { name: 'Tùy chọn nâng cao' }).first()
  await expect(advancedButton).toBeVisible()
  await advancedButton.click()

  const advancedDialog = page.getByRole('dialog', { name: 'Tùy chọn nâng cao' })
  await expect(advancedDialog).toBeVisible()
  await advancedDialog.getByRole('button', { name: 'Đóng' }).click()
  await expect(advancedDialog).toBeHidden()
}

const runRegressionChecks = async (page: Page, viewportCase: ViewportCase) => {
  await page.setViewportSize({ width: viewportCase.width, height: viewportCase.height })
  for (let attempt = 1; attempt <= 2; attempt += 1) {
    try {
      await loginAsDefaultUser(page)
      break
    } catch (error) {
      if (attempt === 2) {
        throw error
      }
      await page.waitForTimeout(1_500)
    }
  }

  await verifyDashboard(page)
  await verifyCustomersTable(page)
  await verifyReports(page)
  await verifyAdvancesFilterGrid(page)
  await verifyReceiptsModal(page)
}

test.describe('CSS regression matrix (light)', () => {
  test.describe.configure({ timeout: 120_000 })
  test.use({ colorScheme: 'light' })

  test('light - desktop/mobile matrix', async ({ page }) => {
    for (const viewportCase of viewportCases) {
      await runRegressionChecks(page, viewportCase)
    }
  })
})

test.describe('CSS regression matrix (dark)', () => {
  test.describe.configure({ timeout: 120_000 })
  test.use({ colorScheme: 'dark' })

  test('dark - desktop/mobile matrix', async ({ page }) => {
    for (const viewportCase of viewportCases) {
      await runRegressionChecks(page, viewportCase)
    }
  })
})
