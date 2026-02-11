import { test, expect } from '@playwright/test'

const username = process.env.E2E_USERNAME ?? 'admin'
const password = process.env.E2E_PASSWORD ?? 'Sam0905@'

test.describe('Customers transactions interactions', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login')

    await page.getByLabel('Tên đăng nhập').fill(username ?? '')
    await page.getByLabel('Mật khẩu').fill(password ?? '')
    await page.getByRole('button', { name: 'Đăng nhập' }).click()

    await expect(page).toHaveURL(/\/dashboard$/)
    await page.getByRole('link', { name: 'Khách hàng' }).first().click()
    await expect(page.getByRole('heading', { name: 'Danh sách khách hàng' })).toBeVisible()
  })

  test('Chọn khách hàng và mở modal Xem/Hủy', async ({ page }) => {
    const listSection = page.locator('section.card', {
      has: page.getByRole('heading', { name: 'Danh sách khách hàng' }),
    })
    const selectButtons = listSection.getByRole('button', { name: 'Xem' })
    if ((await selectButtons.count()) === 0) {
      test.info().skip('Không có dữ liệu khách hàng để chọn.')
      return
    }

    await selectButtons.first().click()
    const transactions = page.locator('#customer-transactions')
    await expect(transactions).toBeVisible()

    const viewButtons = transactions.getByRole('button', { name: 'Xem' })
    if ((await viewButtons.count()) === 0) {
      test.info().skip('Không có dữ liệu giao dịch để mở modal.')
      return
    }

    await viewButtons.first().click()
    const dialog = page.getByRole('dialog')
    await expect(dialog).toBeVisible()
    await expect(dialog.getByText(/Chi tiết/i)).toBeVisible()
    await dialog.getByRole('button', { name: 'Đóng' }).first().click()
    await expect(dialog).toBeHidden()

    const voidButtons = transactions.locator('button:has-text("Hủy"):not([disabled])')
    if ((await voidButtons.count()) === 0) {
      test.info().skip('Không có giao dịch có thể hủy.')
      return
    }

    await voidButtons.first().click()
    const voidDialog = page.getByRole('dialog')
    await expect(voidDialog).toBeVisible()
    await expect(voidDialog.getByText(/Hủy (hóa đơn|khoản trả hộ KH)/i)).toBeVisible()
    await voidDialog.getByRole('button', { name: 'Đóng' }).first().click()
    await expect(voidDialog).toBeHidden()
  })

  test('Lọc trạng thái và tìm kiếm chứng từ trong tab Hóa đơn', async ({ page }) => {
    const listSection = page.locator('section.card', {
      has: page.getByRole('heading', { name: 'Danh sách khách hàng' }),
    })
    const selectButtons = listSection.getByRole('button', { name: 'Xem' })
    if ((await selectButtons.count()) === 0) {
      test.info().skip('Không có dữ liệu khách hàng để chọn.')
      return
    }

    await selectButtons.first().click()
    const transactions = page.locator('#customer-transactions')
    await expect(transactions).toBeVisible()

    const docInput = transactions.getByLabel('Tìm chứng từ (HĐ / PT)')
    await docInput.fill('HD-TEST')
    await expect(docInput).toHaveValue('HD-TEST')

    const statusSelect = transactions.getByRole('combobox', { name: 'Trạng thái' })
    await statusSelect.selectOption({ label: 'Đã thanh toán' })
    await expect(statusSelect).toHaveValue('PAID')
  })
})
