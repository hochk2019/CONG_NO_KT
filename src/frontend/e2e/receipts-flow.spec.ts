import { test, expect } from '@playwright/test'
import { loginAsDefaultUser } from './support/auth'

const today = () => {
  const now = new Date()
  const month = `${now.getMonth() + 1}`.padStart(2, '0')
  const day = `${now.getDate()}`.padStart(2, '0')
  return `${now.getFullYear()}-${month}-${day}`
}

test.describe('Receipts flow', () => {
  test('Create draft, approve, void, unvoid and toggle reminder', async ({ page }) => {
    const receiptNo = `E2E-${Date.now()}`

    try {
      await loginAsDefaultUser(page)
    } catch {
      test.info().skip('Không đăng nhập được bằng tài khoản E2E hiện tại.')
      return
    }

    await page.goto('/receipts')
    await page.waitForLoadState('networkidle')
    const receiptsHeading = page.getByRole('heading', { name: 'Nhập phiếu thu', level: 1 })
    if ((await receiptsHeading.count()) === 0 || !(await receiptsHeading.first().isVisible())) {
      test.info().skip('Tài khoản E2E hiện tại không có quyền hoặc không mở được màn Phiếu thu.')
      return
    }

    const receiptForm = page.locator('section.card', {
      has: page.getByRole('heading', { name: 'Thông tin phiếu thu', level: 2 }),
    })
    await expect(receiptForm).toBeVisible()

    const sellerField = receiptForm
      .locator('label.field', { has: page.getByText('MST bên bán', { exact: true }) })
      .first()
    const customerField = receiptForm
      .locator('label.field', { has: page.getByText('MST bên mua', { exact: true }) })
      .first()

    const sellerOption = sellerField.locator('datalist option').first()
    const customerOption = customerField.locator('datalist option').first()

    if ((await sellerOption.count()) === 0 || (await customerOption.count()) === 0) {
      test.info().skip('Thiếu dữ liệu seller/customer lookup cho receipts flow.')
      return
    }

    const sellerTaxCode = await sellerOption.getAttribute('value')
    const customerTaxCode = await customerOption.getAttribute('value')
    if (!sellerTaxCode || !customerTaxCode) {
      test.info().skip('Không đọc được giá trị lookup seller/customer.')
      return
    }

    await sellerField.getByRole('combobox').fill(sellerTaxCode)
    await customerField.getByRole('combobox').fill(customerTaxCode)
    await receiptForm.getByLabel('Số chứng từ', { exact: true }).fill(receiptNo)
    await receiptForm.getByLabel('Ngày thu', { exact: true }).fill(today())
    await receiptForm.getByLabel('Số tiền', { exact: true }).fill('100000')

    const allocationButton = receiptForm.getByRole('button', { name: 'Chọn phân bổ' })
    await expect(allocationButton).toBeVisible()

    if (await allocationButton.isDisabled()) {
      test.info().skip('Không có open-items để chạy flow phân bổ/duyệt.')
      return
    }

    await allocationButton.click()
    const allocationDialog = page.getByRole('dialog', { name: 'Phân bổ phiếu thu' })
    await expect(allocationDialog).toBeVisible()
    await allocationDialog.getByRole('button', { name: 'Lưu phân bổ' }).click()
    await expect(allocationDialog).toBeHidden()

    await receiptForm.getByRole('button', { name: 'Lưu nháp' }).click()
    await expect(page.getByText(`Đã tạo phiếu thu`, { exact: false })).toBeVisible()

    const rowByReceiptNo = page.locator('tr', { has: page.getByText(receiptNo, { exact: true }) }).first()
    await expect(rowByReceiptNo).toBeVisible({ timeout: 30000 })

    await rowByReceiptNo.getByRole('button', { name: 'Duyệt' }).click()
    const approveDialog = page.getByRole('dialog', { name: 'Phân bổ phiếu thu' })
    await expect(approveDialog).toBeVisible()
    await approveDialog.getByRole('button', { name: 'Duyệt phiếu thu' }).click()
    await expect(approveDialog).toBeHidden()

    await expect(rowByReceiptNo.getByText('Đã duyệt')).toBeVisible()

    await rowByReceiptNo.getByRole('button', { name: 'Hủy' }).click()
    const cancelDialog = page.getByRole('dialog', { name: 'Hủy phiếu thu' })
    await expect(cancelDialog).toBeVisible()
    await cancelDialog.getByLabel('Lý do hủy').fill('E2E cancel flow')
    await cancelDialog.getByRole('button', { name: 'Xác nhận hủy' }).click()
    await expect(cancelDialog).toBeHidden()

    const statusSelect = page.getByRole('combobox', { name: 'Trạng thái' })
    await statusSelect.selectOption('VOID')
    const voidRow = page.locator('tr', { has: page.getByText(receiptNo, { exact: true }) }).first()
    await expect(voidRow).toBeVisible()

    let dialogCount = 0
    page.on('dialog', async (dialog) => {
      dialogCount += 1
      if (dialogCount === 1) {
        await dialog.accept()
      } else {
        await dialog.accept('')
      }
    })

    await voidRow.getByRole('button', { name: 'Bỏ hủy' }).click()
    await expect(voidRow).toBeHidden()

    await statusSelect.selectOption('DRAFT')
    const restoredRow = page.locator('tr', { has: page.getByText(receiptNo, { exact: true }) }).first()
    await expect(restoredRow).toBeVisible()

    const toggleButton = restoredRow.locator('button:has-text("Tắt nhắc"), button:has-text("Bật nhắc")').first()
    const before = await toggleButton.innerText()
    await toggleButton.click()
    await expect(toggleButton).not.toHaveText(before)
  })
})
