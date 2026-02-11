import { useMemo, useState } from 'react'

const kpiPrimary = [
  { label: 'Tổng dư công nợ', value: '128,4 tỷ', meta: '+2,4% so với kỳ trước' },
  { label: 'Dư hóa đơn', value: '92,1 tỷ', meta: '1.245 hóa đơn' },
  { label: 'Dư trả hộ', value: '36,3 tỷ', meta: '842 khoản trả hộ' },
  { label: 'Đã thu chưa phân bổ', value: '4,9 tỷ', meta: '57 phiếu thu treo' },
  { label: 'Quá hạn', value: '18,7 tỷ', meta: '214 khách hàng' },
]

const kpiSecondary = [
  { label: 'Thu trong kỳ', value: '32,8 tỷ', meta: 'Tăng 6,2%' },
  { label: 'Phân bổ trong kỳ', value: '28,4 tỷ', meta: 'Tỷ lệ 86,5%' },
  { label: '% Thu/Phải thu', value: '72,4%', meta: 'Mục tiêu ≥ 75%' },
  { label: 'Phiếu thu chờ duyệt', value: '14', meta: '6 phiếu > 7 ngày' },
  { label: 'Lô nhập chờ xử lý', value: '3', meta: '1 lô lỗi định dạng' },
]

const cashFlowData = [
  { label: 'T2', invoice: 44, receipt: 32, advance: 18 },
  { label: 'T3', invoice: 38, receipt: 28, advance: 22 },
  { label: 'T4', invoice: 52, receipt: 36, advance: 14 },
  { label: 'T5', invoice: 60, receipt: 41, advance: 21 },
  { label: 'T6', invoice: 54, receipt: 39, advance: 19 },
  { label: 'T7', invoice: 48, receipt: 33, advance: 16 },
]

const lineSeriesBase = [
  { label: 'Hóa đơn', key: 'invoice', color: 'var(--color-accent)' },
  { label: 'Khoản trả hộ', key: 'advance', color: 'var(--color-success)' },
  { label: 'Phiếu thu', key: 'receipt', color: '#64748b' },
] as const

const allocationShare = [
  { label: 'Đã phân bổ', value: 78, className: 'chart-bar--invoice' },
  { label: 'Phân bổ một phần', value: 14, className: 'chart-bar--advance' },
  { label: 'Chưa phân bổ', value: 8, className: 'chart-bar--receipt' },
]

const topCustomers = [
  { name: 'CTY Hoàng Minh', taxCode: '2301098313', value: '6,8 tỷ', meta: 'Biến động 7 ngày +3%' },
  { name: 'CTY Hoàng Kim', taxCode: '2300328765', value: '5,4 tỷ', meta: 'Biến động 7 ngày +1,2%' },
  { name: 'CTY Tân Phát', taxCode: '0312456789', value: '4,1 tỷ', meta: 'Ổn định' },
  { name: 'CTY An Khang', taxCode: '0109876543', value: '3,7 tỷ', meta: 'Biến động 7 ngày -0,8%' },
]

const pendingTasks = [
  { title: 'Phiếu thu treo quá 10 ngày', value: '12 phiếu', meta: 'Cần duyệt trong tuần' },
  { title: 'Khoản trả hộ chờ đối soát', value: '7 khoản', meta: '3 khoản > 14 ngày' },
  { title: 'Hóa đơn quá hạn > 60 ngày', value: '25 hóa đơn', meta: 'Tổng 6,2 tỷ' },
]

const longestOverdue = [
  { name: 'CTY Hòa Phát', taxCode: '0316123456', value: '2,1 tỷ', meta: 'Quá hạn 126 ngày' },
  { name: 'CTY Hải Nam', taxCode: '0103344556', value: '1,8 tỷ', meta: 'Quá hạn 104 ngày' },
  { name: 'CTY Bình Minh', taxCode: '0319988776', value: '1,5 tỷ', meta: 'Quá hạn 92 ngày' },
]

