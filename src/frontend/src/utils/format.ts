const moneyFormatter = new Intl.NumberFormat('vi-VN', {
  maximumFractionDigits: 2,
})

const dateFormatter = new Intl.DateTimeFormat('vi-VN', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
})

const dateTimeFormatter = new Intl.DateTimeFormat('vi-VN', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
})

const dateOnlyRegex = /^\d{4}-\d{2}-\d{2}$/
const slashDateRegex = /^(\d{1,4})\/(\d{1,2})\/(\d{1,4})$/

type DateInput = string | Date | null | undefined

const pad2 = (value: string | number) => String(value).padStart(2, '0')

const normalizeSlashDate = (value: string) => {
  const match = slashDateRegex.exec(value)
  if (!match) return value

  const [, first, second, third] = match

  if (first.length === 4) {
    return `${pad2(third)}/${pad2(second)}/${first}`
  }

  if (third.length === 4) {
    const firstNumber = Number(first)
    const secondNumber = Number(second)

    if (firstNumber <= 12 && secondNumber > 12) {
      return `${pad2(secondNumber)}/${pad2(firstNumber)}/${third}`
    }

    return `${pad2(firstNumber)}/${pad2(secondNumber)}/${third}`
  }

  return value
}

export const formatMoney = (value: number | null | undefined) => {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '-'
  }
  return `${moneyFormatter.format(value)} đ`
}

export const formatDate = (value: DateInput) => {
  if (!value) {
    return '-'
  }

  if (value instanceof Date) {
    return dateFormatter.format(value)
  }

  const trimmed = value.trim()
  if (!trimmed) {
    return '-'
  }

  if (trimmed.includes('/')) {
    return normalizeSlashDate(trimmed)
  }

  if (dateOnlyRegex.test(trimmed)) {
    const [year, month, day] = trimmed.split('-')
    return `${day}/${month}/${year}`
  }

  const parsed = new Date(trimmed)
  if (!Number.isNaN(parsed.getTime())) {
    return dateFormatter.format(parsed)
  }

  return trimmed
}

export const formatDateTime = (value: DateInput) => {
  if (!value) {
    return '-'
  }

  if (value instanceof Date) {
    return dateTimeFormatter.format(value)
  }

  const trimmed = value.trim()
  if (!trimmed) {
    return '-'
  }

  if (dateOnlyRegex.test(trimmed)) {
    const [year, month, day] = trimmed.split('-')
    return `${day}/${month}/${year}`
  }

  const parsed = new Date(trimmed)
  if (!Number.isNaN(parsed.getTime())) {
    return dateTimeFormatter.format(parsed)
  }

  return trimmed
}
