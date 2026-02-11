import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { ApiError } from '../api/client'
import {
  fetchDashboardOverview,
  fetchDashboardOverdueGroups,
  type DashboardOverview,
  type DashboardOverdueGroupItem,
  type DashboardTopItem,
} from '../api/dashboard'
import { useAuth } from '../context/AuthStore'
import { formatDateTime, formatMoney } from '../utils/format'
import { toDateInput } from './reports/reportUtils'

const rangeOptions = [
  { value: '1m', label: 'Tháng này', months: 1 },
  { value: '3m', label: '3 tháng', months: 3 },
  { value: '6m', label: '6 tháng', months: 6 },
  { value: '12m', label: '12 tháng', months: 12 },
  { value: 'ytd', label: 'Từ đầu năm', months: null },
]

const buildRangeParams = (range: string) => {
  const today = new Date()
  if (range === 'ytd') {
    const from = new Date(today.getFullYear(), 0, 1)
    return { from: toDateInput(from), to: toDateInput(today) }
  }

  const item = rangeOptions.find((option) => option.value === range)
  if (!item || !item.months) {
    return { months: 6 }
  }

  return { months: item.months }
}

type UnitScale = 'million' | 'billion'
type TrendGranularity = 'week' | 'month'

const resolveRangeBounds = (range: string) => {
  const today = new Date()
  if (range === 'ytd') {
    return { from: new Date(today.getFullYear(), 0, 1), to: today }
  }

  const item = rangeOptions.find((option) => option.value === range)
  const months = item?.months ?? 6
  return {
    from: new Date(today.getFullYear(), today.getMonth() - (months - 1), 1),
    to: today,
  }
}

const startOfWeek = (date: Date) => {
  const day = date.getDay()
  const delta = day === 0 ? 6 : day - 1
  const start = new Date(date)
  start.setDate(date.getDate() - delta)
  start.setHours(0, 0, 0, 0)
  return start
}

const addDays = (date: Date, days: number) => {
  const next = new Date(date)
  next.setDate(next.getDate() + days)
  return next
}

const addMonths = (date: Date, months: number) => new Date(date.getFullYear(), date.getMonth() + months, 1)

const endOfMonth = (date: Date) => new Date(date.getFullYear(), date.getMonth() + 1, 0)

const clampTrendPeriods = (granularity: TrendGranularity, value: number) => {
  if (granularity === 'week') {
    return Math.min(52, Math.max(2, value))
  }
  return Math.min(24, Math.max(1, value))
}

const getTrendPeriodsFromRange = (range: string, granularity: TrendGranularity) => {
  const { from, to } = resolveRangeBounds(range)
  if (granularity === 'month') {
    const months =
      (to.getFullYear() - from.getFullYear()) * 12 + (to.getMonth() - from.getMonth()) + 1
    return clampTrendPeriods('month', months)
  }

  const fromWeek = startOfWeek(from)
  const toWeek = startOfWeek(to)
  const diffDays = Math.round((toWeek.getTime() - fromWeek.getTime()) / 86400000)
  const weeks = Math.floor(diffDays / 7) + 1
  return clampTrendPeriods('week', weeks)
}

const parseDateOnly = (value: string) => {
  const [year, month, day] = value.split('-').map(Number)
  return new Date(year, month - 1, day)
}

const pad2 = (value: number) => String(value).padStart(2, '0')

const formatWeekRangeLabel = (start: Date, end: Date) => {
  const sameMonth = start.getMonth() === end.getMonth() && start.getFullYear() === end.getFullYear()
  if (sameMonth) {
    return `${pad2(start.getDate())}–${pad2(end.getDate())}/${pad2(end.getMonth() + 1)}`
  }
  return `${pad2(start.getDate())}/${pad2(start.getMonth() + 1)}–${pad2(end.getDate())}/${pad2(end.getMonth() + 1)}`
}

const formatWeekRangeFull = (start: Date, end: Date) =>
  `${pad2(start.getDate())}/${pad2(start.getMonth() + 1)}/${start.getFullYear()}–${pad2(end.getDate())}/${pad2(end.getMonth() + 1)}/${end.getFullYear()}`

