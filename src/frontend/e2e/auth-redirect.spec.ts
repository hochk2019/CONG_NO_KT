import { test, expect } from '@playwright/test'

test.use({ storageState: { cookies: [], origins: [] } })

test('Protected routes redirect to login when not authenticated', async ({ page }) => {
  await page.goto('/customers')
  await expect(page).toHaveURL(/\/login$/)
  await expect(page.getByRole('heading', { name: 'Đăng nhập' })).toBeVisible()
})
