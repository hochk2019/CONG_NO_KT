import { test, expect } from '@playwright/test'

test('Login page renders form', async ({ page }) => {
  await page.goto('/login')

  await expect(page.getByRole('heading', { name: 'Đăng nhập' })).toBeVisible()
  await expect(page.getByLabel('Tên đăng nhập')).toBeVisible()
  await expect(page.getByLabel('Mật khẩu')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Đăng nhập' })).toBeVisible()
})
