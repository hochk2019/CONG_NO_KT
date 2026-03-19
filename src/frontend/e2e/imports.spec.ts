import fs from 'node:fs/promises'
import { test, expect, type TestInfo } from '@playwright/test'
import path from 'path'
import { read, utils, write } from 'xlsx'
import { loginAsDefaultUser } from './support/auth'

const buildUniqueCustomerTaxCode = () => {
  const suffix = `${Date.now()}${Math.floor(Math.random() * 1000)}`
  return `9${suffix.slice(-9)}`
}

const extractBatchId = (text: string | null) => {
  const match = text?.match(/Mã lô:\s*([0-9a-f-]+)/i)
  if (!match) {
    throw new Error(`Không đọc được batch id từ: ${text ?? '<empty>'}`)
  }
  return match[1]
}

const createAdvanceImportFile = async (
  testInfo: TestInfo,
  customerTaxCode: string,
  description: string,
) => {
  const templatePath = path.resolve(process.cwd(), 'public/templates/advance_template.xlsx')
  const outputPath = testInfo.outputPath(`advance-import-${customerTaxCode}.xlsx`)
  const workbook = read(await fs.readFile(templatePath), { type: 'buffer' })
  const sheetName = workbook.SheetNames[0]
  const rows = utils.sheet_to_json(workbook.Sheets[sheetName], {
    header: 1,
    raw: true,
  }) as (string | number | null)[][]

  if (rows.length < 2 || rows[1].length < 5) {
    throw new Error(`Template ADVANCE không đúng định dạng: ${templatePath}`)
  }

  rows[1] = [...rows[1]]
  rows[1][1] = customerTaxCode
  rows[1][4] = description
  workbook.Sheets[sheetName] = utils.aoa_to_sheet(rows)

  await fs.writeFile(outputPath, write(workbook, { type: 'buffer', bookType: 'xlsx' }))
  return outputPath
}

test.describe('Imports page', () => {
  test('Upload template and see batch id', async ({ page }) => {
    await loginAsDefaultUser(page)
    await page.getByRole('link', { name: 'Nhập liệu HĐ' }).first().click()

    await expect(
      page.getByRole('heading', { name: 'Nhập file, kiểm tra trước khi ghi dữ liệu' }),
    ).toBeVisible()

    const templatePath = path.resolve(process.cwd(), 'public/templates/invoice_template.xlsx')
    await page.getByLabel('Chọn file').setInputFiles(templatePath)
    await page.getByRole('button', { name: 'Tải file' }).click()

    await expect(page.getByText('Mã lô:')).toBeVisible({ timeout: 30000 })
  })

  test('Advances workspace shows advanced filters', async ({ page }) => {
    await loginAsDefaultUser(page)
    await page.goto('/advances')

    await expect(
      page.getByRole('heading', { name: 'Workspace nhập liệu và xử lý khoản trả hộ KH', level: 2 }),
    ).toBeVisible()

    await page.getByRole('button', { name: 'Bộ lọc nâng cao' }).click()
    const advancedFilters = page.locator('.filters-grid--compact')
    await expect(advancedFilters.getByLabel('Số chứng từ')).toBeVisible()
    await expect(advancedFilters.getByLabel('Từ ngày')).toBeVisible()
    await expect(advancedFilters.getByLabel('Đến ngày')).toBeVisible()
    await expect(advancedFilters.getByLabel('Số tiền từ')).toBeVisible()
    await expect(advancedFilters.getByLabel('Số tiền đến')).toBeVisible()
    await expect(advancedFilters.getByLabel('Nguồn dữ liệu')).toBeVisible()
  })

  test('Advances import shortcut redirects to batch import tab', async ({ page }) => {
    await loginAsDefaultUser(page)
    await page.goto('/advances?tab=import')
    await expect(page).toHaveURL(/\/imports\?tab=batch&type=ADVANCE$/)
    await expect(page.getByRole('heading', { name: 'Nhập file, kiểm tra trước khi ghi dữ liệu' })).toBeVisible()
  })

  test('Commit ADVANCE import with new customer shows up in advances workspace', async ({ page }, testInfo) => {
    const customerTaxCode = buildUniqueCustomerTaxCode()
    const description = `E2E ADVANCE ${customerTaxCode}`
    const templatePath = await createAdvanceImportFile(testInfo, customerTaxCode, description)

    await loginAsDefaultUser(page)
    await page.goto('/imports?tab=batch&type=ADVANCE')

    await expect(
      page.getByRole('heading', { name: 'Nhập file, kiểm tra trước khi ghi dữ liệu' }),
    ).toBeVisible()

    await page.getByLabel('Chọn file').setInputFiles(templatePath)
    await page.getByRole('button', { name: 'Tải file' }).click()

    const batchMeta = page.locator('.meta-row').filter({ hasText: 'Mã lô:' }).first()
    await expect(batchMeta).toContainText('Mã lô:', { timeout: 30_000 })
    const batchId = extractBatchId(await batchMeta.textContent())
    const shortBatchId = batchId.slice(0, 8)

    await page.getByRole('button', { name: 'Xem trước' }).click()
    const previewDialog = page.getByRole('dialog')
    await expect(previewDialog.getByRole('heading', { name: 'Xem trước dữ liệu' })).toBeVisible()
    await expect(
      previewDialog
        .locator('pre.code-block')
        .filter({ hasText: `"customer_tax_code":"${customerTaxCode}"` }),
    ).toBeVisible()
    await previewDialog.getByRole('button', { name: 'Đóng', exact: true }).click()
    await expect(previewDialog).toBeHidden()

    await page.getByRole('button', { name: 'Ghi dữ liệu' }).click()

    const commitAlert = page.locator('.alert--success').filter({ hasText: 'Ghi thành công:' }).first()
    await expect(commitAlert).toContainText('1 khoản trả hộ KH', { timeout: 30_000 })

    await page.goto('/advances')
    await expect(
      page.getByRole('heading', { name: 'Workspace nhập liệu và xử lý khoản trả hộ KH', level: 2 }),
    ).toBeVisible()

    await page.getByLabel('Lọc MST bên mua').fill(customerTaxCode)
    await page.getByRole('button', { name: 'Bộ lọc nâng cao' }).click()
    await page.getByLabel('Nguồn dữ liệu').selectOption('IMPORT')

    const importedRow = page.locator('tbody tr').filter({ hasText: description }).first()
    await expect(importedRow).toContainText(customerTaxCode, { timeout: 30_000 })
    await expect(importedRow).toContainText(`Import · ${shortBatchId}`)
  })
})
