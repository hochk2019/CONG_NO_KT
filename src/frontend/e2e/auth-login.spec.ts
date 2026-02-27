import { test, expect } from '@playwright/test'
import { loginAsDefaultUser } from './support/auth'

test.describe('Authenticated flows', () => {
  test('Login then open Customers page', async ({ page }) => {
    await loginAsDefaultUser(page)
    await page.getByRole('link', { name: 'Khách hàng' }).first().click()
    await expect(page.getByRole('heading', { name: 'Danh sách khách hàng' })).toBeVisible()
  })
})
