import { readdirSync, statSync } from 'node:fs'
import { join } from 'node:path'

const assetsDir = join(process.cwd(), 'dist', 'assets')

const formatKb = (bytes) => (bytes / 1024).toFixed(2)

const files = readdirSync(assetsDir)
const getByRegex = (regex) => files.find((file) => regex.test(file))

const checks = [
  {
    name: 'framework chunk',
    regex: /^framework-.*\.js$/,
    maxBytes: 250 * 1024,
  },
  {
    name: 'entry chunk',
    regex: /^index-.*\.js$/,
    maxBytes: 40 * 1024,
  },
  {
    name: 'entry stylesheet',
    regex: /^index-.*\.css$/,
    maxBytes: 45 * 1024,
  },
]

const pageChunks = files.filter((file) => /Page-.*\.js$/.test(file))
if (pageChunks.length === 0) {
  throw new Error('No page chunks found (expected files matching *Page-*.js).')
}

const maxPageChunk = pageChunks
  .map((file) => ({ file, bytes: statSync(join(assetsDir, file)).size }))
  .sort((a, b) => b.bytes - a.bytes)[0]

const errors = []

for (const check of checks) {
  const file = getByRegex(check.regex)
  if (!file) {
    errors.push(`${check.name}: missing chunk matching ${check.regex}`)
    continue
  }

  const bytes = statSync(join(assetsDir, file)).size
  if (bytes > check.maxBytes) {
    errors.push(
      `${check.name} (${file}) exceeds budget ${formatKb(check.maxBytes)} KB with ${formatKb(bytes)} KB`,
    )
  } else {
    console.log(`${check.name}: ${file} ${formatKb(bytes)} KB (budget ${formatKb(check.maxBytes)} KB)`)
  }
}

const maxPageBudgetBytes = 60 * 1024
if (maxPageChunk.bytes > maxPageBudgetBytes) {
  errors.push(
    `largest page chunk (${maxPageChunk.file}) exceeds budget ${formatKb(maxPageBudgetBytes)} KB with ${formatKb(maxPageChunk.bytes)} KB`,
  )
} else {
  console.log(
    `largest page chunk: ${maxPageChunk.file} ${formatKb(maxPageChunk.bytes)} KB (budget ${formatKb(maxPageBudgetBytes)} KB)`,
  )
}

if (errors.length > 0) {
  console.error('\nBundle budget check failed:')
  for (const error of errors) {
    console.error(`- ${error}`)
  }
  process.exitCode = 1
} else {
  console.log('\nBundle budget check passed.')
}
