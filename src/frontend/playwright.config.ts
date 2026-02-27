import { defineConfig } from '@playwright/test'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)

const parseEnvFile = (filePath: string): Record<string, string> => {
  if (!fs.existsSync(filePath)) {
    return {}
  }

  return fs
    .readFileSync(filePath, 'utf8')
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line && !line.startsWith('#') && line.includes('='))
    .reduce<Record<string, string>>((acc, line) => {
      const index = line.indexOf('=')
      const key = line.slice(0, index).trim()
      const value = line.slice(index + 1).trim()
      if (key) {
        acc[key] = value
      }
      return acc
    }, {})
}

const rootEnv = parseEnvFile(path.resolve(__dirname, '../.env'))

const applyEnvDefault = (key: string, value?: string) => {
  if (!process.env[key] && value) {
    process.env[key] = value
  }
}

applyEnvDefault('E2E_USERNAME', rootEnv.E2E_USERNAME ?? rootEnv.SEED_ADMIN_USERNAME ?? 'admin')
applyEnvDefault('E2E_PASSWORD', rootEnv.E2E_PASSWORD ?? rootEnv.SEED_ADMIN_PASSWORD ?? 'Sam0905@')
applyEnvDefault('VITE_API_PROXY_TARGET', process.env.E2E_API_TARGET ?? rootEnv.E2E_API_TARGET ?? 'http://127.0.0.1:18080')

export default defineConfig({
  testDir: './e2e',
  workers: 1,
  timeout: 30_000,
  expect: {
    timeout: 5_000,
  },
  use: {
    baseURL: 'http://127.0.0.1:5173',
    trace: 'on-first-retry',
  },
  webServer: {
    command: 'npm run dev -- --host 127.0.0.1 --port 5173',
    env: {
      ...process.env,
      VITE_API_PROXY_TARGET: process.env.VITE_API_PROXY_TARGET ?? 'http://127.0.0.1:18080',
    },
    url: 'http://127.0.0.1:5173',
    reuseExistingServer: false,
    timeout: 120_000,
  },
})
