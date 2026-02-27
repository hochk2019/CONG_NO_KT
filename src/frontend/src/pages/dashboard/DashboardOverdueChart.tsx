import EmptyState from '../../components/EmptyState'
import Skeleton from '../../components/Skeleton'
import type { DashboardOverdueGroupItem } from '../../api/dashboard'
import { formatMoney } from '../../utils/format'

type DashboardOverdueChartProps = {
  rows: DashboardOverdueGroupItem[]
  loading: boolean
  error: string | null
}

export default function DashboardOverdueChart({ rows, loading, error }: DashboardOverdueChartProps) {
  if (loading) {
    return (
      <div className="overdue-chart overdue-chart--loading" role="status" aria-label="Đang tải quá hạn theo phụ trách">
        {Array.from({ length: 5 }).map((_, index) => (
          <div className="overdue-chart__row" key={index}>
            <div className="overdue-chart__header">
              <Skeleton width="42%" height="0.9rem" />
              <Skeleton width="26%" height="0.85rem" />
            </div>
            <Skeleton width="100%" height="0.6rem" borderRadius="999px" />
          </div>
        ))}
      </div>
    )
  }

  if (error) {
    return (
      <div className="alert alert--error" role="alert" aria-live="assertive">
        {error}
      </div>
    )
  }

  if (rows.length === 0) {
    return (
      <EmptyState
        title="Chưa có dữ liệu quá hạn theo phụ trách"
        description="Biểu đồ sẽ hiển thị khi phát sinh khách hàng quá hạn trong kỳ."
        icon="🧾"
        compact
      />
    )
  }

  const maxOverdueAmount = Math.max(...rows.map((item) => item.overdueAmount), 1)

  return (
    <div className="overdue-chart">
      {rows.map((row) => {
        const ratio = Math.max(6, Math.round((row.overdueAmount / maxOverdueAmount) * 100))
        const overduePercent = Math.round(row.overdueRatio * 1000) / 10

        return (
          <div className="overdue-chart__row" key={row.groupKey}>
            <div className="overdue-chart__header">
              <div>
                <div className="overdue-chart__title">{row.groupName}</div>
                <div className="muted">{row.overdueCustomers} khách hàng quá hạn</div>
              </div>
              <div className="overdue-chart__value">
                <strong>{formatMoney(row.overdueAmount)}</strong>
                <span className="muted">{Number.isFinite(overduePercent) ? `${overduePercent}%` : '-'}</span>
              </div>
            </div>
            <div className="overdue-chart__track" aria-hidden="true">
              <div className="overdue-chart__bar" style={{ width: `${ratio}%` }} />
            </div>
          </div>
        )
      })}
    </div>
  )
}
