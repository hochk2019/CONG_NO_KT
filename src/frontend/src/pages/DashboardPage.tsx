import { Fragment, useEffect, useMemo, useState, type ReactNode } from 'react'
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
  type DashboardTopItem,
} from '../api/dashboard'
import { useAuth } from '../context/AuthStore'
import { useDebouncedValue } from '../hooks/useDebouncedValue'
import { usePersistedState } from '../hooks/usePersistedState'
import { formatDateTime, formatMoney } from '../utils/format'
import { toDateInput } from './reports/reportUtils'
import AllocationDonutCard from './dashboard/AllocationDonutCard'
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

const resolveSummaryTone = (status?: string) => {
  if (status === 'critical') return 'critical'
  if (status === 'warning') return 'warning'
  if (status === 'good') return 'good'
  return 'stable'
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
    return <div className="kpi-delta kpi-delta--neutral">Chưa có dữ liệu so sánh tháng trước.</div>
  }

  if (delta.delta === 0) {
    return <div className="kpi-delta kpi-delta--neutral">Không đổi so với tháng trước.</div>
  }

  const tone = resolveKpiDeltaTone(delta.delta, direction)
  const directionText = delta.delta > 0 ? 'Tăng' : 'Giảm'
  const percent = formatKpiDeltaPercent(delta.deltaPercent)

  return (
    <div className={`kpi-delta kpi-delta--${tone}`}>
      {directionText} {formatKpiDeltaValue(delta.delta)}
      {percent ? ` (${percent})` : ''} so với tháng trước.
    </div>
  )
}

