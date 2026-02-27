import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { RiskCustomerItem } from '../../../api/risk'
import { buildRiskCustomerColumns } from '../riskAlertColumns'

const sampleRow: RiskCustomerItem = {
  customerTaxCode: '0312345678',
  customerName: 'Cong ty Alpha',
  ownerId: '11111111-1111-1111-1111-111111111111',
  ownerName: 'Nguyen Van A',
  totalOutstanding: 180_000_000,
  overdueAmount: 120_000_000,
  overdueRatio: 0.667,
  maxDaysPastDue: 42,
  lateCount: 3,
  riskLevel: 'HIGH',
  predictedOverdueProbability: 0.679,
  aiSignal: 'HIGH',
  aiFactors: [
    {
      code: 'OVERDUE_RATIO',
      label: 'Tỷ lệ quá hạn',
      rawValue: 0.667,
      normalizedValue: 0.667,
      weight: 0.48,
      contribution: 0.3202,
    },
    {
      code: 'MAX_DAYS_PAST_DUE',
      label: 'Số ngày quá hạn lớn nhất',
      rawValue: 42,
      normalizedValue: 0.4667,
      weight: 0.27,
      contribution: 0.126,
    },
  ],
  aiRecommendation: 'Liên hệ xác nhận kế hoạch thanh toán trong 48h.',
}

describe('risk alert columns', () => {
  it('renders AI signal column with explainability and recommendation', () => {
    const columns = buildRiskCustomerColumns()
    const aiColumn = columns.find((column) => column.key === 'aiSignal')

    expect(aiColumn).toBeDefined()
    expect(aiColumn?.label).toBe('AI dự báo')

    render(<>{aiColumn?.render?.(sampleRow)}</>)

    expect(screen.getByText('Nguy cơ cao')).toBeInTheDocument()
    expect(screen.getByText('67.9%')).toBeInTheDocument()
    expect(screen.getByText('Tỷ lệ quá hạn: 32%')).toBeInTheDocument()
    expect(screen.getByText('Số ngày quá hạn lớn nhất: 12.6%')).toBeInTheDocument()
    expect(screen.getByText('Liên hệ xác nhận kế hoạch thanh toán trong 48h.')).toBeInTheDocument()
  })
})