const overdueByOwner = [
  { name: 'Nguyễn Thị Lan', value: '6,4 tỷ', meta: '18 KH quá hạn' },
  { name: 'Trần Minh Đức', value: '4,9 tỷ', meta: '12 KH quá hạn' },
  { name: 'Phạm Hồng Ngát', value: '3,7 tỷ', meta: '9 KH quá hạn' },
]

const quickActions = [
  { label: 'Tạo phiếu thu', value: 'Thu nhanh', meta: 'Ghi nhận & phân bổ' },
  { label: 'Duyệt phiếu thu', value: 'Chờ duyệt', meta: '14 phiếu' },
  { label: 'Xử lý lô nhập lỗi', value: 'Lô nhập', meta: '1 lô lỗi' },
  { label: 'Nhắc khách hàng', value: 'Quá hạn', meta: '214 KH' },
]

const buildLinePath = (values: number[], width: number, height: number, padding: number) => {
  const max = Math.max(...values)
  const min = Math.min(...values)
  const range = max - min || 1
  const step = values.length > 1 ? (width - padding * 2) / (values.length - 1) : 0
  return values
    .map((value, index) => {
      const x = padding + index * step
      const y = padding + (height - padding * 2) * (1 - (value - min) / range)
      return `${index === 0 ? 'M' : 'L'}${x},${y}`
    })
    .join(' ')
}

