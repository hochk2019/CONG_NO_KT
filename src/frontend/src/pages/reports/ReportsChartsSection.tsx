import { useMemo } from 'react'
import type { ReportCharts } from '../../api/reports'
import { formatMoney } from '../../utils/format'

type ReportsChartsSectionProps = {
  charts: ReportCharts | null
  loading: boolean
}

const buildLinePath = (
  values: number[],
  width: number,
  height: number,
  padding: number,
  maxValue: number,
) => {
  if (!values.length) return ''
  const safeMax = maxValue || 1
  const step = values.length > 1 ? (width - padding * 2) / (values.length - 1) : 0
  return values
    .map((value, index) => {
      const x = padding + index * step
      const ratio = Math.min(1, Math.max(0, value / safeMax))
      const y = padding + (height - padding * 2) * (1 - ratio)
      return `${index === 0 ? 'M' : 'L'}${x},${y}`
    })
    .join(' ')
}

export function ReportsChartsSection({ charts, loading }: ReportsChartsSectionProps) {
  const cashFlowValues = useMemo(
    () => charts?.cashFlow.map((point) => point.value) ?? [],
    [charts],
  )
  const maxValue = useMemo(() => {
    if (!cashFlowValues.length) return 0
    return Math.max(...cashFlowValues)
  }, [cashFlowValues])

  const agingSegments = useMemo(() => {
    const distribution = charts?.agingDistribution
    if (!distribution) {
      return {
        total: 0,
        items: [] as { label: string; amount: number; index: number; percent: number }[],
      }
    }
    const items = [
      { label: '0-30', amount: distribution.bucket0To30 },
      { label: '31-60', amount: distribution.bucket31To60 },
      { label: '61-90', amount: distribution.bucket61To90 },
      { label: '91-180', amount: distribution.bucket91To180 },
      { label: '>180', amount: distribution.bucketOver180 },
    ]
    const total = items.reduce((sum, item) => sum + item.amount, 0)
    return {
      total,
      items: items.map((item, index) => ({
        ...item,
        index,
        percent: total > 0 ? Math.round((item.amount / total) * 1000) / 10 : 0,
      })),
    }
  }, [charts])

  const allocationSummary = useMemo(() => {
    const rows = charts?.allocationStatuses ?? []
    const bucket = {
      ALLOCATED: 0,
      PARTIAL: 0,
      UNALLOCATED: 0,
    }

    rows.forEach((row) => {
      const key = row.status.toUpperCase()
      if (key === 'ALLOCATED') bucket.ALLOCATED += row.amount
      else if (key === 'PARTIAL') bucket.PARTIAL += row.amount
      else bucket.UNALLOCATED += row.amount
    })

    const total = bucket.ALLOCATED + bucket.PARTIAL + bucket.UNALLOCATED

    return {
      total,
      items: [
        { key: 'ALLOCATED', label: 'Đã phân bổ', amount: bucket.ALLOCATED },
        { key: 'PARTIAL', label: 'Phân bổ một phần', amount: bucket.PARTIAL },
        { key: 'UNALLOCATED', label: 'Chưa phân bổ', amount: bucket.UNALLOCATED },
      ].map((item) => ({
        ...item,
        percent: total > 0 ? Math.round((item.amount / total) * 1000) / 10 : 0,
      })),
    }
  }, [charts])

  return (
    <section className="reports-charts">
      <section className="card">
        <h3>Luồng tiền thu theo ngày</h3>
        <p className="muted">Dòng tiền thu theo ngày trong kỳ đã chọn.</p>
        {loading ? (
          <div className="empty-state">Đang tải biểu đồ...</div>
        ) : cashFlowValues.length > 0 ? (
          <>
            <div className="line-chart">
              <svg viewBox="0 0 600 220" className="line-chart__svg" aria-hidden="true">
                {[0, 1, 2, 3].map((index) => (
                  <line
                    key={index}
                    x1="40"
                    x2="560"
                    y1={40 + index * 40}
                    y2={40 + index * 40}
                    className="line-chart__grid"
                  />
                ))}
                <path
                  className="line-chart__path"
                  d={buildLinePath(cashFlowValues, 560, 200, 40, maxValue)}
                  style={{ stroke: 'var(--color-accent)' }}
                />
                {cashFlowValues.map((value, idx) => {
                  const ratio = maxValue ? Math.min(1, value / maxValue) : 0
                  const step = cashFlowValues.length > 1 ? (560 - 80) / (cashFlowValues.length - 1) : 0
                  const x = 40 + idx * step
                  const y = 40 + (200 - 80) * (1 - ratio)
                  return (
                    <circle key={idx} cx={x} cy={y} r={3.5} fill="var(--color-accent)">
                      <title>{formatMoney(value)}</title>
                    </circle>
                  )
                })}
              </svg>
              <div className="line-chart__labels">
                {charts?.cashFlow.map((point) => (
                  <span key={point.date}>{point.date}</span>
                ))}
              </div>
            </div>
            <div className="chart-legend">
              <span style={{ color: 'var(--color-accent)' }}>■ Phiếu thu</span>
            </div>
          </>
        ) : (
          <div className="empty-state">Không có dữ liệu trong kỳ đã chọn.</div>
        )}
      </section>

      <section className="card">
        <h3>Tuổi nợ</h3>
        <p className="muted">Tỷ trọng công nợ theo nhóm ngày quá hạn.</p>
        {loading ? (
          <div className="empty-state">Đang tải tuổi nợ...</div>
        ) : agingSegments.total > 0 ? (
          <>
            <div className="aging-bar">
              {agingSegments.items.map((item) => (
                <div
                  key={item.label}
                  className={`aging-segment aging-segment--${item.index}`}
                  style={{ width: `${item.percent}%` }}
                  title={`${item.label}: ${formatMoney(item.amount)}`}
                />
              ))}
            </div>
            <div className="aging-legend">
              {agingSegments.items.map((item) => (
                <div key={item.label} className="aging-legend__item">
                  <span className={`legend-dot legend-dot--bucket${item.index}`} />
                  <span>{item.label}</span>
                  <span className="muted">{formatMoney(item.amount)}</span>
                </div>
              ))}
            </div>
          </>
        ) : (
          <div className="empty-state">Khách hàng không còn khoản nợ nào trên hệ thống</div>
        )}
      </section>

      <section className="card">
        <h3>Trạng thái phân bổ</h3>
        <p className="muted">Tỷ trọng phiếu thu theo tình trạng phân bổ.</p>
        {loading ? (
          <div className="empty-state">Đang tải trạng thái phân bổ...</div>
        ) : allocationSummary.total > 0 ? (
          <div className="chart">
            {allocationSummary.items.map((item) => (
              <div className="chart-column" key={item.key}>
                <div className="chart-total">{item.percent}%</div>
                <div className="chart-bars">
                  <div
                    className={`chart-bar ${
                      item.key === 'ALLOCATED'
                        ? 'chart-bar--invoice'
                        : item.key === 'PARTIAL'
                        ? 'chart-bar--advance'
                        : 'chart-bar--receipt'
                    }`}
                    style={{ height: `${Math.max(6, item.percent)}%` }}
                    title={`${item.label}: ${formatMoney(item.amount)}`}
                  />
                </div>
                <div className="chart-label">{item.label}</div>
              </div>
            ))}
          </div>
        ) : (
          <div className="empty-state">Không có dữ liệu trong kỳ đã chọn.</div>
        )}
      </section>
    </section>
  )
}