const formatMonthLabel = (date: Date) => `${pad2(date.getMonth() + 1)}/${date.getFullYear()}`

const formatUnitValue = (value: number, unit: UnitScale) => {
  const divider = unit === 'billion' ? 1_000_000_000 : 1_000_000
  const scaled = value / divider
  const formatted = scaled.toLocaleString('vi-VN', {
    maximumFractionDigits: scaled >= 10 ? 1 : 2,
  })
  return `${formatted} ${unit === 'billion' ? 'tỷ' : 'triệu'}`
}

const cashflowStorageKey = {
  granularity: 'dashboard.cashflow.granularity',
}

const getStoredGranularity = (): TrendGranularity => {
  if (typeof window === 'undefined') return 'week'
  const stored = window.localStorage.getItem(cashflowStorageKey.granularity)
  return stored === 'month' || stored === 'week' ? stored : 'week'
}


const renderTopList = (
  rows: DashboardTopItem[],
  emptyMessage: string,
  showDays: boolean,
) => {
  if (rows.length === 0) {
    return <div className="empty-state">{emptyMessage}</div>
  }

  return (
    <div>
      {rows.map((row) => (
        <div className="list-row" key={row.customerTaxCode}>
          <div>
            <div className="list-title">{row.customerName}</div>
            <div className="muted">{row.customerTaxCode}</div>
          </div>
          <div className="list-meta">
            <div>{formatMoney(row.amount)}</div>
            {showDays && row.daysPastDue !== null && row.daysPastDue !== undefined && (
              <span className="muted">{row.daysPastDue} ngày</span>
            )}
          </div>
        </div>
      ))}
    </div>
  )
}