export default function DashboardPreviewPage() {
  const [unit, setUnit] = useState<'billion' | 'million'>('billion')
  const unitLabel = unit === 'billion' ? 'tỷ' : 'triệu'

  const lineSeries = useMemo(
    () =>
      lineSeriesBase.map((series) => ({
        label: series.label,
        color: series.color,
        values: cashFlowData.map((item) => item[series.key] as number),
      })),
    [],
  )

  return (
    <div className="dashboard-preview">
      <div className="page-header">
        <div>
          <h1 className="page-title">Tổng quan công nợ (Preview)</h1>
          <p className="muted">
            Dữ liệu demo để duyệt bố cục. Biểu đồ & KPI hiển thị theo ý tưởng thiết kế.
          </p>
        </div>
        <div className="dashboard-filters">
          <button className="filter-chip">Tháng này</button>
          <button className="filter-chip">Toàn công ty</button>
          <button className="filter-chip">Tất cả trạng thái</button>
          <button className="filter-chip filter-chip--ghost">Đặt lại</button>
        </div>
      </div>

      <section className="kpi-stack">
        <div className="stat-grid stat-grid--primary">
          {kpiPrimary.map((item) => (
            <div className="stat-card" key={item.label}>
              <div className="stat-card__label">{item.label}</div>
              <div className="stat-card__value">{item.value}</div>
              <div className="stat-card__meta">{item.meta}</div>
            </div>
          ))}
        </div>
        <div className="stat-grid stat-grid--secondary">
          {kpiSecondary.map((item) => (
            <div className="stat-card stat-card--secondary" key={item.label}>
              <div className="stat-card__label">{item.label}</div>
              <div className="stat-card__value">{item.value}</div>
              <div className="stat-card__meta">{item.meta}</div>
            </div>
          ))}
        </div>
      </section>

      <section className="dashboard-charts">
        <div className="card cashflow-card">
          <div className="card-row">
            <h3>Luồng tiền thu theo ngày</h3>
            <div className="unit-toggle" role="group" aria-label="Đổi đơn vị biểu đồ">
              <button
                className={`unit-toggle__btn ${unit === 'million' ? 'unit-toggle__btn--active' : ''}`}
                type="button"
                onClick={() => setUnit('million')}
              >
                Triệu
              </button>
              <button
                className={`unit-toggle__btn ${unit === 'billion' ? 'unit-toggle__btn--active' : ''}`}
                type="button"
                onClick={() => setUnit('billion')}
              >
                Tỷ
              </button>
            </div>
          </div>
          <p className="muted">So sánh hóa đơn, khoản trả hộ và phiếu thu</p>
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
              {lineSeries.map((series) => (
                <path
                  key={series.label}
                  d={buildLinePath(series.values, 560, 200, 40)}
                  className="line-chart__path"
                  style={{ stroke: series.color }}
                />
              ))}
            </svg>
            <div className="line-chart__labels">
              {cashFlowData.map((item) => (
                <span key={item.label}>{item.label}</span>
              ))}
            </div>
          </div>
          <div className="chart-legend">
            {lineSeries.map((series) => (
              <span key={series.label} style={{ color: series.color }}>
                ■ {series.label}
              </span>
            ))}
            <span className="chart-legend__unit">Đơn vị: {unitLabel}</span>
          </div>
        </div>

        <div className="card">
          <div className="card-row">
            <div>
              <h3>Trạng thái phân bổ</h3>
              <p className="muted">Đã phân bổ vs còn treo</p>
            </div>
            <div className="text-caption">%</div>
          </div>
          <div className="chart">
            {allocationShare.map((item) => (
              <div className="chart-column" key={item.label}>
                <div className="chart-total">{item.value}%</div>
                <div className="chart-bars">
                  <div
                    className={`chart-bar ${item.className}`}
                    style={{ height: `${item.value * 2.2}px` }}
                  />
                </div>
                <div className="chart-label">{item.label}</div>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="dashboard-panels">
        <div className="card">
          <div className="card-row">
            <h3>Top khách hàng dư nợ</h3>
            <span className="text-caption">Cập nhật hôm nay</span>
          </div>
          <div>
            {topCustomers.map((item) => (
              <div className="list-row" key={item.taxCode}>
                <div>
                  <div className="list-title">{item.name}</div>
                  <div className="muted">{item.taxCode}</div>
                </div>
                <div className="list-meta">
                  <div>{item.value}</div>
                  <span className="muted">{item.meta}</span>
                </div>
              </div>
            ))}
          </div>
        </div>

        <div className="card">
          <div className="card-row">
            <h3>Nhắc việc quan trọng</h3>
            <span className="text-caption">Cần xử lý</span>
          </div>
          <div>
            {pendingTasks.map((item) => (
              <div className="list-row" key={item.title}>
                <div>
                  <div className="list-title">{item.title}</div>
                  <div className="muted">{item.meta}</div>
                </div>
                <div className="list-meta">
                  <div>{item.value}</div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="dashboard-panels">
        <div className="card">
          <div className="card-row">
            <h3>Top quá hạn lâu nhất</h3>
            <span className="text-caption">Ưu tiên xử lý</span>
          </div>
          <div>
            {longestOverdue.map((item) => (
              <div className="list-row" key={item.taxCode}>
                <div>
                  <div className="list-title">{item.name}</div>
                  <div className="muted">{item.taxCode}</div>
                </div>
                <div className="list-meta">
                  <div>{item.value}</div>
                  <span className="muted">{item.meta}</span>
                </div>
              </div>
            ))}
          </div>
        </div>

        <div className="card">
          <div className="card-row">
            <h3>Quá hạn theo phụ trách</h3>
            <span className="text-caption">Theo owner</span>
          </div>
          <div>
            {overdueByOwner.map((item) => (
              <div className="list-row" key={item.name}>
                <div>
                  <div className="list-title">{item.name}</div>
                  <div className="muted">{item.meta}</div>
                </div>
                <div className="list-meta">
                  <div>{item.value}</div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="card">
        <div className="card-row">
          <h3>Hành động nhanh</h3>
          <span className="text-caption">Cuối trang</span>
        </div>
        <div className="action-grid">
          {quickActions.map((action) => (
            <div className="action-card" key={action.label}>
              <div className="action-card__label">{action.label}</div>
              <div className="action-card__value">{action.value}</div>
              <div className="action-card__meta">{action.meta}</div>
            </div>
          ))}
        </div>
      </section>
    </div>
  )
}
