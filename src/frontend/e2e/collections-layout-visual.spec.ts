import { expect, test } from '@playwright/test'
import { loginAsDefaultUser } from './support/auth'

test.describe('Collections layout visual checks', () => {
  test('captures action/update columns and reports overflow-hint styles', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 })
    await loginAsDefaultUser(page)

    await page.route('**/collections/tasks?**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            taskId: 'task-e2e-001',
            customerTaxCode: '0101234567',
            customerName: 'Cong ty Alpha',
            ownerId: 'owner-1',
            ownerName: 'Owner 1',
            totalOutstanding: 2000000,
            overdueAmount: 1500000,
            maxDaysPastDue: 35,
            predictedOverdueProbability: 0.76,
            riskLevel: 'HIGH',
            aiSignal: 'OVERDUE_RISK',
            priorityScore: 0.88,
            status: 'OPEN',
            assignedTo: 'user-1',
            note: null,
            createdAt: '2026-03-01T01:00:00Z',
            updatedAt: '2026-03-01T01:00:00Z',
            completedAt: null,
          },
        ]),
      })
    })

    await page.goto('/collections')

    await expect(page.getByRole('heading', { name: 'Workboard thu hồi công nợ' })).toBeVisible()
    await expect(page.getByText('Cong ty Alpha')).toBeVisible()
    await expect(page.getByRole('columnheader', { name: 'Cập nhật' })).toBeVisible()
    await expect(page.getByRole('columnheader', { name: 'Xử lý' })).toBeVisible()

    const tableScroll = page.locator('.table-scroll').first()
    await expect(tableScroll).toBeVisible()

    const collectDebug = () =>
      page.evaluate(() => {
        const updateHeaderEl = Array.from(document.querySelectorAll('th'))
          .find((th) => (th.textContent ?? '').includes('Cập nhật')) as HTMLElement | undefined
        const tableScrollEl = updateHeaderEl?.closest('.table-scroll') as HTMLElement | null

        if (!tableScrollEl || !updateHeaderEl) {
          return null
        }

        const scrollRect = tableScrollEl.getBoundingClientRect()
        const updateRect = updateHeaderEl.getBoundingClientRect()
        const pseudo = window.getComputedStyle(tableScrollEl, '::after')
        const pseudoWidth = Number.parseFloat(pseudo.width) || 0
        const pseudoLeft = scrollRect.right - pseudoWidth

        return {
          className: tableScrollEl.className,
          scrollLeft: tableScrollEl.scrollLeft,
          scrollWidth: tableScrollEl.scrollWidth,
          clientWidth: tableScrollEl.clientWidth,
          overflowX: window.getComputedStyle(tableScrollEl).overflowX,
          pseudoDisplay: pseudo.display,
          pseudoContent: pseudo.content,
          pseudoOpacity: pseudo.opacity,
          pseudoWidth,
          pseudoBackground: pseudo.backgroundImage,
          pseudoLeft,
          updateLeft: updateRect.left,
          updateRight: updateRect.right,
          crossesUpdateColumn: pseudoLeft >= updateRect.left && pseudoLeft <= updateRect.right,
        }
      })

    const debug = await collectDebug()

    await tableScroll.evaluate((el) => {
      const maxScroll = el.scrollWidth - el.clientWidth
      const target = Math.max(0, Math.min(620, maxScroll))
      el.scrollTo({ left: target, behavior: 'auto' })
    })

    await page.waitForTimeout(150)
    const scrolledDebug = await collectDebug()

    await page.screenshot({
      path: 'e2e/.artifacts/collections-layout-desktop.png',
      fullPage: true,
    })

    await page.screenshot({
      path: 'e2e/.artifacts/collections-layout-desktop-scrolled.png',
      fullPage: true,
    })

    expect(debug).not.toBeNull()
    expect(scrolledDebug).not.toBeNull()
    expect(scrolledDebug?.className).toContain('table-scroll--no-hint')
    expect(scrolledDebug?.pseudoContent).toBe('none')
  })
})
