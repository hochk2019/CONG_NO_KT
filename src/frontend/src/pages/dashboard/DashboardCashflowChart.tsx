import { useState, type MouseEvent } from 'react'
import EmptyState from '../../components/EmptyState'
import Skeleton from '../../components/Skeleton'

export type UnitScale = 'million' | 'billion'
export type TrendGranularity = 'week' | 'month'

export type CashflowPoint = {
  period: string
  label: string
  fullLabel: string
  expected: number
  actual: number
  variance: number
}

export type ForecastPoint = {
  period: string
  label: string
  expected: number
  actual: number
  variance: number
}

type DashboardCashflowChartProps = {
  loading: boolean
  error: string | null
  points: CashflowPoint[]
  forecastPoints: ForecastPoint[]
  maxValue: number
  hasMeaningfulData: boolean
  labelStep: number
  trendGranularity: TrendGranularity
  unit: UnitScale
  onChangeTrendGranularity: (value: TrendGranularity) => void
  onChangeUnit: (value: UnitScale) => void
  formatMoney: (value: number) => string
  formatUnitValue: (value: number, unit: UnitScale) => string
}

type TooltipState = {
  x: number
  y: number
  content: string
}

export default function DashboardCashflowChart({
  loading,
  error,
  points,
  forecastPoints,
  maxValue,
  hasMeaningfulData,
  labelStep,
  trendGranularity,
  unit,
  onChangeTrendGranularity,
  onChangeUnit,
  formatMoney,
  formatUnitValue,
}: DashboardCashflowChartProps) {
  const [tooltip, setTooltip] = useState<TooltipState | null>(null)

  const handleShowTooltip = (event: MouseEvent<HTMLDivElement>, content: string) => {
    setTooltip({
      x: event.clientX,
      y: event.clientY,
      content,
    })
  }

  const handleMoveTooltip = (event: MouseEvent<HTMLDivElement>) => {
    setTooltip((previous) =>
      previous
        ? {
            ...previous,
            x: event.clientX,
            y: event.clientY,
          }
        : previous,
    )
  }

  return (
    <section className="card cashflow-card">
      <div className="cashflow-header">
        <div>
          <h3>Dòng tiền Expected vs Actual</h3>
          <p className="muted">Theo dõi kỳ vọng thu hồi, thực thu và chênh lệch theo từng kỳ.</p>
        </div>
        <div className="chart-controls chart-controls--cashflow">
          <div className="unit-toggle chart-controls__group" role="group" aria-label="Chế độ kỳ biểu đồ">
            <button
              className={`unit-toggle__btn ${trendGranularity === 'week' ? 'unit-toggle__btn--active' : ''}`}
              type="button"
              onClick={() => onChangeTrendGranularity('week')}
            >
              Theo tuần
            </button>
            <button
              className={`unit-toggle__btn ${trendGranularity === 'month' ? 'unit-toggle__btn--active' : ''}`}
              type="button"
              onClick={() => onChangeTrendGranularity('month')}
            >
              Theo tháng
            </button>
          </div>
          <div className="unit-toggle chart-controls__group" role="group" aria-label="Đổi đơn vị biểu đồ">
            <button
              className={`unit-toggle__btn ${unit === 'million' ? 'unit-toggle__btn--active' : ''}`}
              type="button"
              onClick={() => onChangeUnit('million')}
            >
              Triệu
            </button>
            <button
              className={`unit-toggle__btn ${unit === 'billion' ? 'unit-toggle__btn--active' : ''}`}
              type="button"
              onClick={() => onChangeUnit('billion')}
            >
              Tỷ
            </button>
          </div>
        </div>
      </div>
      <div className="chart-legend chart-legend--cashflow">
        <span style={{ color: 'var(--color-accent)' }}>■ Expected (Kỳ vọng)</span>
        <span style={{ color: 'var(--color-success)' }}>■ Actual (Thực thu)</span>
        <span style={{ color: 'var(--color-warning)' }}>■ Variance</span>
        <span className="chart-legend__unit">Đơn vị: {unit === 'billion' ? 'tỷ' : 'triệu'}</span>
      </div>
      {loading ? (
        <div className="cashflow-skeleton" role="status" aria-label="Đang tải biểu đồ dòng tiền">
          <div className="cashflow-skeleton__bars">
            {Array.from({ length: 9 }).map((_, index) => (
              <div className="cashflow-skeleton__group" key={index}>
                <Skeleton width="14px" height={`${80 + (index % 4) * 20}px`} borderRadius="8px" />
                <Skeleton width="14px" height={`${70 + (index % 3) * 16}px`} borderRadius="8px" />
              </div>
            ))}
          </div>
        </div>
      ) : error ? (
        <div className="alert alert--error">{error}</div>
      ) : points.length === 0 ? (
        <EmptyState
          title="Chưa có dữ liệu dòng tiền"
          description="Dữ liệu sẽ hiển thị sau khi có phát sinh theo kỳ đã chọn."
          icon="📉"
          compact
        />
      ) : !hasMeaningfulData ? (
        <EmptyState
          title="Dữ liệu kỳ này chưa đủ để so sánh"
          description="Hãy đổi kỳ hiển thị hoặc chờ thêm dữ liệu mới."
          icon="🧭"
          compact
        />
      ) : (
        <>
          <div className="cashflow-chart">
            {points.map((point, index) => {
              const expectedRatio = maxValue ? point.expected / maxValue : 0
              const actualRatio = maxValue ? point.actual / maxValue : 0
              const showLabel = index % labelStep === 0 || index === points.length - 1
              const expectedHeight = point.expected > 0 ? Math.max(4, expectedRatio * 100) : 0
              const actualHeight = point.actual > 0 ? Math.max(4, actualRatio * 100) : 0
              const expectedTooltip = `${point.fullLabel}\nExpected: ${formatUnitValue(point.expected, unit)}`
              const actualTooltip = `${point.fullLabel}\nActual: ${formatUnitValue(point.actual, unit)}`

              return (
                <div className="cashflow-chart__group" key={point.period}>
                  <div className="cashflow-chart__bars">
                    <div
                      className="cashflow-chart__bar cashflow-chart__bar--expected"
                      style={{ height: `${expectedHeight}%` }}
                      title={expectedTooltip}
                      onMouseEnter={(event) => handleShowTooltip(event, expectedTooltip)}
                      onMouseMove={handleMoveTooltip}
                      onMouseLeave={() => setTooltip(null)}
                    />
                    <div
                      className="cashflow-chart__bar cashflow-chart__bar--actual"
                      style={{ height: `${actualHeight}%` }}
                      title={actualTooltip}
                      onMouseEnter={(event) => handleShowTooltip(event, actualTooltip)}
                      onMouseMove={handleMoveTooltip}
                      onMouseLeave={() => setTooltip(null)}
                    />
                  </div>
                  <div
                    className={`cashflow-chart__variance ${
                      point.variance >= 0
                        ? 'cashflow-chart__variance--positive'
                        : 'cashflow-chart__variance--negative'
                    }`}
                  >
                    {point.variance >= 0 ? '+' : ''}
                    {formatUnitValue(point.variance, unit)}
                  </div>
                  <div className="cashflow-chart__label">{showLabel ? point.label : ''}</div>
                </div>
              )
            })}
          </div>
          {tooltip ? (
            <div className="chart-tooltip" style={{ left: tooltip.x, top: tooltip.y }} role="status">
              {tooltip.content}
            </div>
          ) : null}
          {forecastPoints.length > 0 ? (
            <div className="cashflow-forecast">
              <h4 className="subsection-title">
                Dự báo {trendGranularity === 'week' ? '4 tuần' : '3 tháng'} tới
              </h4>
              <div className="forecast-grid">
                {forecastPoints.map((point) => (
                  <article className="forecast-card" key={point.period}>
                    <div className="forecast-card__period">{point.label}</div>
                    <div className="forecast-card__row">
                      <span>Kỳ vọng</span>
                      <strong>{formatMoney(point.expected)}</strong>
                    </div>
                    <div className="forecast-card__row">
                      <span>Thực thu dự kiến</span>
                      <strong>{formatMoney(point.actual)}</strong>
                    </div>
                    <div
                      className={`forecast-card__row ${
                        point.variance >= 0
                          ? 'forecast-card__row--positive'
                          : 'forecast-card__row--negative'
                      }`}
                    >
                      <span>Variance</span>
                      <strong>{formatMoney(point.variance)}</strong>
                    </div>
                  </article>
                ))}
              </div>
            </div>
          ) : null}
        </>
      )}
    </section>
  )
}
