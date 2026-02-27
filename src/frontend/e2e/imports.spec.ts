import { test, expect } from '@playwright/test'
import path from 'path'
import { loginAsDefaultUser } from './support/auth'

test.describe('Imports page', () => {
  test('Upload template and see batch id', async ({ page }) => {
    await loginAsDefaultUser(page)
    await page.getByRole('link', { name: 'Nhập liệu' }).first().click()

    await expect(
      page.getByRole('heading', { name: 'Nhập file, kiểm tra trước khi ghi dữ liệu' }),
    ).toBeVisible()

    const templatePath = path.resolve(process.cwd(), 'public/templates/invoice_template.xlsx')
    await page.getByLabel('Chọn file').setInputFiles(templatePath)
    await page.getByRole('button', { name: 'Tải file' }).click()

    await expect(page.getByText('Mã lô:')).toBeVisible({ timeout: 30000 })
  })

  test('Manual advances shows advanced filters', async ({ page }) => {
    await loginAsDefaultUser(page)
    await page.getByRole('link', { name: 'Nhập liệu' }).first().click()

    await page.getByRole('tab', { name: 'Nhập thủ công (Khoản trả hộ KH)' }).click()
    await expect(page.getByRole('heading', { name: 'Khoản trả hộ KH', level: 2 })).toBeVisible()

    await page.getByRole('button', { name: 'Bộ lọc nâng cao' }).click()
    const advancedFilters = page.locator('.filters-grid--compact')
    await expect(advancedFilters.getByLabel('Số chứng từ')).toBeVisible()
    await expect(advancedFilters.getByLabel('Từ ngày')).toBeVisible()
    await expect(advancedFilters.getByLabel('Đến ngày')).toBeVisible()
    await expect(advancedFilters.getByLabel('Số tiền từ')).toBeVisible()
    await expect(advancedFilters.getByLabel('Số tiền đến')).toBeVisible()
    await expect(advancedFilters.getByLabel('Nguồn dữ liệu')).toBeVisible()
  })

  test('Advances route redirects to manual imports tab', async ({ page }) => {
    await loginAsDefaultUser(page)

    await page.goto('/advances')
    await expect(page).toHaveURL(/\/imports\?tab=manual$/)
    await expect(page.getByRole('heading', { name: 'Khoản trả hộ KH', level: 2 })).toBeVisible()
  })
})
