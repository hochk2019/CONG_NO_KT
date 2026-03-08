const NON_MONEY_CHARACTER_PATTERN = /[^0-9.,]/g
const NON_DIGIT_PATTERN = /\D/g
const RAW_MONEY_CHARACTER_PATTERN = /[^0-9.]/g
const GROUPING_SEPARATOR = '.'
const DECIMAL_SEPARATOR = ','

const stripNonDigits = (value: string) => value.replace(NON_DIGIT_PATTERN, '')

const normalizeIntegerPart = (value: string) => {
  const digits = stripNonDigits(value)
  if (!digits) return '0'
  return digits.replace(/^0+(?=\d)/, '') || '0'
}

export const normalizeMoneyInput = (value: string) => {
  const cleaned = value.replace(NON_MONEY_CHARACTER_PATTERN, '')
  if (!cleaned) return ''

  const decimalIndex = cleaned.lastIndexOf(DECIMAL_SEPARATOR)
  if (decimalIndex === -1) {
    return stripNonDigits(cleaned)
  }

  const integerPart = stripNonDigits(cleaned.slice(0, decimalIndex))
  const fractionPart = stripNonDigits(cleaned.slice(decimalIndex + 1))
  const normalizedInteger = integerPart || '0'

  if (decimalIndex === cleaned.length - 1) {
    return `${normalizedInteger}.`
  }

  return fractionPart ? `${normalizedInteger}.${fractionPart}` : normalizedInteger
}

export const formatMoneyInput = (value: string) => {
  const cleaned = value.replace(RAW_MONEY_CHARACTER_PATTERN, '')
  if (!cleaned) return ''

  const hasTrailingDecimal = cleaned.endsWith('.')
  const [integerPartRaw, fractionPartRaw = ''] = cleaned.split('.')
  const integerPart = normalizeIntegerPart(integerPartRaw).replace(
    /\B(?=(\d{3})+(?!\d))/g,
    GROUPING_SEPARATOR,
  )

  if (hasTrailingDecimal) {
    return `${integerPart}${DECIMAL_SEPARATOR}`
  }

  if (!fractionPartRaw) {
    return integerPart
  }

  return `${integerPart}${DECIMAL_SEPARATOR}${stripNonDigits(fractionPartRaw)}`
}
