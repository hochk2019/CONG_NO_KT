import { test, expect } from '@playwright/test'

const username = process.env.E2E_USERNAME ?? 'admin'
const password = process.env.E2E_PASSWORD ?? 'Sam0905@'

test.describe('Customers page', () => {
  test('Load customers page and show main filters', async ({ page }) => {
    await page.goto('/login')

    await page.getByLabel('Tên đăng nhập').fill(username ?? '')
    await page.getByLabel('Mật khẩu').fill(password ?? '')
    await page.getByRole('button', { name: 'Đăng nhập' }).click()

    await expect(page).toHaveURL(/\/dashboard$/)
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
