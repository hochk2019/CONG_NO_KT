import { test, expect } from '@playwright/test'
import { loginAsDefaultUser } from './support/auth'

test.describe('Customers page', () => {
  test('Load customers page and show main filters', async ({ page }) => {
    await loginAsDefaultUser(page)
    await page.getByRole('link', { name: 'Khách hàng' }).first().click()

    await expect(page.getByRole('heading', { name: 'Danh sách khách hàng' })).toBeVisible()
    await expect(page.getByLabel('Tìm kiếm')).toBeVisible()
    await expect(page.getByRole('combobox', { name: 'Trạng thái' })).toBeVisible()
    await expect(page.getByRole('combobox', { name: 'Phụ trách' })).toBeVisible()

    await page.waitForTimeout(1000)
    const viewButtons = page.getByRole('button', { name: 'Xem' })
    if ((await viewButtons.count()) === 0) {
      test.info().skip('Không có dữ liệu khách hàng để chọn.')
      return
    }

    await viewButtons.first().click()
    await expect(page.getByRole('heading', { name: 'Giao dịch khách hàng' })).toBeVisible()
  })
})
