import type { ReactNode } from 'react'
import type { DashboardKpiDelta, DashboardOverview } from '../../api/dashboard'
import StatCardSkeleton from '../../components/StatCardSkeleton'

type KpiDeltaDirection = 'higher-better' | 'lower-better'

type PeriodTotals = {
  expected: number
  expectedNext: number
  actual: number
  variance: number
  actualRatio: number
  onTimeCustomers: number
}

type DashboardKpiSectionProps = {
  overview: DashboardOverview | null
  periodTotals: PeriodTotals
  isLoading: boolean
  renderMomBadge: (
    delta: DashboardKpiDelta | undefined,
    direction?: KpiDeltaDirection,
  ) => ReactNode
  formatMoney: (value: number) => string
}

export default function DashboardKpiSection({
  overview,
  periodTotals,
  isLoading,
  renderMomBadge,
  formatMoney,
}: DashboardKpiSectionProps) {
  const kpis = overview?.kpis ?? {
    totalOutstanding: 0,
    outstandingInvoice: 0,
    outstandingAdvance: 0,
    unallocatedReceiptsAmount: 0,
    unallocatedReceiptsCount: 0,
    overdueTotal: 0,
    overdueCustomers: 0,
  }
  const actual = periodTotals.actual
  const expected = periodTotals.expected
  const openDebt = Math.max(0, expected - actual)
  const overdue = kpis.overdueTotal || 0
  const dueToday = Math.max(0, openDebt - overdue)

  const targetTooltip = `Tổng khối lượng nợ cần xử lý:\n• Dư nợ tồn đọng hiện tại: ${formatMoney(openDebt)}\n   - Đến hạn hôm nay: ${formatMoney(dueToday)}\n   - Đang quá hạn: ${formatMoney(overdue)}\n• Đã thu thành công 30 ngày qua: ${formatMoney(actual)}`

  if (isLoading) {
    return (
      <section className="kpi-stack" aria-label="Đang tải chỉ số KPI" role="status">
        <section className="kpi-stack__group" aria-labelledby="kpi-overview-heading">
          <div className="kpi-stack__header">
            <h3 id="kpi-overview-heading" className="subsection-title">
              Công nợ tổng quan
            </h3>
            <p className="muted">Tập trung vào quy mô dư nợ, quá hạn và trạng thái phân bổ phiếu thu.</p>
          </div>
          <StatCardSkeleton count={5} className="stat-grid--primary" />
        </section>
        <section className="kpi-stack__group" aria-labelledby="kpi-performance-heading">
          <div className="kpi-stack__header">
            <h3 id="kpi-performance-heading" className="subsection-title">
              Hiệu suất / Kỳ vọng 30 ngày
            </h3>
            <p className="muted">Đánh giá hiệu suất thu hồi và dự báo trong 30 ngày.</p>
          </div>
          <StatCardSkeleton count={5} className="stat-grid--secondary" />
        </section>
      </section>
    )
  }

  return (
    <section className="kpi-stack" aria-label="Chỉ số KPI chính">
      <section className="kpi-stack__group" aria-labelledby="kpi-overview-heading-loaded">
        <div className="kpi-stack__header">
          <h3 id="kpi-overview-heading-loaded" className="subsection-title">
            Công nợ tổng quan
          </h3>
          <p className="muted">Tập trung vào quy mô dư nợ, quá hạn và trạng thái phân bổ phiếu thu.</p>
        </div>
        <div className="stat-grid stat-grid--primary">
          <div className="stat-card">
            <div className="stat-card__label">Tổng dư công nợ</div>
            <div className="stat-card__value">{formatMoney(kpis.totalOutstanding)}</div>
            <div className="stat-card__meta">Gồm hóa đơn + trả hộ</div>
            {renderMomBadge(overview?.kpiMoM?.totalOutstanding, 'lower-better')}
          </div>
          <div className="stat-card">
            <div className="stat-card__label">Dư hóa đơn</div>
            <div className="stat-card__value">{formatMoney(kpis.outstandingInvoice)}</div>
            <div className="stat-card__meta">Chưa phân bổ hết</div>
            {renderMomBadge(overview?.kpiMoM?.outstandingInvoice, 'lower-better')}
          </div>
          <div className="stat-card">
            <div className="stat-card__label">Dư trả hộ</div>
            <div className="stat-card__value">{formatMoney(kpis.outstandingAdvance)}</div>
            <div className="stat-card__meta">Khoản trả hộ còn lại</div>
            {renderMomBadge(overview?.kpiMoM?.outstandingAdvance, 'lower-better')}
          </div>
          <div className="stat-card">
            <div className="stat-card__label">Đã thu chưa phân bổ</div>
            <div className="stat-card__value">{formatMoney(kpis.unallocatedReceiptsAmount)}</div>
            <div className="stat-card__meta">
              {kpis.unallocatedReceiptsCount} phiếu thu treo
            </div>
            {renderMomBadge(overview?.kpiMoM?.unallocatedReceiptsAmount, 'lower-better')}
          </div>
          <div className="stat-card stat-card--danger">
            <div className="stat-card__label">Quá hạn</div>
            <div className="stat-card__value">{formatMoney(kpis.overdueTotal)}</div>
            <div className="stat-card__meta">
              {kpis.overdueCustomers} khách hàng đang quá hạn
            </div>
            {renderMomBadge(overview?.kpiMoM?.overdueTotal, 'lower-better')}
          </div>
        </div>
      </section>
      <section className="kpi-stack__group" aria-labelledby="kpi-performance-heading-loaded">
        <div className="kpi-stack__header">
          <h3 id="kpi-performance-heading-loaded" className="subsection-title">
            Hiệu suất / Kỳ vọng 30 ngày
          </h3>
          <p className="muted">Đánh giá hiệu suất thu hồi và dự báo trong 30 ngày.</p>
        </div>
        <div className="stat-grid stat-grid--secondary">
          <div className="stat-card stat-card--secondary" title={targetTooltip}>
            <div className="stat-card__label">Mục tiêu thu 30 ngày qua</div>
            <div className="stat-card__value">{formatMoney(periodTotals.expected)}</div>
            <div className="stat-card__meta">Rê chuột để xem chi tiết</div>
          </div>
          <div className="stat-card stat-card--secondary">
            <div className="stat-card__label">Đã thu 30 ngày qua</div>
            <div className="stat-card__value">{formatMoney(periodTotals.actual)}</div>
            <div className="stat-card__meta">Phiếu thu đã duyệt 30 ngày qua</div>
          </div>
          <div 
            className="stat-card stat-card--secondary" 
            title={`Hiệu suất thu hồi 30 ngày qua:\n• Mục tiêu cần thu: ${formatMoney(periodTotals.expected)}\n• Thực tế đã thu: ${formatMoney(periodTotals.actual)}\n--------------------------\nThực thu so với mục tiêu: ${periodTotals.variance >= 0 ? '+' : ''}${formatMoney(periodTotals.variance)}`}
          >
            <div className="stat-card__label">Hiệu suất hoàn thành mục tiêu</div>
            <div className="stat-card__value">
              {periodTotals.actualRatio}% 
              <span className={`kpi-variance--${periodTotals.variance >= 0 ? 'positive' : 'negative'}`} style={{ fontSize: '0.8em', marginLeft: '8px', fontWeight: 'normal' }}>
                ({periodTotals.variance >= 0 ? '+' : ''}{formatMoney(periodTotals.variance)})
              </span>
            </div>
            <div className="stat-card__meta">Rê chuột để xem chi tiết</div>
          </div>
          <div className="stat-card stat-card--secondary">
            <div className="stat-card__label">KH trả đúng hạn 30 ngày</div>
            <div className="stat-card__value">{periodTotals.onTimeCustomers}</div>
            <div className="stat-card__meta">≥95% khoản đến hạn trong 30 ngày qua</div>
            {renderMomBadge(overview?.kpiMoM?.onTimeCustomers, 'higher-better')}
          </div>
          <div className="stat-card stat-card--secondary">
            <div className="stat-card__label">Mục tiêu cần thu 30 ngày tới</div>
            <div className="stat-card__value">{formatMoney(periodTotals.expectedNext)}</div>
            <div className="stat-card__meta">Dư nợ sẽ đến hạn trong 30 ngày tới</div>
          </div>
        </div>
      </section>
    </section>
  )
}
