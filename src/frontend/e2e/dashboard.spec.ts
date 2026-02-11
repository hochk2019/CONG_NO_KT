import { test, expect } from '@playwright/test'

const username = process.env.E2E_USERNAME ?? 'admin'
const password = process.env.E2E_PASSWORD ?? 'Sam0905@'

test.describe('Dashboard page', () => {
  test.use({ viewport: { width: 1440, height: 900 } })

  test('Render dashboard overview sections', async ({ page }) => {
    await page.goto('/login')

    await page.getByLabel('Tên đăng nhập').fill(username ?? '')
    await page.getByLabel('Mật khẩu').fill(password ?? '')
    await page.getByRole('button', { name: 'Đăng nhập' }).click()

    await expect(page).toHaveURL(/\/dashboard$/)
    await expect(page.getByRole('heading', { name: 'Dashboard công nợ' })).toBeVisible()

    const kpiLabels = [
      'Tổng dư công nợ',
      'Dư hóa đơn',
      'Dư trả hộ',
      'Đã thu chưa phân bổ',
      'Quá hạn',
    ]
    for (const label of kpiLabels) {
      await expect(page.getByText(label, { exact: true })).toBeVisible()
    }

    await expect(page.getByRole('heading', { name: 'Luồng tiền thu theo ngày' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Tuổi nợ' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Trạng thái phân bổ' })).toBeVisible()

    const millionToggle = page.getByRole('button', { name: 'Triệu' })
    await expect(millionToggle).toBeVisible()
    await millionToggle.click()
    await expect(millionToggle).toHaveClass(/unit-toggle__btn--active/)

    await expect(page.getByRole('heading', { name: 'Hành động nhanh' })).toBeVisible()
  })
})
