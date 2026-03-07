import { test, expect } from '@playwright/test'
import { loginAsDefaultUser } from './support/auth'

test.describe('Customers page', () => {
  test('Load customers page and show main filters', async ({ page }) => {
    await loginAsDefaultUser(page)
    await page.getByRole('link', { name: 'Khách hàng' }).first().click()

    await expect(page.getByRole('heading', { name: 'Danh sách khách hàng' })).toBeVisible()
    await expect(page.getByLabel('Tìm kiếm')).toBeVisible()
    await expect(page.getByRole('combobox', { name: 'Trạng thái' })).toBeVisible()
    await expect(page.getByRole('combobox', { name: 'Phụ trách' })).toBeVisible()

    await page.waitForTimeout(1000)
    const viewButtons = page.getByRole('button', { name: 'Xem' })
    if ((await viewButtons.count()) === 0) {
      test.info().skip('Không có dữ liệu khách hàng để chọn.')
      return
    }

    await viewButtons.first().click()
    await expect(page.getByRole('heading', { name: 'Giao dịch khách hàng' })).toBeVisible()
  })

  test('Hide horizontal scroll hint after reaching the right edge of the customers table', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 })
    await loginAsDefaultUser(page)
    await page.getByRole('link', { name: 'Khách hàng' }).first().click()

    const listSection = page.locator('section.card', {
      has: page.getByRole('heading', { name: 'Danh sách khách hàng' }),
    })
    const tableScroll = listSection.locator('.table-scroll')
    await expect(tableScroll).toBeVisible()

    const overflowMetrics = await tableScroll.evaluate((node) => {
      return {
        className: node.className,
        maxScrollLeft: node.scrollWidth - node.clientWidth,
      }
    })

    expect(overflowMetrics.maxScrollLeft).toBeGreaterThan(0)
    expect(overflowMetrics.className).not.toContain('table-scroll--no-hint')

    await tableScroll.evaluate((node) => {
      node.scrollLeft = node.scrollWidth
      node.dispatchEvent(new Event('scroll', { bubbles: true }))
    })

    await expect
      .poll(async () => {
        return tableScroll.evaluate((node) => node.className)
      })
      .toContain('table-scroll--no-hint')
  })
})
