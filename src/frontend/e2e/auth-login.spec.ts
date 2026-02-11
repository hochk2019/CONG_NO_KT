import { test, expect } from '@playwright/test'

const username = process.env.E2E_USERNAME ?? 'admin'
const password = process.env.E2E_PASSWORD ?? 'Sam0905@'

test.describe('Authenticated flows', () => {
  test('Login then open Customers page', async ({ page }) => {
    await page.goto('/login')

    await page.getByLabel('Tên đăng nhập').fill(username ?? '')
    await page.getByLabel('Mật khẩu').fill(password ?? '')
    await page.getByRole('button', { name: 'Đăng nhập' }).click()

    await expect(page).toHaveURL(/\/dashboard$/)
    await page.getByRole('link', { name: 'Khách hàng' }).first().click()
    await expect(page.getByRole('heading', { name: 'Danh sách khách hàng' })).toBeVisible()
  })
})
