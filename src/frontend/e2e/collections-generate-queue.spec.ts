import { expect, test } from '@playwright/test'
import { loginAsDefaultUser } from './support/auth'

type GenerateQueueResponse = {
  created: number
  candidates: number
  minPriorityScore: number
}

test.describe('Collections queue generation', () => {
  test('Creates queue and shows summary message from API result', async ({ page }) => {
    await loginAsDefaultUser(page)
    await page.goto('/collections')

    await expect(page.getByRole('heading', { name: 'Workboard thu hồi công nợ' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Tạo queue từ cảnh báo rủi ro' })).toBeVisible()

    const generateButton = page.getByRole('button', { name: 'Tạo queue', exact: true })

    const responsePromise = page.waitForResponse(
      (response) =>
        response.url().includes('/collections/tasks/generate') &&
        response.request().method() === 'POST',
      { timeout: 30_000 },
    )

    await generateButton.click()

    const response = await responsePromise
    expect(response.status()).toBe(200)

    const payload = (await response.json()) as GenerateQueueResponse
    const summary = `Tạo mới ${payload.created} task trên ${payload.candidates} khách hàng rủi ro. Ngưỡng ưu tiên: ${payload.minPriorityScore.toFixed(2)}.`

    await expect(page.getByText(summary)).toBeVisible({ timeout: 30_000 })

    if (payload.created === 0) {
      if (payload.candidates === 0) {
        await expect(
          page.getByText('Không có khách hàng rủi ro phù hợp với bộ lọc hiện tại.'),
        ).toBeVisible()
      } else {
        await expect(page.getByText('Không có task mới được thêm.')).toBeVisible()
      }
    }

    await expect(page.getByText('Không thể tạo queue thu hồi từ dữ liệu rủi ro.')).toHaveCount(0)
  })
})
