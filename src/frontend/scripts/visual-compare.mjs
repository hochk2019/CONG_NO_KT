import fs from 'fs/promises'
import path from 'path'
import { chromium } from 'playwright'
import { PNG } from 'pngjs'
import pixelmatch from 'pixelmatch'
import { spawn } from 'child_process'

const username = process.env.E2E_USERNAME
const password = process.env.E2E_PASSWORD
const baseUrl = process.env.E2E_BASE_URL ?? 'http://127.0.0.1:5173'

if (!username || !password) {
  console.error('Thiếu E2E_USERNAME/E2E_PASSWORD')
  process.exit(1)
}

const repoRoot = path.resolve(process.cwd(), '..', '..')
const previewPath = path.resolve(repoRoot, 'preview', 'receipts-form-preview.html')
const outputDir = path.resolve(process.cwd(), 'test-results', 'visual-compare')

await fs.mkdir(outputDir, { recursive: true })

const previewFormPath = path.join(outputDir, 'receipts-form-preview.png')
const actualFormPath = path.join(outputDir, 'receipts-form-actual.png')
const diffFormPath = path.join(outputDir, 'receipts-form-diff.png')
const previewModalPath = path.join(outputDir, 'receipts-advanced-preview.png')
const actualModalPath = path.join(outputDir, 'receipts-advanced-actual.png')
const diffModalPath = path.join(outputDir, 'receipts-advanced-diff.png')

const disableMotion = async (page) => {
  await page.addStyleTag({
    content: `
      *, *::before, *::after {
        animation-duration: 0s !important;
        animation-delay: 0s !important;
        transition-duration: 0s !important;
        transition-delay: 0s !important;
        caret-color: transparent !important;
      }
    `,
  })
}

const padToSameSize = (png, width, height) => {
  const padded = new PNG({ width, height })
  padded.data.fill(255)
  PNG.bitblt(png, padded, 0, 0, png.width, png.height, 0, 0)
  return padded
}

const diffImages = async (basePath, comparePath, diffPath) => {
  const [baseBuffer, compareBuffer] = await Promise.all([
    fs.readFile(basePath),
    fs.readFile(comparePath),
  ])
  const basePng = PNG.sync.read(baseBuffer)
  const comparePng = PNG.sync.read(compareBuffer)
  const width = Math.max(basePng.width, comparePng.width)
  const height = Math.max(basePng.height, comparePng.height)
  const basePadded = padToSameSize(basePng, width, height)
  const comparePadded = padToSameSize(comparePng, width, height)
  const diff = new PNG({ width, height })

  const diffPixels = pixelmatch(
    basePadded.data,
    comparePadded.data,
    diff.data,
    width,
    height,
    { threshold: 0.1 },
  )

  await fs.writeFile(diffPath, PNG.sync.write(diff))
  return { diffPixels, width, height }
}

const checkServer = async (url, timeoutMs = 3000) => {
  const controller = new AbortController()
  const timeout = setTimeout(() => controller.abort(), timeoutMs)
  try {
    const response = await fetch(url, { signal: controller.signal })
    return response.ok
  } catch {
    return false
  } finally {
    clearTimeout(timeout)
  }
}

const startDevServer = async () => {
  const npmCmd = process.platform === 'win32' ? 'npm.cmd' : 'npm'
  const serverProcess = spawn(
    npmCmd,
    ['run', 'dev', '--', '--host', '127.0.0.1', '--port', '5173'],
    { cwd: process.cwd(), stdio: 'pipe', shell: true },
  )

  const startTime = Date.now()
  while (Date.now() - startTime < 60_000) {
    const ok = await checkServer(baseUrl, 2000)
    if (ok) return serverProcess
    await new Promise((resolve) => setTimeout(resolve, 1000))
  }

  serverProcess.kill()
  throw new Error('Không thể khởi động Vite dev server trong 60s.')
}

let serverProcess = null
if (!(await checkServer(baseUrl))) {
  serverProcess = await startDevServer()
}

const browser = await chromium.launch()
const context = await browser.newContext({
  viewport: { width: 1440, height: 900 },
})

