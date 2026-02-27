import type { ReactNode } from 'react'
import type { DashboardKpiDelta, DashboardOverview } from '../../api/dashboard'
import StatCardSkeleton from '../../components/StatCardSkeleton'

type KpiDeltaDirection = 'higher-better' | 'lower-better'

type PeriodTotals = {
  expected: number
  actual: number
  variance: number
  actualRatio: number
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
              Hiệu suất thu hồi theo kỳ
            </h3>
            <p className="muted">So sánh kỳ vọng và thực thu để theo dõi chất lượng thu hồi công nợ.</p>
          </div>
          <StatCardSkeleton count={5} className="stat-grid--secondary" />
        </section>
      </section>
    )
  }

  return (
    <section className="kpi-stack">
      <section className="kpi-stack__group" aria-labelledby="kpi-overview-heading">
        <div className="kpi-stack__header">
          <h3 id="kpi-overview-heading" className="subsection-title">
            Công nợ tổng quan
          </h3>
          <p className="muted">Tập trung vào quy mô dư nợ, quá hạn và trạng thái phân bổ phiếu thu.</p>
        </div>
        <div className="stat-grid stat-grid--primary">
          <div className="stat-card">
            <div className="stat-card__label">Tổng dư công nợ</div>
            <div className="stat-card__value">{formatMoney(overview?.kpis.totalOutstanding ?? 0)}</div>
            <div className="stat-card__meta">Gồm hóa đơn + trả hộ</div>
            {renderMomBadge(overview?.kpiMoM?.totalOutstanding, 'lower-better')}
          </div>
          <div className="stat-card">
            <div className="stat-card__label">Dư hóa đơn</div>
            <div className="stat-card__value">{formatMoney(overview?.kpis.outstandingInvoice ?? 0)}</div>
            <div className="stat-card__meta">Chưa phân bổ hết</div>
            {renderMomBadge(overview?.kpiMoM?.outstandingInvoice, 'lower-better')}
          </div>
          <div className="stat-card">
            <div className="stat-card__label">Dư trả hộ</div>
            <div className="stat-card__value">{formatMoney(overview?.kpis.outstandingAdvance ?? 0)}</div>
            <div className="stat-card__meta">Khoản trả hộ còn lại</div>
            {renderMomBadge(overview?.kpiMoM?.outstandingAdvance, 'lower-better')}
          </div>
          <div className="stat-card">
            <div className="stat-card__label">Đã thu chưa phân bổ</div>
            <div className="stat-card__value">{formatMoney(overview?.kpis.unallocatedReceiptsAmount ?? 0)}</div>
            <div className="stat-card__meta">{overview?.kpis.unallocatedReceiptsCount ?? 0} phiếu thu treo</div>
            {renderMomBadge(overview?.kpiMoM?.unallocatedReceiptsAmount, 'lower-better')}
          </div>
          <div className={`stat-card${(overview?.kpis.overdueTotal ?? 0) > 0 ? ' stat-card--danger' : ''}`}>
            <div className="stat-card__label">Quá hạn</div>
            <div className="stat-card__value">{formatMoney(overview?.kpis.overdueTotal ?? 0)}</div>
            <div className="stat-card__meta">{overview?.kpis.overdueCustomers ?? 0} khách hàng đang quá hạn</div>
            {renderMomBadge(overview?.kpiMoM?.overdueTotal, 'lower-better')}
          </div>
        </div>
      </section>
      <section className="kpi-stack__group" aria-labelledby="kpi-performance-heading">
        <div className="kpi-stack__header">
          <h3 id="kpi-performance-heading" className="subsection-title">
            Hiệu suất thu hồi theo kỳ
          </h3>
          <p className="muted">So sánh kỳ vọng và thực thu để theo dõi chất lượng thu hồi công nợ.</p>
        </div>
        <div className="stat-grid stat-grid--secondary">
          <div className="stat-card stat-card--secondary">
            <div className="stat-card__label">Thu thực tế trong kỳ</div>
            <div className="stat-card__value">{formatMoney(periodTotals.actual)}</div>
            <div className="stat-card__meta">Theo kỳ đã chọn</div>
          </div>
          <div className="stat-card stat-card--secondary">
            <div className="stat-card__label">KH trả đúng hạn</div>
            <div className="stat-card__value">{overview?.kpis.onTimeCustomers ?? 0}</div>
            <div className="stat-card__meta">≥95% khoản đến hạn trong kỳ</div>
            {renderMomBadge(overview?.kpiMoM?.onTimeCustomers, 'higher-better')}
          </div>
          <div className="stat-card stat-card--secondary">
            <div className="stat-card__label">Thu kỳ vọng</div>
            <div className="stat-card__value">{formatMoney(periodTotals.expected)}</div>
            <div className="stat-card__meta">Invoice + trả hộ</div>
          </div>
          <div className="stat-card stat-card--secondary">
            <div className="stat-card__label">Chênh lệch (Actual - Expected)</div>
            <div className="stat-card__value">{formatMoney(periodTotals.variance)}</div>
            <div className="stat-card__meta">{periodTotals.variance >= 0 ? 'Thu vượt kỳ vọng' : 'Thu thấp hơn kỳ vọng'}</div>
          </div>
          <div className="stat-card stat-card--secondary">
            <div className="stat-card__label">% Actual/Expected</div>
            <div className="stat-card__value">{periodTotals.actualRatio}%</div>
            <div className="stat-card__meta">Hiệu suất thu hồi trong kỳ</div>
          </div>
        </div>
      </section>
    </section>
  )
}
