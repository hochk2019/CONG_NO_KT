import type { ReportInsights, ReportOverdueGroup, ReportTopCustomer } from '../../api/reports'
import { formatMoney } from '../../utils/format'

type ReportsInsightsSectionProps = {
  insights: ReportInsights | null
  loading: boolean
  topOutstandingCount?: number
  topOutstandingOptions?: number[]
  onTopOutstandingCountChange?: (value: number) => void
}

const formatRatio = (ratio?: number | null) => {
  if (ratio === null || ratio === undefined) return null
  const percent = Math.round(ratio * 1000) / 10
  return `${percent}% đã trả`
}

const renderTopList = (
  rows: ReportTopCustomer[],
  emptyMessage: string,
  buildMeta: (row: ReportTopCustomer) => string | null,
  loading: boolean,
) => {
  if (loading) {
    return <div className="empty-state">Đang tải dữ liệu...</div>
  }

  if (rows.length === 0) {
    return <div className="empty-state">{emptyMessage}</div>
  }

  return (
    <div>
      {rows.map((row) => {
        const meta = buildMeta(row)
        return (
          <div className="list-row" key={row.customerTaxCode}>
            <div>
              <div className="list-title">{row.customerName}</div>
              <div className="muted">{row.customerTaxCode}</div>
            </div>
            <div className="list-meta">
              <div>{formatMoney(row.amount)}</div>
              {meta && <span className="muted">{meta}</span>}
            </div>
          </div>
        )
      })}
    </div>
  )
}

const renderOverdueByOwner = (
  rows: ReportOverdueGroup[],
  emptyMessage: string,
  loading: boolean,
) => {
  if (loading) {
    return <div className="empty-state">Đang tải dữ liệu...</div>
  }

  if (rows.length === 0) {
    return <div className="empty-state">{emptyMessage}</div>
  }

  return (
    <div>
      {rows.map((row) => {
        const percent = Math.round(row.overdueRatio * 1000) / 10
        return (
          <div className="list-row" key={row.groupKey}>
            <div>
              <div className="list-title">{row.groupName}</div>
              <div className="muted">{row.overdueCustomers} KH quá hạn</div>
            </div>
            <div className="list-meta">
              <div>{Number.isFinite(percent) ? `${percent}%` : '-'}</div>
              <span className="muted">{formatMoney(row.overdueAmount)}</span>
            </div>
          </div>
        )
      })}
    </div>
  )
}

export function ReportsInsightsSection({
  insights,
  loading,
  topOutstandingCount,
  topOutstandingOptions,
  onTopOutstandingCountChange,
}: ReportsInsightsSectionProps) {
  const topOutstanding = insights?.topOutstanding ?? []
  const topOnTime = insights?.topOnTime ?? []
  const overdueByOwner = insights?.overdueByOwner ?? []
  const options = topOutstandingOptions?.length ? topOutstandingOptions : [5, 10]
  const selectedTop = topOutstandingCount ?? options[0] ?? 5
  const emptyMessage = 'Không có dữ liệu trong kỳ đã chọn'

  return (
    <section className="reports-insights">
      <div className="reports-insights__column">
        <section className="card">
          <div className="card-row">
            <h3>Top cần chú ý</h3>
            <label className="table-page-size">
              <span>Hiển thị</span>
              <select
                value={selectedTop}
                onChange={(event) =>
                  onTopOutstandingCountChange?.(Number(event.target.value))
                }
                disabled={loading}
              >
                {options.map((value) => (
                  <option key={value} value={value}>
                    {value}
                  </option>
                ))}
              </select>
            </label>
          </div>
          <p className="muted">Khách hàng có dư nợ cao nhất trong kỳ.</p>
          {renderTopList(
            topOutstanding,
            emptyMessage,
            (row) => (row.daysPastDue !== null && row.daysPastDue !== undefined ? `${row.daysPastDue} ngày` : null),
            loading,
          )}
        </section>
      </div>
      <div className="reports-insights__column">
        <section className="card">
          <h3>Top trả đúng hạn nhất</h3>
          <p className="muted">Chỉ tính khách hàng phát sinh trong kỳ.</p>
          {renderTopList(
            topOnTime,
            emptyMessage,
            (row) => formatRatio(row.ratio),
            loading,
          )}
        </section>
        <section className="card">
          <h3>Quá hạn theo phụ trách</h3>
          <p className="muted">So sánh mức quá hạn theo phụ trách.</p>
          {renderOverdueByOwner(
            overdueByOwner,
            emptyMessage,
            loading,
          )}
        </section>
      </div>
    </section>
  )
}