try {
  const previewUrl = `file://${previewPath}`
  const previewPage = await context.newPage()
  await previewPage.goto(previewUrl)
  await disableMotion(previewPage)

  const previewForm = previewPage.locator('.card', {
    has: previewPage.getByRole('heading', { name: 'Thông tin phiếu thu', level: 2 }),
  })
  await previewForm.waitFor({ state: 'visible' })
  await previewForm.screenshot({ path: previewFormPath })

  const previewModal = previewPage.locator('.modal')
  await previewModal.screenshot({ path: previewModalPath })
  await previewPage.close()

  const page = await context.newPage()
  await page.goto(`${baseUrl}/login`)
  await page.getByLabel('Tên đăng nhập').fill(username)
  await page.getByLabel('Mật khẩu').fill(password)
  await page.getByRole('button', { name: 'Đăng nhập' }).click()
  await page.waitForURL(/\/dashboard$/)
  await page.goto(`${baseUrl}/receipts`)
  await page.waitForLoadState('networkidle')
  await disableMotion(page)

  const actualForm = page
    .locator('section.card')
    .filter({ hasText: 'Thông tin phiếu thu' })
  let actualFormLocator = null
  try {
    await actualForm.waitFor({ state: 'visible', timeout: 15000 })
    actualFormLocator = actualForm
  } catch {
    console.warn('Không tìm thấy form "Thông tin phiếu thu", chụp toàn trang.')
  }
  if (actualFormLocator) {
    try {
      await page.getByLabel('MST bên bán').fill('2301098313')
    } catch {
      try {
        await page.getByPlaceholder('VD: 2301098313').fill('2301098313')
      } catch {
        console.warn('Không thể điền "MST bên bán".')
      }
    }
    try {
      await page.getByLabel('MST bên mua').fill('2300328765')
    } catch {
      try {
        await page.getByPlaceholder('VD: 2300328765').fill('2300328765')
      } catch {
        console.warn('Không thể điền "MST bên mua".')
      }
    }
    try {
      await actualFormLocator.locator('input[type="date"]').fill('2026-01-21')
    } catch {
      console.warn('Không thể điền "Ngày thu".')
    }
    try {
      await actualFormLocator.locator('input[type="number"]').fill('15000000')
    } catch {
      console.warn('Không thể điền "Số tiền".')
    }
    try {
      await actualFormLocator.locator('select').first().selectOption('BANK')
    } catch {
      console.warn('Không thể chọn "Hình thức".')
    }
    try {
      const allocationButton = page.getByRole('button', { name: 'Chọn phân bổ' })
      if (await allocationButton.isEnabled()) {
        await allocationButton.click()
        const allocationModal = page.locator('.modal', {
          has: page.getByRole('heading', { name: 'Phân bổ phiếu thu' }),
        })
        await allocationModal.waitFor({ state: 'visible', timeout: 8000 })
        await allocationModal.getByRole('button', { name: /Lưu phân bổ/ }).click()
        await page.waitForTimeout(500)
      }
    } catch {
      console.warn('Không thể chọn phân bổ mẫu.')
    }
    await actualFormLocator.screenshot({ path: actualFormPath })
  } else {
    await page.screenshot({ path: actualFormPath, fullPage: true })
  }

  let modalCaptured = false
  try {
    await page.getByRole('button', { name: 'Tùy chọn nâng cao' }).click({ timeout: 5000 })
    const actualModal = page.locator('.modal').filter({ hasText: 'Tùy chọn nâng cao' })
    await actualModal.waitFor({ state: 'visible', timeout: 10000 })
    try {
      await actualModal.getByLabel('Vượt khóa kỳ khi duyệt').check()
    } catch {
      console.warn('Không thể bật "Vượt khóa kỳ khi duyệt".')
    }
    try {
      await actualModal
        .locator('input[placeholder="Bắt buộc nếu vượt khóa kỳ"]')
        .fill('Khách yêu cầu ghi nhận gấp')
    } catch {
      console.warn('Không thể điền lý do vượt khóa kỳ.')
    }
    await actualModal.screenshot({ path: actualModalPath })
    modalCaptured = true
  } catch {
    console.warn('Không mở được modal "Tùy chọn nâng cao", chụp toàn trang.')
  }
  if (!modalCaptured) {
    await page.screenshot({ path: actualModalPath, fullPage: true })
  }
  await page.close()

  const formDiff = await diffImages(previewFormPath, actualFormPath, diffFormPath)
  const modalDiff = await diffImages(previewModalPath, actualModalPath, diffModalPath)

  const summary = {
    generatedAt: new Date().toISOString(),
    outputDir,
    form: { ...formDiff, preview: previewFormPath, actual: actualFormPath, diff: diffFormPath },
    modal: { ...modalDiff, preview: previewModalPath, actual: actualModalPath, diff: diffModalPath },
  }

  await fs.writeFile(
    path.join(outputDir, 'summary.json'),
    JSON.stringify(summary, null, 2),
  )

  console.log('Visual compare completed.')
  console.log(`Form diff pixels: ${formDiff.diffPixels}`)
  console.log(`Modal diff pixels: ${modalDiff.diffPixels}`)
  console.log(`Output: ${outputDir}`)

  if (process.env.FAIL_ON_DIFF === '1' && (formDiff.diffPixels > 0 || modalDiff.diffPixels > 0)) {
    process.exit(1)
  }
} finally {
  await context.close()
  await browser.close()
  if (serverProcess) {
    if (process.platform === 'win32') {
      spawn('taskkill', ['/PID', String(serverProcess.pid), '/T', '/F'], { shell: true })
    } else {
      serverProcess.kill('SIGTERM')
    }
  }
}
