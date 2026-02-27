import { useMemo, useState } from 'react'
import { formatMoney } from '../../utils/format'

export type AllocationStatusKey = 'ALLOCATED' | 'PARTIAL' | 'UNALLOCATED'

export type AllocationSummaryItem = {
  key: AllocationStatusKey
  label: string
  amount: number
  percent: number
}

export type AllocationSummary = {
  total: number
  items: AllocationSummaryItem[]
}

type AllocationDonutCardProps = {
  summary: AllocationSummary
  onDrilldown: (status: AllocationStatusKey) => void
}

const colorByStatus: Record<AllocationStatusKey, string> = {
  ALLOCATED: 'var(--color-success)',
  PARTIAL: 'var(--color-warning)',
  UNALLOCATED: 'var(--color-danger)',
}

export default function AllocationDonutCard({ summary, onDrilldown }: AllocationDonutCardProps) {
  const [selectedStatus, setSelectedStatus] = useState<AllocationStatusKey | null>(null)

  const donutBackground = useMemo(() => {
    if (summary.total <= 0 || summary.items.length === 0) {
      return 'conic-gradient(var(--color-border) 0% 100%)'
    }

    let cursor = 0
    const segments = summary.items.map((item, index) => {
      const start = cursor
      const end = index === summary.items.length - 1 ? 100 : Math.min(100, cursor + item.percent)
      cursor = end
      return `${colorByStatus[item.key]} ${start}% ${end}%`
    })

    return `conic-gradient(${segments.join(', ')})`
  }, [summary])

  return (
    <section className="card">
      <h3>Trạng thái phân bổ</h3>
      <p className="muted">Donut chart + drill-down theo trạng thái phân bổ phiếu thu.</p>

      {summary.total > 0 ? (
        <div className="allocation-donut-layout">
          <button
            className="allocation-donut"
            type="button"
            style={{ background: donutBackground }}
            onClick={() => setSelectedStatus(null)}
            aria-label="Biểu đồ trạng thái phân bổ"
          >
            <span className="allocation-donut__center">
              <strong>{formatMoney(summary.total)}</strong>
              <small>Tổng phân bổ</small>
            </span>
          </button>

          <div className="allocation-donut__legend" role="list">
            {summary.items.map((item) => (
              <button
                key={item.key}
                type="button"
                role="listitem"
                className={`allocation-donut__legend-item${
                  selectedStatus === item.key ? ' allocation-donut__legend-item--active' : ''
                }`}
                onClick={() =>
                  setSelectedStatus((current) => (current === item.key ? null : item.key))
                }
              >
                <span
                  className="allocation-donut__swatch"
                  style={{ backgroundColor: colorByStatus[item.key] }}
                  aria-hidden
                />
                <span className="allocation-donut__name">{item.label}</span>
                <span className="allocation-donut__percent">{item.percent}%</span>
                <span className="allocation-donut__amount">{formatMoney(item.amount)}</span>
              </button>
            ))}
          </div>
        </div>
      ) : (
        <div className="empty-state">Chưa có dữ liệu phân bổ.</div>
      )}

      {selectedStatus && (
        <div className="allocation-donut__drilldown">
          <span>
            Đang chọn: <strong>{summary.items.find((item) => item.key === selectedStatus)?.label}</strong>
          </span>
          <button
            type="button"
            className="btn btn-outline"
            onClick={() => onDrilldown(selectedStatus)}
          >
            Xem chi tiết phiếu thu
          </button>
        </div>
      )}
    </section>
  )
}
