import { test, expect } from '@playwright/test'
import { loginAsDefaultUser } from './support/auth'

test.describe('Receipts visual', () => {
  test.use({ viewport: { width: 1440, height: 900 } })

  test('Receipt form snapshots', async ({ page }) => {
    await loginAsDefaultUser(page)

    await page.goto('/receipts')
    await expect(page.getByRole('heading', { name: 'Nhập phiếu thu', level: 1 })).toBeVisible()

    await page.addStyleTag({
      content: `
        *, *::before, *::after {
          animation-duration: 0s !important;
          animation-delay: 0s !important;
          transition-duration: 0s !important;
          transition-delay: 0s !important;
          caret-color: transparent !important;
        }
      `,
    })

    const formCard = page.locator('section.card', {
      has: page.getByRole('heading', { name: 'Thông tin phiếu thu', level: 2 }),
    })

    await expect(formCard).toBeVisible()
    await expect(formCard).toHaveScreenshot('receipts-form.png', {
      maxDiffPixelRatio: 0.02,
    })

    await page.getByRole('button', { name: 'Tùy chọn nâng cao' }).click()
    const advancedDialog = page.getByRole('dialog', { name: 'Tùy chọn nâng cao' })
    await expect(advancedDialog).toBeVisible()
    await expect(advancedDialog).toHaveScreenshot('receipts-advanced-modal.png', {
      maxDiffPixelRatio: 0.02,
    })
  })
})