const areDashboardPreferencesEqual = (
  first: DashboardPreferences | null,
  second: DashboardPreferences | null,
) => {
  if (!first || !second) return false
  if (first.widgetOrder.length !== second.widgetOrder.length) return false
  if (first.hiddenWidgets.length !== second.hiddenWidgets.length) return false

  const sameOrder = first.widgetOrder.every((item, index) => item === second.widgetOrder[index])
  if (!sameOrder) return false
  return first.hiddenWidgets.every((item, index) => item === second.hiddenWidgets[index])
}

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
  const [preferencesLoaded, setPreferencesLoaded] = useState(false)
  const [preferencesError, setPreferencesError] = useState<string | null>(null)
  const [lastSavedPreferences, setLastSavedPreferences] = useState<DashboardPreferences | null>(null)
  const pendingPreferences = useMemo<DashboardPreferences>(
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

  const handleTrendGranularityChange = (value: TrendGranularity) => {
    setTrendGranularity(value)
  }

  useEffect(() => {
    if (!token) return
    let isActive = true

    const loadPreferences = async () => {
      try {
        const result = await fetchDashboardPreferences(token)
        if (!isActive) return
        const normalizedOrder = normalizeDashboardWidgetOrder(result.widgetOrder)
        const normalizedHidden = normalizeDashboardHiddenWidgets(result.hiddenWidgets)
        setWidgetOrder(normalizedOrder)
        setHiddenWidgets(normalizedHidden)
        setLastSavedPreferences({
          widgetOrder: normalizedOrder,
          hiddenWidgets: normalizedHidden,
        })
      } catch {
        if (!isActive) return
      } finally {
        if (isActive) {
          setPreferencesLoaded(true)
        }
      }
    }

    loadPreferences()
    return () => {
      isActive = false
    }
  }, [token])

  useEffect(() => {
    if (!token || !preferencesLoaded) return
    if (areDashboardPreferencesEqual(lastSavedPreferences, debouncedPreferences)) return

    let isActive = true

    const persistPreferences = async () => {
      setPreferencesError(null)
      try {
        const result = await updateDashboardPreferences(token, debouncedPreferences)
        if (!isActive) return
        const normalizedOrder = normalizeDashboardWidgetOrder(result.widgetOrder)
        const normalizedHidden = normalizeDashboardHiddenWidgets(result.hiddenWidgets)
        setWidgetOrder(normalizedOrder)
        setHiddenWidgets(normalizedHidden)
        setLastSavedPreferences({
          widgetOrder: normalizedOrder,
          hiddenWidgets: normalizedHidden,
        })
      } catch {
        if (!isActive) return
        setPreferencesError('Không lưu được cấu hình widget dashboard.')
      }
    }

    persistPreferences()
    return () => {
      isActive = false
    }
  }, [token, preferencesLoaded, debouncedPreferences, lastSavedPreferences])

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

  const allocationSummary = useMemo<AllocationSummary>(() => {
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
    const items: AllocationSummary['items'] = [
      { key: 'ALLOCATED', label: 'Đã phân bổ', amount: bucket.ALLOCATED, percent: 0 },
      { key: 'PARTIAL', label: 'Phân bổ một phần', amount: bucket.PARTIAL, percent: 0 },
      { key: 'UNALLOCATED', label: 'Chưa phân bổ', amount: bucket.UNALLOCATED, percent: 0 },
    ]

    return {
      total,
      items: items.map((item) => ({
        ...item,
        percent: total > 0 ? Math.round((item.amount / total) * 1000) / 10 : 0,
      })),
    }
  }, [overview])

  const executiveSummary = overview?.executiveSummary
  const summaryTone = resolveSummaryTone(executiveSummary?.status)
  const roleView = resolveRoleView(state.roles)

  const handleAllocationDrilldown = (status: AllocationStatusKey) => {
    if (typeof window !== 'undefined') {
      window.localStorage.removeItem(receiptsStorageKey.status)
      window.localStorage.setItem(receiptsStorageKey.allocationStatus, status)
    }
    navigate('/receipts')
  }

  const widgetSections: Record<DashboardWidgetId, ReactNode> = {
    executiveSummary: executiveSummary ? (
      <section className={`dashboard-summary dashboard-summary--${summaryTone}`}>
        <div className="dashboard-summary__content">
          <p className="dashboard-summary__label">Tóm tắt điều hành</p>
          <h3 className="dashboard-summary__title">{executiveSummary.message}</h3>
          <p className="dashboard-summary__hint">{executiveSummary.actionHint}</p>
        </div>
        <div className="dashboard-summary__meta">
          <span>Cập nhật: {formatDateTime(executiveSummary.generatedAt)}</span>
        </div>
      </section>
    ) : null,
    roleCockpit: (
      <RoleCockpitSection
        roleView={roleView}
        kpis={overview?.kpis}
        topOverdue={overview?.topOverdueDays ?? []}
      />
    ),
    kpis: (
      <section className="kpi-stack">
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
            <div className="stat-card__value">
              {formatMoney(overview?.kpis.unallocatedReceiptsAmount ?? 0)}
            </div>
            <div className="stat-card__meta">
              {overview?.kpis.unallocatedReceiptsCount ?? 0} phiếu thu treo
            </div>
            {renderMomBadge(overview?.kpiMoM?.unallocatedReceiptsAmount, 'lower-better')}
          </div>
          <div className={`stat-card${(overview?.kpis.overdueTotal ?? 0) > 0 ? ' stat-card--danger' : ''}`}>
            <div className="stat-card__label">Quá hạn</div>
            <div className="stat-card__value">{formatMoney(overview?.kpis.overdueTotal ?? 0)}</div>
            <div className="stat-card__meta">
              {overview?.kpis.overdueCustomers ?? 0} khách hàng đang quá hạn
            </div>
            {renderMomBadge(overview?.kpiMoM?.overdueTotal, 'lower-better')}
          </div>
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
            <div className="stat-card__meta">
              {periodTotals.variance >= 0 ? 'Thu vượt kỳ vọng' : 'Thu thấp hơn kỳ vọng'}
            </div>
          </div>
          <div className="stat-card stat-card--secondary">
            <div className="stat-card__label">% Actual/Expected</div>
            <div className="stat-card__value">{periodTotals.actualRatio}%</div>
            <div className="stat-card__meta">Hiệu suất thu hồi trong kỳ</div>
          </div>
        </div>
      </section>
    ),
    cashflow: (
      <section className="dashboard-charts">
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
            <span style={{ color: 'var(--color-accent)' }}>■ Expected (Kỳ vọng)</span>
            <span style={{ color: 'var(--color-success)' }}>■ Actual (Thực thu)</span>
            <span style={{ color: 'var(--color-warning)' }}>■ Variance</span>
            <span className="chart-legend__unit">Đơn vị: {unit === 'billion' ? 'tỷ' : 'triệu'}</span>
          </div>
          {cashflowLoading ? (
            <div className="empty-state">Đang tải biểu đồ...</div>
          ) : cashflowError ? (
            <div className="alert alert--error">{cashflowError}</div>
          ) : cashflowPoints.length === 0 ? (
            <div className="empty-state">Không có dữ liệu trong kỳ đã chọn.</div>
          ) : (
            <>
              <div className="cashflow-chart">
                {cashflowPoints.map((point, index) => {
                  const expectedRatio = maxCashflowValue ? point.expected / maxCashflowValue : 0
                  const actualRatio = maxCashflowValue ? point.actual / maxCashflowValue : 0
                  const showLabel = index % cashflowLabelStep === 0 || index === cashflowPoints.length - 1
                  const expectedHeight = point.expected > 0 ? Math.max(4, expectedRatio * 100) : 0
                  const actualHeight = point.actual > 0 ? Math.max(4, actualRatio * 100) : 0
                  return (
                    <div className="cashflow-chart__group" key={point.period}>
                      <div className="cashflow-chart__bars">
                        <div
                          className="cashflow-chart__bar cashflow-chart__bar--expected"
                          style={{ height: `${expectedHeight}%` }}
                          title={`${point.fullLabel} • Expected: ${formatUnitValue(point.expected, unit)}`}
                        />
                        <div
                          className="cashflow-chart__bar cashflow-chart__bar--actual"
                          style={{ height: `${actualHeight}%` }}
                          title={`${point.fullLabel} • Actual: ${formatUnitValue(point.actual, unit)}`}
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
              {forecastPoints.length > 0 && (
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
              )}
            </>
          )}
        </section>

        <AllocationDonutCard summary={allocationSummary} onDrilldown={handleAllocationDrilldown} />
      </section>
    ),
    panels: (
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
