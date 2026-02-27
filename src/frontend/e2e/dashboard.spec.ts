import { test, expect } from '@playwright/test'
import { loginAsDefaultUser } from './support/auth'

test.describe('Dashboard page', () => {
  test.use({ viewport: { width: 1440, height: 900 } })

  test('Render dashboard overview sections', async ({ page }) => {
    await loginAsDefaultUser(page)
    await expect(page.getByRole('heading', { name: 'Tổng quan công nợ' })).toBeVisible()

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

    await expect(page.getByRole('heading', { name: /Dòng tiền Expected vs Actual/i })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Trạng thái phân bổ' })).toBeVisible()

    const millionToggle = page.getByRole('button', { name: 'Triệu' })
    await expect(millionToggle).toBeVisible()
    await millionToggle.click()
    await expect(millionToggle).toHaveClass(/unit-toggle__btn--active/)

    await expect(page.getByRole('heading', { name: 'Hành động nhanh' })).toBeVisible()
  })
})
