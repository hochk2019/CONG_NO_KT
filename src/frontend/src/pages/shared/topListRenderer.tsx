import type { ReactNode } from 'react'

type BaseTopListRow = {
  customerTaxCode: string
  customerName: string
  amount: number
}

type RenderTopListOptions<T extends BaseTopListRow> = {
  rows: T[]
  emptyMessage: string
  formatAmount: (value: number) => string
  buildMeta?: (row: T) => string | null
  loading?: boolean
  loadingMessage?: string
  emptyRenderer?: (message: string) => ReactNode
}

export const renderTopList = <T extends BaseTopListRow>({
  rows,
  emptyMessage,
  formatAmount,
  buildMeta,
  loading = false,
  loadingMessage = 'Đang tải dữ liệu...',
  emptyRenderer,
}: RenderTopListOptions<T>): ReactNode => {
  if (loading) {
    return <div className="empty-state">{loadingMessage}</div>
  }

  if (rows.length === 0) {
    if (emptyRenderer) {
      return emptyRenderer(emptyMessage)
    }
    return <div className="empty-state">{emptyMessage}</div>
  }

  return (
    <div>
      {rows.map((row) => {
        const meta = buildMeta?.(row)
        return (
          <div className="list-row" key={row.customerTaxCode}>
            <div>
              <div className="list-title">{row.customerName}</div>
              <div className="muted">{row.customerTaxCode}</div>
            </div>
            <div className="list-meta">
              <div>{formatAmount(row.amount)}</div>
              {meta && <span className="muted">{meta}</span>}
            </div>
          </div>
        )
      })}
    </div>
  )
}
