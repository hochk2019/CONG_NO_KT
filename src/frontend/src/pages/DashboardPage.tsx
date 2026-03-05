import { Fragment, useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { ApiError } from '../api/client'
import {
  fetchDashboardPreferences,
  fetchDashboardOverview,
  fetchDashboardOverdueGroups,
  updateDashboardPreferences,
  type DashboardKpiDelta,
  type DashboardOverview,
  type DashboardPreferences,
  type DashboardOverdueGroupItem,
} from '../api/dashboard'
import { useAuth } from '../context/AuthStore'
import { useDebouncedValue } from '../hooks/useDebouncedValue'
import { usePersistedState } from '../hooks/usePersistedState'
import { useServerSyncedPreferences } from '../hooks/useServerSyncedPreferences'
import { formatDateTime, formatMoney } from '../utils/format'
import { buildAllocationSummary } from './shared/allocationSummary'
import { toDateInput } from './shared/dateInput'
import AllocationDonutCard from './dashboard/AllocationDonutCard'
import DashboardCashflowChart, {
  type TrendGranularity,
  type UnitScale,
} from './dashboard/DashboardCashflowChart'
import DashboardExecutiveSummary from './dashboard/DashboardExecutiveSummary'
import DashboardKpiSection from './dashboard/DashboardKpiSection'
import DashboardTopCustomers from './dashboard/DashboardTopCustomers'
import DashboardWidgetSettings from './dashboard/DashboardWidgetSettings'
import RoleCockpitSection, { type DashboardRoleView } from './dashboard/RoleCockpitSection'
import {
  defaultDashboardWidgetOrder,
  moveDashboardWidget,
  normalizeDashboardHiddenWidgets,
  normalizeDashboardWidgetOrder,
  type DashboardWidgetId,
} from './dashboard/dashboardWidgetPreferences'
import type { AllocationStatusKey, AllocationSummary } from './dashboard/AllocationDonutCard'
import './dashboard/dashboard.css'

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

const formatPeriodKeyLabel = (period: string) => {
  if (/^\d{4}-W\d{2}$/i.test(period)) {
    return `Tuần ${period.slice(-2)}`
  }
  if (/^\d{4}-\d{2}$/.test(period)) {
    const [year, month] = period.split('-')
    return `${month}/${year}`
  }
  return period
}

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
const receiptsStorageKey = {
  status: 'pref.receipts.status',
  allocationStatus: 'pref.receipts.allocationStatus',
}

type KpiDeltaDirection = 'higher-better' | 'lower-better'

const resolveRoleView = (roles: string[]): DashboardRoleView => {
  const normalizedRoles = roles.map((role) => role.toLowerCase())
  if (normalizedRoles.some((role) => ['director', 'ceo', 'admin'].includes(role))) {
    return 'director'
  }
  if (normalizedRoles.some((role) => ['manager', 'supervisor'].includes(role))) {
    return 'manager'
  }
  return 'operator'
}

const resolveKpiDeltaTone = (delta: number, direction: KpiDeltaDirection) => {
  if (delta === 0) return 'neutral'
  if (direction === 'higher-better') {
    return delta > 0 ? 'positive' : 'negative'
  }
  return delta < 0 ? 'positive' : 'negative'
}

const formatKpiDeltaValue = (value: number) => {
  const absolute = Math.abs(value)
  if (Number.isInteger(absolute)) {
    return absolute.toLocaleString('vi-VN')
  }

  return absolute.toLocaleString('vi-VN', { maximumFractionDigits: 1 })
}

const formatKpiDeltaPercent = (value: number | null) => {
  if (value === null) return ''
  const prefix = value > 0 ? '+' : ''
  return `${prefix}${value.toLocaleString('vi-VN', { maximumFractionDigits: 1 })}%`
}

const renderMomBadge = (
  delta: DashboardKpiDelta | undefined,
  direction: KpiDeltaDirection = 'lower-better',
) => {
  if (!delta) {
    return <div className="kpi-delta kpi-delta--neutral">─ Chưa có dữ liệu so sánh tháng trước.</div>
  }

  if (delta.delta === 0) {
    return <div className="kpi-delta kpi-delta--neutral">─ Không đổi so với tháng trước.</div>
  }

  const tone = resolveKpiDeltaTone(delta.delta, direction)
  const indicator = delta.delta > 0 ? '▲' : '▼'
  const directionText = delta.delta > 0 ? 'Tăng' : 'Giảm'
  const percent = formatKpiDeltaPercent(delta.deltaPercent)

  return (
    <div className={`kpi-delta kpi-delta--${tone}`}>
      {indicator} {directionText} {formatKpiDeltaValue(delta.delta)}
      {percent ? ` (${percent})` : ''} so với tháng trước.
    </div>
  )
}

type DashboardPreferenceState = {
  widgetOrder: DashboardWidgetId[]
  hiddenWidgets: DashboardWidgetId[]
}

const areDashboardPreferencesEqual = (
  first: DashboardPreferenceState | null,
  second: DashboardPreferenceState | null,
) => {
  if (!first || !second) return false
  if (first.widgetOrder.length !== second.widgetOrder.length) return false
  if (first.hiddenWidgets.length !== second.hiddenWidgets.length) return false

  const sameOrder = first.widgetOrder.every((item, index) => item === second.widgetOrder[index])
  if (!sameOrder) return false
  return first.hiddenWidgets.every((item, index) => item === second.hiddenWidgets[index])
}

const normalizeDashboardPreferences = (value: DashboardPreferences): DashboardPreferenceState => ({
  widgetOrder: normalizeDashboardWidgetOrder(value.widgetOrder),
  hiddenWidgets: normalizeDashboardHiddenWidgets(value.hiddenWidgets),
})

export default function DashboardPage() {
  const { state } = useAuth()
  const navigate = useNavigate()
  const token = state.accessToken ?? ''

  const [range, setRange] = useState('6m')
  const [topCount, setTopCount] = useState(5)
  const [unit, setUnit] = useState<UnitScale>('billion')
  const [trendGranularity, setTrendGranularity] = usePersistedState<TrendGranularity>(
    cashflowStorageKey.granularity,
    'week',
    {
      validate: (value): value is TrendGranularity => value === 'week' || value === 'month',
    },
  )
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
  const [widgetOrder, setWidgetOrder] = useState<DashboardWidgetId[]>(defaultDashboardWidgetOrder)
  const [hiddenWidgets, setHiddenWidgets] = useState<DashboardWidgetId[]>([])
  const [preferencesError, setPreferencesError] = useState<string | null>(null)
  const pendingPreferences = useMemo<DashboardPreferenceState>(
    () => ({
      widgetOrder,
      hiddenWidgets,
    }),
    [widgetOrder, hiddenWidgets],
  )
  const debouncedPreferences = useDebouncedValue(pendingPreferences, 500)
  const trendPeriods = useMemo(
    () => getTrendPeriodsFromRange(range, trendGranularity),
    [range, trendGranularity],
  )
  const applyDashboardPreferences = useCallback((value: DashboardPreferenceState) => {
    setWidgetOrder(value.widgetOrder)
    setHiddenWidgets(value.hiddenWidgets)
  }, [])

  useServerSyncedPreferences<DashboardPreferences, DashboardPreferenceState>({
    token,
    pendingPreferences: debouncedPreferences,
    fetchPreferences: fetchDashboardPreferences,
    updatePreferences: updateDashboardPreferences,
    toLocal: normalizeDashboardPreferences,
    applyLocal: applyDashboardPreferences,
    isEqual: areDashboardPreferencesEqual,
    onPersistStart: () => setPreferencesError(null),
    onPersistError: () => setPreferencesError('Không lưu được cấu hình widget dashboard.'),
  })

  const handleTrendGranularityChange = (value: TrendGranularity) => {
    setTrendGranularity(value)
  }

  const handleToggleWidgetVisibility = (widgetId: DashboardWidgetId, visible: boolean) => {
    setHiddenWidgets((prev) => {
      if (visible) {
        return prev.filter((item) => item !== widgetId)
      }
      return normalizeDashboardHiddenWidgets([...prev, widgetId])
    })
  }

  const handleMoveWidget = (widgetId: DashboardWidgetId, direction: 'up' | 'down') => {
    setWidgetOrder((prev) => moveDashboardWidget(prev, widgetId, direction))
  }

  useEffect(() => {
    if (!token) return
    let isActive = true

    const loadOverview = async () => {
      setError(null)
      setCashflowError(null)
      setCashflowLoading(true)
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
        setOverview(result)
        setCashflowTrend(result.trend)
        setCashflowRange({ from: result.trendFrom, to: result.trendTo })
        setLastUpdated(result.lastUpdatedAt ? new Date(result.lastUpdatedAt) : new Date())
      } catch (err) {
        if (!isActive) return
        if (err instanceof ApiError) {
          const message = err.status === 500 ? 'Không tải được dữ liệu tổng quan.' : err.message
          setError(message)
          setCashflowError(message)
        } else {
          setError('Không tải được dữ liệu tổng quan.')
          setCashflowError('Không tải được dữ liệu tổng quan.')
        }
        setCashflowRange(null)
      } finally {
        if (isActive) {
          setCashflowLoading(false)
        }
      }
    }

    loadOverview()
    return () => {
      isActive = false
    }
  }, [token, range, topCount, trendGranularity, trendPeriods])

  useEffect(() => {
    if (!token) return
    let isActive = true

    const loadOverdueGroups = async () => {
      setOverdueGroupsLoading(true)
      setOverdueGroupsError(null)
      try {
        const rangeParams = buildRangeParams(range)
        const rows = await fetchDashboardOverdueGroups({
          token,
          asOf: rangeParams.to ?? toDateInput(new Date()),
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
    }

    loadOverdueGroups()
    return () => {
      isActive = false
    }
  }, [token, range, topCount])

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
        expected: point.expectedTotal,
        actual: point.actualTotal,
        variance: point.variance,
      }
    })
  }, [cashflowRange, cashflowTrend, trendGranularity])

  const forecastPoints = useMemo(
    () =>
      (overview?.cashflowForecast ?? []).map((point) => ({
        period: point.period,
        label: formatPeriodKeyLabel(point.period),
        expected: point.expectedTotal,
        actual: point.actualTotal,
        variance: point.variance,
      })),
    [overview],
  )

  const maxCashflowValue = useMemo(() => {
    if (cashflowPoints.length === 0) {
      return 0
    }
    return Math.max(...cashflowPoints.flatMap((point) => [point.expected, point.actual]))
  }, [cashflowPoints])

  const hasMeaningfulCashflow = useMemo(
    () =>
      cashflowPoints.some(
        (point) =>
          Math.abs(point.expected) > 0 || Math.abs(point.actual) > 0 || Math.abs(point.variance) > 0,
      ),
    [cashflowPoints],
  )

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
        expected: acc.expected + point.expectedTotal,
        actual: acc.actual + point.actualTotal,
      }),
      { invoiced: 0, advanced: 0, expected: 0, actual: 0 },
    )

    if (!totals) {
      return { invoiced: 0, advanced: 0, expected: 0, actual: 0, variance: 0, actualRatio: 0 }
    }

    const variance = totals.actual - totals.expected
    const actualRatio =
      totals.expected > 0 ? Math.round((totals.actual / totals.expected) * 1000) / 10 : 0
    return { ...totals, variance, actualRatio }
  }, [overview])

  const allocationSummary = useMemo<AllocationSummary>(
    () => buildAllocationSummary(overview?.allocationStatuses ?? []),
    [overview],
  )

  const executiveSummary = overview?.executiveSummary
  const roleView = resolveRoleView(state.roles)

  const handleAllocationDrilldown = (status: AllocationStatusKey) => {
    if (typeof window !== 'undefined') {
      window.localStorage.removeItem(receiptsStorageKey.status)
      window.localStorage.setItem(receiptsStorageKey.allocationStatus, status)
    }
    navigate('/receipts')
  }

  const widgetSections: Record<DashboardWidgetId, ReactNode> = {
    executiveSummary: <DashboardExecutiveSummary summary={executiveSummary} />,
    roleCockpit: (
      <RoleCockpitSection
        roleView={roleView}
        kpis={overview?.kpis}
        topOverdue={overview?.topOverdueDays ?? []}
      />
    ),
    kpis: (
      <DashboardKpiSection
        overview={overview}
        periodTotals={periodTotals}
        isLoading={cashflowLoading}
        renderMomBadge={renderMomBadge}
        formatMoney={formatMoney}
      />
    ),
    cashflow: (
      <section className="dashboard-charts">
        <DashboardCashflowChart
          loading={cashflowLoading}
          error={cashflowError}
          points={cashflowPoints}
          forecastPoints={forecastPoints}
          maxValue={maxCashflowValue}
          hasMeaningfulData={hasMeaningfulCashflow}
          labelStep={cashflowLabelStep}
          trendGranularity={trendGranularity}
          unit={unit}
          onChangeTrendGranularity={handleTrendGranularityChange}
          onChangeUnit={setUnit}
          formatMoney={formatMoney}
          formatUnitValue={formatUnitValue}
        />

        <AllocationDonutCard summary={allocationSummary} onDrilldown={handleAllocationDrilldown} />
      </section>
    ),
    panels: (
      <DashboardTopCustomers
        topCount={topCount}
        onTopCountChange={setTopCount}
        topOutstanding={overview?.topOutstanding ?? []}
        topOverdueDays={overview?.topOverdueDays ?? []}
        topOnTime={overview?.topOnTime ?? []}
        overdueGroups={overdueGroups}
        overdueGroupsLoading={overdueGroupsLoading}
        overdueGroupsError={overdueGroupsError}
      />
    ),
  }

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
          <DashboardWidgetSettings
            order={widgetOrder}
            hiddenWidgets={hiddenWidgets}
            onToggleVisibility={handleToggleWidgetVisibility}
            onMove={handleMoveWidget}
            triggerClassName="dashboard-filters__settings-btn"
          />
        </div>
      </header>

      {error && <div className="alert alert--error" role="alert" aria-live="assertive">{error}</div>}
      {preferencesError && (
        <div className="alert alert--error" role="alert" aria-live="assertive">
          {preferencesError}
        </div>
      )}

      {widgetOrder.map((widgetId) => {
        if (hiddenWidgets.includes(widgetId)) return null
        return <Fragment key={widgetId}>{widgetSections[widgetId]}</Fragment>
      })}
    </div>
  )
}
