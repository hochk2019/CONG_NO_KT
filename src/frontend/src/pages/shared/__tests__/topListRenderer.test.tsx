import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { renderTopList } from '../topListRenderer'

type Row = {
  customerTaxCode: string
  customerName: string
  amount: number
  daysPastDue?: number | null
}

describe('renderTopList', () => {
  it('renders loading state', () => {
    render(
      <>{renderTopList<Row>({ rows: [], emptyMessage: 'empty', formatAmount: String, loading: true })}</>,
    )

    expect(screen.getByText('Đang tải dữ liệu...')).toBeInTheDocument()
  })

  it('uses custom empty renderer when no rows', () => {
    render(
      <>
        {renderTopList<Row>({
          rows: [],
          emptyMessage: 'Không có dữ liệu',
          formatAmount: String,
          emptyRenderer: (message) => <div data-testid="custom-empty">{message}</div>,
        })}
      </>,
    )

    expect(screen.getByTestId('custom-empty')).toHaveTextContent('Không có dữ liệu')
  })

  it('renders rows and optional meta text', () => {
    const rows: Row[] = [
      { customerTaxCode: '0101', customerName: 'Công ty A', amount: 1200, daysPastDue: 5 },
    ]

    render(
      <>
        {renderTopList<Row>({
          rows,
          emptyMessage: 'empty',
          formatAmount: (value) => `${value} đ`,
          buildMeta: (row) =>
            row.daysPastDue !== null && row.daysPastDue !== undefined ? `${row.daysPastDue} ngày` : null,
        })}
      </>,
    )

    expect(screen.getByText('Công ty A')).toBeInTheDocument()
    expect(screen.getByText('0101')).toBeInTheDocument()
    expect(screen.getByText('1200 đ')).toBeInTheDocument()
    expect(screen.getByText('5 ngày')).toBeInTheDocument()
  })
})
