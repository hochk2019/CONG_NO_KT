import { test, expect, type Locator, type Page } from '@playwright/test'
import { loginAsDefaultUser } from './support/auth'

const transactionTabs = [
  {
    tab: 'Hóa đơn',
    panelId: '#customer-panel-invoices',
    viewTitle: /Chi tiết hóa đơn/i,
    voidTitle: /Hủy hóa đơn/i,
    emptyMessage: 'Không có hóa đơn.',
  },
  {
    tab: 'Khoản trả hộ KH',
    panelId: '#customer-panel-advances',
    viewTitle: /Chi tiết khoản trả hộ KH/i,
    voidTitle: /Hủy khoản trả hộ KH/i,
    emptyMessage: 'Không có khoản trả hộ KH.',
  },
] as const

const waitForTransactionPanelState = async (
  panel: Locator,
  emptyMessage: string,
): Promise<'rows' | 'empty'> => {
  let stableRowHits = 0

  for (let attempt = 0; attempt < 40; attempt += 1) {
    if ((await panel.getByText('Đang tải...').count()) > 0) {
      stableRowHits = 0
      await new Promise((resolve) => setTimeout(resolve, 250))
      continue
    }
    const viewButtons = panel.getByRole('button', { name: 'Xem' })
    if ((await viewButtons.count()) > 0 && await viewButtons.first().isVisible().catch(() => false)) {
      stableRowHits += 1
      if (stableRowHits >= 2) {
        return 'rows'
      }
      await new Promise((resolve) => setTimeout(resolve, 200))
      continue
    }
    stableRowHits = 0
    if ((await panel.getByText(emptyMessage).count()) > 0) {
      return 'empty'
    }
    await new Promise((resolve) => setTimeout(resolve, 250))
  }

  return (await panel.getByRole('button', { name: 'Xem' }).count()) > 0 ? 'rows' : 'empty'
}

const openTransactionDialog = async (params: {
  page: Page
  panel: Locator
  emptyMessage: string
  dialogTitle: RegExp
  getAction: () => Locator
}): Promise<boolean> => {
  const { page, panel, emptyMessage, dialogTitle, getAction } = params

  for (let attempt = 0; attempt < 5; attempt += 1) {
    if ((await waitForTransactionPanelState(panel, emptyMessage)) !== 'rows') {
      return false
    }

    const action = getAction()
    if ((await action.count()) === 0) {
      return false
    }

    try {
      await expect(action.first()).toBeVisible({ timeout: 3_000 })
      await action.first().click({ timeout: 5_000 })

      const dialog = page.getByRole('dialog')
      await expect(dialog).toBeVisible({ timeout: 5_000 })
      await expect(dialog.getByText(dialogTitle)).toBeVisible({ timeout: 5_000 })
      return true
    } catch {
      const dialog = page.getByRole('dialog')
      if (await dialog.isVisible().catch(() => false)) {
        await dialog.getByRole('button', { name: 'Đóng' }).first().click().catch(() => undefined)
        await expect(dialog).toBeHidden({ timeout: 2_000 }).catch(() => undefined)
      }
      await page.waitForTimeout(300)
    }
  }

  return false
}

test.describe('Customers transactions interactions', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsDefaultUser(page)
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
    await page.waitForLoadState('networkidle')

    let selectedViewTab: (typeof transactionTabs)[number] | null = null
    let foundViewRows = false
    for (const candidate of transactionTabs) {
      await transactions.getByRole('tab', { name: candidate.tab }).click()
      const panel = page.locator(candidate.panelId)
      await expect(panel).toBeVisible()
      await page.waitForLoadState('networkidle')

      if ((await waitForTransactionPanelState(panel, candidate.emptyMessage)) !== 'rows') {
        continue
      }

      foundViewRows = true
      if (await openTransactionDialog({
        page,
        panel,
        emptyMessage: candidate.emptyMessage,
        dialogTitle: candidate.viewTitle,
        getAction: () => panel.getByRole('button', { name: 'Xem' }),
      })) {
        selectedViewTab = candidate
        break
      }
    }

    if (!selectedViewTab) {
      if (!foundViewRows) {
        test.info().skip('Không có dữ liệu giao dịch để mở modal.')
        return
      }
      throw new Error('Có dữ liệu giao dịch nhưng không mở được modal Xem sau nhiều lần thử.')
    }

    const dialog = page.getByRole('dialog')
    await dialog.getByRole('button', { name: 'Đóng' }).first().click()
    await expect(dialog).toBeHidden()

    let selectedVoidTab: (typeof transactionTabs)[number] | null = null
    let foundVoidCandidate = false
    for (const candidate of transactionTabs) {
      await transactions.getByRole('tab', { name: candidate.tab }).click()
      const panel = page.locator(candidate.panelId)
      await expect(panel).toBeVisible()
      await page.waitForLoadState('networkidle')
      if ((await waitForTransactionPanelState(panel, candidate.emptyMessage)) !== 'rows') {
        continue
      }
      if ((await panel.locator('button:has-text("Hủy"):not([disabled])').count()) === 0) {
        continue
      }

      foundVoidCandidate = true
      if (await openTransactionDialog({
        page,
        panel,
        emptyMessage: candidate.emptyMessage,
        dialogTitle: candidate.voidTitle,
        getAction: () => panel.locator('button:has-text("Hủy"):not([disabled])'),
      })) {
        selectedVoidTab = candidate
        break
      }
    }

    if (!selectedVoidTab) {
      if (!foundVoidCandidate) {
        test.info().skip('Không có giao dịch có thể hủy.')
        return
      }
      throw new Error('Có giao dịch có thể hủy nhưng không mở được modal Hủy sau nhiều lần thử.')
    }

    const voidDialog = page.getByRole('dialog')
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
    await page.waitForLoadState('networkidle')
    await transactions.getByRole('tab', { name: 'Hóa đơn' }).click()
    await expect(page.locator('#customer-panel-invoices')).toBeVisible()
    await page.waitForLoadState('networkidle')

    const docInput = transactions.getByLabel('Tìm chứng từ (HĐ / PT)')
    await docInput.fill('HD-TEST')
    await expect(docInput).toHaveValue('HD-TEST')

    const statusSelect = transactions.getByRole('combobox', { name: 'Trạng thái' })
    await statusSelect.selectOption({ label: 'Đã thanh toán' })
    await expect(statusSelect).toHaveValue('PAID')
  })
})