const renderOverdueGroupList = (
  rows: DashboardOverdueGroupItem[],
  emptyMessage: string,
) => {
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
              <div className="muted">{row.overdueCustomers} khách hàng quá hạn</div>
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

export default function DashboardPage() {
  const { state } = useAuth()
  const token = state.accessToken ?? ''
  const canViewImports = state.roles.includes('Admin') || state.roles.includes('Supervisor')
  const canViewLocks = state.roles.includes('Admin') || state.roles.includes('Supervisor')

  const [range, setRange] = useState('6m')
  const [topCount, setTopCount] = useState(5)
  const [unit, setUnit] = useState<UnitScale>('billion')
  const initialGranularity = getStoredGranularity()
  const [trendGranularity, setTrendGranularity] = useState<TrendGranularity>(initialGranularity)
  const [overview, setOverview] = useState<DashboardOverview | null>(null)
  const [cashflowTrend, setCashflowTrend] = useState<DashboardOverview['trend']>([])
  const [cashflowRange, setCashflowRange] = useState<{ from: string; to: string } | null>(null)
  const [cashflowLoading, setCashflowLoading] = useState(false)
  const [cashflowError, setCashflowError] = useState<string | null>(null)
  const [overdueGroups, setOverdueGroups] = useState<DashboardOverdueGroupItem[]>([])
  const [overdueGroupsLoading, setOverdueGroupsLoading] = useState(false)
  const [overdueGroupsError, setOverdueGroupsError] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null)
  const trendPeriods = useMemo(
    () => getTrendPeriodsFromRange(range, trendGranularity),
    [range, trendGranularity],
  )

  const handleTrendGranularityChange = (value: TrendGranularity) => {
    setTrendGranularity(value)
  }

  useEffect(() => {
    if (typeof window === 'undefined') return
    window.localStorage.setItem(cashflowStorageKey.granularity, trendGranularity)
  }, [trendGranularity])

  useEffect(() => {
    if (!token) return
    let isActive = true

    const loadOverview = async () => {
      setError(null)
      try {
        const rangeParams = buildRangeParams(range)
        const result = await fetchDashboardOverview({
          token,
          from: rangeParams.from,
          to: rangeParams.to,
          months: rangeParams.months,
          top: topCount,
        })
        if (!isActive) return
        setOverview(result)
        setLastUpdated(result.lastUpdatedAt ? new Date(result.lastUpdatedAt) : new Date())

        setOverdueGroupsLoading(true)
        setOverdueGroupsError(null)
        try {
          const asOf = rangeParams.to ?? toDateInput(new Date())
          const rows = await fetchDashboardOverdueGroups({
            token,
            asOf,
            top: topCount,
            groupBy: 'owner',
          })
          if (!isActive) return
          setOverdueGroups(rows)
        } catch (err) {
          if (!isActive) return
          if (err instanceof ApiError) {
            setOverdueGroupsError(
              err.status === 500
                ? 'Không tải được thống kê quá hạn theo phụ trách.'
                : err.message,
            )
          } else {
            setOverdueGroupsError('Không tải được thống kê quá hạn theo phụ trách.')
          }
        } finally {
          if (isActive) {
            setOverdueGroupsLoading(false)
          }
        }
      } catch (err) {
        if (!isActive) return
        if (err instanceof ApiError) {
          setError(err.status === 500 ? 'Không tải được dữ liệu tổng quan.' : err.message)
        } else {
          setError('Không tải được dữ liệu tổng quan.')
        }
      }
    }

    loadOverview()
    return () => {
      isActive = false
    }
  }, [token, range, topCount])

  useEffect(() => {
    if (!token) return
    let isActive = true

    const loadCashflow = async () => {
      setCashflowLoading(true)
      setCashflowError(null)
      try {
        const rangeParams = buildRangeParams(range)
        const result = await fetchDashboardOverview({
          token,
          from: rangeParams.from,
          to: rangeParams.to,
          months: rangeParams.months,
          top: topCount,
          trendGranularity,
          trendPeriods,
        })
        if (!isActive) return
        setCashflowTrend(result.trend)
        setCashflowRange({ from: result.trendFrom, to: result.trendTo })
      } catch (err) {
        if (!isActive) return
        setCashflowError(err instanceof ApiError ? err.message : 'Không thể tải biểu đồ thu')
        setCashflowRange(null)
      } finally {
        if (isActive) {
          setCashflowLoading(false)
        }
      }
    }

    loadCashflow()
    return () => {
      isActive = false
    }
  }, [token, range, topCount, trendGranularity, trendPeriods])

  const cashflowPoints = useMemo(() => {
    const start = cashflowRange?.from ? parseDateOnly(cashflowRange.from) : null
    return cashflowTrend.map((point, index) => {
      const periodStart =
        start && trendGranularity === 'week'
          ? addDays(start, index * 7)
          : start
          ? addMonths(start, index)
          : null
      const periodEnd = periodStart
        ? trendGranularity === 'week'
          ? addDays(periodStart, 6)
          : endOfMonth(periodStart)
        : null
      const label =
        periodStart && periodEnd
          ? trendGranularity === 'week'
            ? formatWeekRangeLabel(periodStart, periodEnd)
            : formatMonthLabel(periodStart)
          : point.period
      const fullLabel =
        periodStart && periodEnd
          ? trendGranularity === 'week'
            ? formatWeekRangeFull(periodStart, periodEnd)
            : formatMonthLabel(periodStart)
          : point.period
      return {
        period: point.period,
        label,
        fullLabel,
        invoiced: point.invoicedTotal,
        advanced: point.advancedTotal,
        revenueTotal: point.invoicedTotal + point.advancedTotal,
        receipted: point.receiptedTotal,
      }
    })
  }, [cashflowRange, cashflowTrend, trendGranularity])

  const maxCashflowValue = useMemo(() => {
    if (cashflowPoints.length === 0) {
      return 0
    }
    return Math.max(
      ...cashflowPoints.flatMap((point) => [point.revenueTotal, point.receipted]),
    )
  }, [cashflowPoints])

  const cashflowLabelStep = useMemo(() => {
    if (cashflowPoints.length > 16) return 3
    if (cashflowPoints.length > 8) return 2
    return 1
  }, [cashflowPoints.length])

  const periodTotals = useMemo(() => {
    const totals = overview?.trend.reduce(
      (acc, point) => ({
        invoiced: acc.invoiced + point.invoicedTotal,
        advanced: acc.advanced + point.advancedTotal,
        receipted: acc.receipted + point.receiptedTotal,
      }),
      { invoiced: 0, advanced: 0, receipted: 0 },
    )

    if (!totals) {
      return { invoiced: 0, advanced: 0, receipted: 0, ratio: 0 }
    }

    const denominator = totals.invoiced + totals.advanced
    const ratio = denominator > 0 ? Math.round((totals.receipted / denominator) * 1000) / 10 : 0
    return { ...totals, ratio }
  }, [overview])

  const allocationSummary = useMemo(() => {
    const rows = overview?.allocationStatuses ?? []
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
  }, [overview])

  const quickActions = [
    {
      id: 'imports',
      title: 'Nhập liệu',
      description: 'Tải template, nhập thủ công',
      to: '/imports',
      visible: canViewImports || state.roles.includes('Accountant'),
    },
    {
      id: 'customers',
      title: 'Khách hàng',
      description: 'Danh sách khách hàng, giao dịch',
      to: '/customers',
      visible: true,
    },
    {
      id: 'receipts',
      title: 'Thu tiền',
      description: 'Nhập phiếu thu, phân bổ',
      to: '/receipts',
      visible: true,
    },
    {
      id: 'reports',
      title: 'Báo cáo',
      description: 'Tuổi nợ, tổng hợp',
      to: '/reports',
      visible: true,
    },
    {
      id: 'risk',
      title: 'Cảnh báo rủi ro',
      description: 'Theo dõi rủi ro công nợ',
      to: '/risk',
      visible: true,
    },
    {
      id: 'locks',
      title: 'Khóa kỳ',
      description: 'Quản lý kỳ kế toán',
      to: '/admin/period-locks',
      visible: canViewLocks,
    },
  ]

  const visibleActions = quickActions.filter((item) => item.visible)

  return (
    <div className="page-stack dashboard-page">
      <header className="page-header">
        <div>
          <h2>Tổng quan công nợ</h2>
          <p className="muted">
            Theo dõi tổng quan công nợ, dòng tiền thu và các nhóm cần xử lý.
          </p>
          {lastUpdated && (
            <div className="meta-row text-caption">
              <span>Cập nhật lúc {formatDateTime(lastUpdated)}</span>
            </div>
          )}
        </div>
        <div className="dashboard-filters">
          <label className="field">
            <span>Kỳ hiển thị</span>
            <select value={range} onChange={(event) => setRange(event.target.value)}>
              {rangeOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>
        </div>
      </header>

      {error && <div className="alert alert--error" role="alert" aria-live="assertive">{error}</div>}

      <section className="kpi-stack">
        <div className="stat-grid stat-grid--primary">
          <div className="stat-card">
            <div className="stat-card__label">Tổng dư công nợ</div>
            <div className="stat-card__value">{formatMoney(overview?.kpis.totalOutstanding ?? 0)}</div>
            <div className="stat-card__meta">Gồm hóa đơn + trả hộ</div>
          </div>
          <div className="stat-card">
            <div className="stat-card__label">Dư hóa đơn</div>
            <div className="stat-card__value">{formatMoney(overview?.kpis.outstandingInvoice ?? 0)}</div>
            <div className="stat-card__meta">Chưa phân bổ hết</div>
          </div>
          <div className="stat-card">
            <div className="stat-card__label">Dư trả hộ</div>
            <div className="stat-card__value">{formatMoney(overview?.kpis.outstandingAdvance ?? 0)}</div>
            <div className="stat-card__meta">Khoản trả hộ còn lại</div>
          </div>
          <div className="stat-card">
            <div className="stat-card__label">Đã thu chưa phân bổ</div>
            <div className="stat-card__value">
              {formatMoney(overview?.kpis.unallocatedReceiptsAmount ?? 0)}
            </div>
            <div className="stat-card__meta">
              {overview?.kpis.unallocatedReceiptsCount ?? 0} phiếu thu treo
            </div>
          </div>
          <div className={`stat-card${(overview?.kpis.overdueTotal ?? 0) > 0 ? ' stat-card--danger' : ''}`}>
            <div className="stat-card__label">Quá hạn</div>
            <div className="stat-card__value">{formatMoney(overview?.kpis.overdueTotal ?? 0)}</div>
            <div className="stat-card__meta">
              {overview?.kpis.overdueCustomers ?? 0} khách hàng đang quá hạn
            </div>
          </div>
        </div>
        <div className="stat-grid stat-grid--secondary">
          <div className="stat-card stat-card--secondary">
            <div className="stat-card__label">Thu trong kỳ</div>
            <div className="stat-card__value">{formatMoney(periodTotals.receipted)}</div>
            <div className="stat-card__meta">Theo kỳ đã chọn</div>
          </div>
          <div className="stat-card stat-card--secondary">
            <div className="stat-card__label">KH trả đúng hạn</div>
            <div className="stat-card__value">{overview?.kpis.onTimeCustomers ?? 0}</div>
            <div className="stat-card__meta">≥95% khoản đến hạn trong kỳ</div>
          </div>
          <div className="stat-card stat-card--secondary">
            <div className="stat-card__label">Phát sinh hóa đơn</div>
            <div className="stat-card__value">{formatMoney(periodTotals.invoiced)}</div>
            <div className="stat-card__meta">Hóa đơn phát sinh</div>
          </div>
          <div className="stat-card stat-card--secondary">
            <div className="stat-card__label">Phát sinh trả hộ</div>
            <div className="stat-card__value">{formatMoney(periodTotals.advanced)}</div>
            <div className="stat-card__meta">Khoản trả hộ mới</div>
          </div>
          <div className="stat-card stat-card--secondary">
            <div className="stat-card__label">% Thu/Phải thu</div>
            <div className="stat-card__value">{periodTotals.ratio}%</div>
            <div className="stat-card__meta">Tỷ lệ thu hồi</div>
          </div>
        </div>
      </section>

      <section className="dashboard-charts">
        <section className="card cashflow-card">
          <div className="cashflow-header">
            <div>
              <h3>Luồng tiền thu theo kỳ</h3>
              <p className="muted">So sánh doanh thu, trả hộ và tiền thu đã duyệt.</p>
            </div>
            <div className="chart-controls chart-controls--cashflow">
              <div className="unit-toggle chart-controls__group" role="group" aria-label="Chế độ kỳ biểu đồ">
                <button
                  className={`unit-toggle__btn ${trendGranularity === 'week' ? 'unit-toggle__btn--active' : ''}`}
                  type="button"
                  onClick={() => handleTrendGranularityChange('week')}
                >
                  Theo tuần
                </button>
                <button
                  className={`unit-toggle__btn ${trendGranularity === 'month' ? 'unit-toggle__btn--active' : ''}`}
                  type="button"
                  onClick={() => handleTrendGranularityChange('month')}
                >
                  Theo tháng
                </button>
              </div>
              <div className="unit-toggle chart-controls__group" role="group" aria-label="Đổi đơn vị biểu đồ">
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
          </div>
          <div className="chart-legend chart-legend--cashflow">
            <span style={{ color: 'var(--color-accent)' }}>■ Doanh thu</span>
            <span style={{ color: 'var(--color-warning)' }}>■ Trả hộ</span>
            <span style={{ color: 'var(--color-success)' }}>■ Tiền thu được</span>
            <span className="chart-legend__unit">Đơn vị: {unit === 'billion' ? 'tỷ' : 'triệu'}</span>
          </div>
          {cashflowLoading ? (
            <div className="empty-state">Đang tải biểu đồ...</div>
          ) : cashflowError ? (
            <div className="alert alert--error">{cashflowError}</div>
          ) : cashflowPoints.length === 0 ? (
            <div className="empty-state">Không có dữ liệu trong kỳ đã chọn.</div>
          ) : (
            <div className="cashflow-chart">
              {cashflowPoints.map((point, index) => {
                const revenueRatio = maxCashflowValue ? point.revenueTotal / maxCashflowValue : 0
                const receiptedRatio = maxCashflowValue ? point.receipted / maxCashflowValue : 0
                const showLabel = index % cashflowLabelStep === 0 || index === cashflowPoints.length - 1
                const revenueHeight =
                  point.revenueTotal > 0 ? Math.max(4, revenueRatio * 100) : 0
                const receiptedHeight =
                  point.receipted > 0 ? Math.max(4, receiptedRatio * 100) : 0
                const segmentTotal = point.revenueTotal || 1
                return (
                  <div className="cashflow-chart__group" key={point.period}>
                    <div className="cashflow-chart__bars">
                      <div
                        className="cashflow-chart__stack"
                        style={{ height: `${revenueHeight}%` }}
                      >
                        <div
                          className="cashflow-chart__segment cashflow-chart__segment--invoice"
                          style={{ height: `${(point.invoiced / segmentTotal) * 100}%` }}
                          title={`${point.fullLabel} • Doanh thu: ${formatUnitValue(point.invoiced, unit)}`}
                        />
                        <div
                          className="cashflow-chart__segment cashflow-chart__segment--advance"
                          style={{ height: `${(point.advanced / segmentTotal) * 100}%` }}
                          title={`${point.fullLabel} • Trả hộ: ${formatUnitValue(point.advanced, unit)}`}
                        />
                      </div>
                      <div
                        className="cashflow-chart__bar cashflow-chart__bar--receipted"
                        style={{ height: `${receiptedHeight}%` }}
                        title={`${point.fullLabel} • Tiền thu được: ${formatUnitValue(point.receipted, unit)}`}
                      />
                    </div>
                    <div className="cashflow-chart__label">{showLabel ? point.label : ''}</div>
                  </div>
                )
              })}
            </div>
          )}
        </section>

        <section className="card">
          <h3>Trạng thái phân bổ</h3>
          <p className="muted">Tỷ trọng phiếu thu theo tình trạng phân bổ.</p>
          {allocationSummary.total > 0 ? (
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
            <div className="empty-state">Chưa có dữ liệu phân bổ.</div>
          )}
        </section>
      </section>

      <section className="dashboard-panels">
        <section className="card">
          <div className="card-row">
            <div>
              <h3 className="section-title">Top cần chú ý</h3>
              <p className="muted">Công nợ, quá hạn và số ngày trễ.</p>
            </div>
            <label className="field">
              <span>Hiển thị</span>
              <select value={topCount} onChange={(event) => setTopCount(Number(event.target.value))}>
                <option value={5}>Top 5</option>
                <option value={10}>Top 10</option>
              </select>
            </label>
          </div>
          <div className="stack-section">
            <h4 className="subsection-title">Top công nợ lớn nhất</h4>
            {renderTopList(overview?.topOutstanding ?? [], 'Chưa có công nợ.', false)}
          </div>
          <div className="stack-section">
            <h4 className="subsection-title">Top quá hạn lâu nhất</h4>
            {renderTopList(overview?.topOverdueDays ?? [], 'Chưa có khoản quá hạn.', true)}
          </div>
        </section>

        <div className="panel-stack">
          <section className="card">
            <h3>Top trả đúng hạn nhất</h3>
            <p className="muted">Khách hàng có tỷ lệ quá hạn thấp nhất trong kỳ.</p>
            {renderTopList(overview?.topOnTime ?? [], 'Chưa có khách hàng trả đúng hạn.', false)}
          </section>

          <section className="card">
            <div className="card-row">
              <div>
                <h3>Quá hạn theo phụ trách</h3>
                <p className="muted">Tổng giá trị và tỷ lệ quá hạn theo nhóm phụ trách.</p>
              </div>
            </div>
            {overdueGroupsLoading ? (
              <div className="empty-state">Đang tải thống kê quá hạn...</div>
            ) : overdueGroupsError ? (
              <div className="alert alert--error" role="alert" aria-live="assertive">{overdueGroupsError}</div>
            ) : (
              renderOverdueGroupList(overdueGroups, 'Chưa có dữ liệu quá hạn theo phụ trách.')
            )}
          </section>
        </div>
      </section>

      <section className="card">
        <div className="card-row">
          <div>
            <h3>Hành động nhanh</h3>
            <p className="muted">Thao tác nhanh với các mục cần xử lý.</p>
          </div>
        </div>
        {visibleActions.length === 0 ? (
          <div className="empty-state">Bạn không có quyền thao tác nhanh.</div>
        ) : (
          <div className="action-grid">
            {visibleActions.map((item) => (
              <Link key={item.id} to={item.to} className="action-card">
                <span className="action-card__label">Đi tới</span>
                <span className="action-card__value">{item.title}</span>
                <span className="action-card__meta">{item.description}</span>
              </Link>
            ))}
          </div>
        )}
      </section>
    </div>
  )
}
