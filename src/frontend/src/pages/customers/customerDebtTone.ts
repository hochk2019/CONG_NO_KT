export const CUSTOMER_DEBT_WARNING_THRESHOLD = 100_000_000
export const CUSTOMER_DEBT_DANGER_THRESHOLD = 500_000_000

export const getDebtToneClass = (currentBalance: number) => {
  if (currentBalance <= 0) return 'debt-value--clear'
  if (currentBalance >= CUSTOMER_DEBT_DANGER_THRESHOLD) return 'debt-value--high'
  if (currentBalance >= CUSTOMER_DEBT_WARNING_THRESHOLD) return 'debt-value--medium'
  return 'debt-value--normal'
}
