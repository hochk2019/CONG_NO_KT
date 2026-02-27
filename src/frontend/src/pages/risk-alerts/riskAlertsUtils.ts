export const riskLabels: Record<string, string> = {
  VERY_HIGH: 'Rất cao',
  HIGH: 'Cao',
  MEDIUM: 'Trung bình',
  LOW: 'Thấp',
}

export const aiSignalLabels: Record<string, string> = {
  CRITICAL: 'Nguy cơ rất cao',
  HIGH: 'Nguy cơ cao',
  MEDIUM: 'Nguy cơ trung bình',
  LOW: 'Nguy cơ thấp',
}

export const channelLabels: Record<string, string> = {
  IN_APP: 'In-app',
  ZALO: 'Zalo',
}

export const statusLabels: Record<string, string> = {
  SENT: 'Đã gửi',
  FAILED: 'Lỗi',
  SKIPPED: 'Bỏ qua',
}

export const toDateInput = (value: Date) => {
  const year = value.getFullYear()
  const month = String(value.getMonth() + 1).padStart(2, '0')
  const day = String(value.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

export const resolveRiskPillClass = (level: string) => {
  switch (level) {
    case 'VERY_HIGH':
      return 'risk-pill risk-pill--very-high'
    case 'HIGH':
      return 'risk-pill risk-pill--high'
    case 'MEDIUM':
      return 'risk-pill risk-pill--medium'
    default:
      return 'risk-pill risk-pill--low'
  }
}

export const resolveAiSignalPillClass = (signal: string) => {
  switch (signal) {
    case 'CRITICAL':
      return 'risk-pill risk-pill--very-high'
    case 'HIGH':
      return 'risk-pill risk-pill--high'
    case 'MEDIUM':
      return 'risk-pill risk-pill--medium'
    default:
      return 'risk-pill risk-pill--low'
  }
}

export const formatRatio = (value: number) => {
  if (!Number.isFinite(value)) {
    return '-'
  }
  return `${Math.round(value * 1000) / 10}%`
}

export const toPercentInput = (value: number) => {
  if (!Number.isFinite(value)) {
    return 0
  }
  return Math.round(value * 1000) / 10
}
