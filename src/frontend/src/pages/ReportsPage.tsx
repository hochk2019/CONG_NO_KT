import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useLocation } from 'react-router-dom'
import { ApiError } from '../api/client'
import {
  exportReport,
  fetchReportAgingPaged,
  fetchReportOverview,
  fetchReportPreferences,
  fetchReportStatementPaged,
  fetchReportSummaryPaged,
  updateReportPreferences,
  type ReportExportFormat,
  type ReportExportKind,
  type ReportAgingRow,
  type ReportCharts,
  type ReportInsights,
  type ReportKpi,
  type ReportPreferences,
  type ReportStatementPagedResult,
  type ReportSummaryRow,
} from '../api/reports'
import {
  fetchCustomerLookup,
  fetchOwnerLookup,
  fetchSellerLookup,
  mapOwnerOptions,
  mapTaxCodeOptions,
  type LookupOption,
} from '../api/lookups'
import { useAuth } from '../context/AuthStore'
import { useDebouncedValue } from '../hooks/useDebouncedValue'
import { usePagination } from '../hooks/usePagination'
import { usePersistedState } from '../hooks/usePersistedState'
import { formatDate } from '../utils/format'
import { ReportsChartsSection } from './reports/ReportsChartsSection'
import { ReportsFilters, type ReportPreset } from './reports/ReportsFilters'
import { ReportsInsightsSection } from './reports/ReportsInsightsSection'
import { ReportsKpiSection } from './reports/ReportsKpiSection'
import { ReportsQuickActions } from './reports/ReportsQuickActions'
import { ReportsTablesSection } from './reports/ReportsTablesSection'
import { ReportsValidationModal } from './reports/ReportsValidationModal'
import {
  buildPresetList,
  defaultKpiOrder,
  groupByLabels,
  quickActionsStorageKey,
  resolveOptionLabel,
  toDateInput,
  type ReportPresetConfig,
} from './reports/reportUtils'
import './reports/reports.css'

type ExportJob = {
  id: number
  state: 'running' | 'done' | 'error'
  fileName?: string
  label?: string
  startedAt: string
}

const DEFAULT_PAGE_SIZE = 20
const PAGE_SIZE_OPTIONS = [10, 20, 50, 100]
const TOP_OUTSTANDING_OPTIONS = [5, 10]
const SUMMARY_PAGE_SIZE_KEY = 'reports.summary.pageSize'
const STATEMENT_PAGE_SIZE_KEY = 'reports.statement.pageSize'
const AGING_PAGE_SIZE_KEY = 'reports.aging.pageSize'
const TOP_OUTSTANDING_KEY = 'reports.insights.topOutstanding'

const isAllowedPageSize = (value: unknown): value is number =>
  typeof value === 'number' && PAGE_SIZE_OPTIONS.includes(value)

const isAllowedTopOutstanding = (value: unknown): value is number =>
  typeof value === 'number' && TOP_OUTSTANDING_OPTIONS.includes(value)

const arePreferencesEqual = (
  first: { kpiOrder: string[]; dueSoonDays: number } | null,
  second: { kpiOrder: string[]; dueSoonDays: number } | null,
) => {
  if (!first || !second) return false
  if (first.dueSoonDays !== second.dueSoonDays) return false
  if (first.kpiOrder.length !== second.kpiOrder.length) return false
  return first.kpiOrder.every((item, index) => item === second.kpiOrder[index])
}

export function ReportsPage() {
  const { state } = useAuth()
  const token = state.accessToken ?? ''
  const location = useLocation()

  const [sellerOptions, setSellerOptions] = useState<LookupOption[]>([])
  const [customerOptions, setCustomerOptions] = useState<LookupOption[]>([])
  const [ownerOptions, setOwnerOptions] = useState<LookupOption[]>([])
  const [sellerQuery, setSellerQuery] = useState('')
  const [customerQuery, setCustomerQuery] = useState('')
  const debouncedSellerQuery = useDebouncedValue(sellerQuery, 300)
  const debouncedCustomerQuery = useDebouncedValue(customerQuery, 300)

  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [asOfDate, setAsOfDate] = useState('')
  const [useCustomAsOf, setUseCustomAsOf] = useState(false)
  const [sellerTaxCode, setSellerTaxCode] = useState('')
  const [customerTaxCode, setCustomerTaxCode] = useState('')
  const [ownerId, setOwnerId] = useState('')
  const [groupBy, setGroupBy] = useState('customer')
  const [filterText, setFilterText] = useState('')

  const [kpis, setKpis] = useState<ReportKpi | null>(null)
  const [charts, setCharts] = useState<ReportCharts | null>(null)
  const [insights, setInsights] = useState<ReportInsights | null>(null)
  const [summaryRows, setSummaryRows] = useState<ReportSummaryRow[]>([])
  const [summaryPageSizePreference, setSummaryPageSizePreference] = usePersistedState<number>(
    SUMMARY_PAGE_SIZE_KEY,
    DEFAULT_PAGE_SIZE,
    { validate: isAllowedPageSize },
  )
  const summaryPagination = usePagination({
    initialPage: 1,
    initialPageSize: summaryPageSizePreference,
    initialTotal: 0,
  })
  const [summarySortKey, setSummarySortKey] = useState('')

  const [statement, setStatement] = useState<ReportStatementPagedResult | null>(null)
  const [statementPageSizePreference, setStatementPageSizePreference] = usePersistedState<number>(
    STATEMENT_PAGE_SIZE_KEY,
    DEFAULT_PAGE_SIZE,
    { validate: isAllowedPageSize },
  )
  const statementPagination = usePagination({
    initialPage: 1,
    initialPageSize: statementPageSizePreference,
    initialTotal: 0,
  })

  const [agingRows, setAgingRows] = useState<ReportAgingRow[]>([])
  const [agingPageSizePreference, setAgingPageSizePreference] = usePersistedState<number>(
    AGING_PAGE_SIZE_KEY,
    DEFAULT_PAGE_SIZE,
    { validate: isAllowedPageSize },
  )
  const agingPagination = usePagination({
    initialPage: 1,
    initialPageSize: agingPageSizePreference,
    initialTotal: 0,
  })
  const [agingSortKey, setAgingSortKey] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loadingAction, setLoadingAction] = useState('')
  const [exportJobs, setExportJobs] = useState<ExportJob[]>([])
  const [validationModal, setValidationModal] = useState<{
    title: string
    message: string
    confirmLabel?: string
    onConfirm?: () => void
  } | null>(null)

  const [preferencesLoaded, setPreferencesLoaded] = useState(false)
  const [lastSavedPreferences, setLastSavedPreferences] = useState<ReportPreferences | null>(null)
  const [kpiOrder, setKpiOrder] = useState<string[]>(defaultKpiOrder)
  const [dueSoonDays, setDueSoonDays] = useState(7)
  const [overviewLoaded, setOverviewLoaded] = useState(false)
  const lastOverviewKey = useRef<string | null>(null)
  const overviewInFlight = useRef(false)
  const [topOutstandingCount, setTopOutstandingCount] = usePersistedState<number>(
    TOP_OUTSTANDING_KEY,
    TOP_OUTSTANDING_OPTIONS[0],
    { validate: isAllowedTopOutstanding },
  )
  const [quickActionsOpen, setQuickActionsOpen] = usePersistedState<boolean>(
    quickActionsStorageKey,
    true,
    { validate: (value): value is boolean => typeof value === 'boolean' },
  )

  const presets = useMemo<ReportPresetConfig[]>(() => buildPresetList(), [])
  const presetOptions = useMemo<ReportPreset[]>(
    () => presets.map(({ id, label }) => ({ id, label })),
    [presets],
  )

  const pendingPreferences = useMemo(
    () => ({ kpiOrder, dueSoonDays }),
    [kpiOrder, dueSoonDays],
  )
  const debouncedPreferences = useDebouncedValue(pendingPreferences, 600)

  useEffect(() => {
    if (!location.hash) return
    const target = document.getElementById(location.hash.slice(1))
    if (target) {
      target.scrollIntoView({ behavior: 'smooth', block: 'start' })
    }
  }, [location.hash])

  useEffect(() => {
    if (!token) return
    let isActive = true

    const loadSellers = async () => {
      try {
        const result = await fetchSellerLookup({
          token,
          search: debouncedSellerQuery || undefined,
          limit: 200,
        })
        if (!isActive) return
        setSellerOptions(mapTaxCodeOptions(result))
      } catch {
        if (!isActive) return
        setSellerOptions([])
      }
    }

    loadSellers()
    return () => {
      isActive = false
    }
  }, [token, debouncedSellerQuery])

  useEffect(() => {
    if (!token) return
    let isActive = true

    const loadCustomers = async () => {
      try {
        const result = await fetchCustomerLookup({
          token,
          search: debouncedCustomerQuery || undefined,
          limit: 200,
        })
        if (!isActive) return
        setCustomerOptions(mapTaxCodeOptions(result))
      } catch {
        if (!isActive) return
        setCustomerOptions([])
      }
    }

    loadCustomers()
    return () => {
      isActive = false
    }
  }, [token, debouncedCustomerQuery])

  useEffect(() => {
    if (!token) return
    let isActive = true

    const loadOwners = async () => {
      try {
        const result = await fetchOwnerLookup({ token, limit: 200 })
        if (!isActive) return
        setOwnerOptions(mapOwnerOptions(result))
      } catch {
        if (!isActive) return
        setOwnerOptions([])
      }
    }

    loadOwners()
    return () => {
      isActive = false
    }
  }, [token])

  useEffect(() => {
    if (!token) return
    let isActive = true
    const loadPreferences = async () => {
      try {
        const result = await fetchReportPreferences(token)
        if (!isActive) return
        setKpiOrder(result.kpiOrder.length ? result.kpiOrder : defaultKpiOrder)
        setDueSoonDays(result.dueSoonDays || 7)
        setLastSavedPreferences(result)
        setPreferencesLoaded(true)
      } catch {
        if (!isActive) return
        setPreferencesLoaded(true)
      }
    }

    loadPreferences()
    return () => {
      isActive = false
    }
  }, [token])

  useEffect(() => {
    if (!token || !preferencesLoaded) return
    if (arePreferencesEqual(lastSavedPreferences, debouncedPreferences)) return
    let isActive = true

    const persistPreferences = async () => {
      try {
        const result = await updateReportPreferences(token, debouncedPreferences)
        if (!isActive) return
        setLastSavedPreferences(result)
        setKpiOrder(result.kpiOrder.length ? result.kpiOrder : defaultKpiOrder)
        setDueSoonDays(result.dueSoonDays || 7)
      } catch {
        if (!isActive) return
        setError('Không lưu được cấu hình báo cáo.')
      }
    }

    persistPreferences()
    return () => {
      isActive = false
    }
  }, [token, preferencesLoaded, debouncedPreferences, lastSavedPreferences])

  const baseParams = useMemo(
    () => ({
      from: from || undefined,
      to: to || undefined,
      asOfDate: asOfDate || undefined,
      sellerTaxCode: sellerTaxCode || undefined,
      customerTaxCode: customerTaxCode || undefined,
      ownerId: ownerId || undefined,
    }),
    [from, to, asOfDate, sellerTaxCode, customerTaxCode, ownerId],
  )
  const overviewKey = useMemo(
    () => JSON.stringify({ ...baseParams, dueSoonDays, topOutstandingCount }),
    [baseParams, dueSoonDays, topOutstandingCount],
  )

  const handleFromChange = (value: string) => setFrom(value)
  const handleToChange = (value: string) => {
    setTo(value)
    if (!useCustomAsOf) {
      setAsOfDate(value)
    }
  }
  const handleAsOfChange = (value: string) => {
    setUseCustomAsOf(true)
    setAsOfDate(value)
  }
  const handleSellerChange = (value: string) => {
    setSellerTaxCode(value)
    setSellerQuery(value)
  }
  const handleCustomerChange = (value: string) => {
    setCustomerTaxCode(value)
    setCustomerQuery(value)
  }
  const handleOwnerChange = (value: string) => setOwnerId(value)
  const handleGroupByChange = (value: string) => setGroupBy(value)
  const handleFilterTextChange = (value: string) => setFilterText(value)

  const scrollToSection = useCallback((id: string) => {
    const target = document.getElementById(id)
    if (target) {
      target.scrollIntoView({ behavior: 'smooth', block: 'start' })
    }
  }, [])
  const handlePrint = useCallback(() => {
    window.print()
  }, [])

  const showValidation = useCallback((title: string, message: string) => {
    setError(null)
    setValidationModal({ title, message })
  }, [])

  const showConfirm = useCallback(
    (title: string, message: string, onConfirm: () => void, confirmLabel?: string) => {
      setError(null)
      setValidationModal({ title, message, confirmLabel, onConfirm })
    },
    [],
  )

  const closeValidation = useCallback(() => {
    setValidationModal(null)
  }, [])

  const quickActions = useMemo(
    () => [
      { id: 'filters', label: 'Bộ lọc' },
      { id: 'overview', label: 'Tổng quan' },
      { id: 'summary', label: 'Báo cáo tổng hợp' },
      { id: 'statement', label: 'Sao kê khách hàng' },
      { id: 'aging', label: 'Báo cáo tuổi nợ' },
    ],
    [],
  )

  const ensureDateRange = useCallback(
    (actionLabel: string) => {
      if (!from || !to) {
        showValidation(
          'Thiếu khoảng thời gian',
          `Vui lòng chọn "Từ ngày" và "Đến ngày" trước khi ${actionLabel}.`,
        )
        return false
      }
      if (from > to) {
        showValidation('Khoảng thời gian không hợp lệ', 'Từ ngày phải nhỏ hơn hoặc bằng Đến ngày.')
        return false
      }
      return true
    },
    [from, showValidation, to],
  )

  const ensureAsOfDate = useCallback(
    (actionLabel: string) => {
      if (!asOfDate) {
        showValidation(
          'Thiếu ngày tính đến',
          `Vui lòng chọn "Tính đến ngày" trước khi ${actionLabel}.`,
        )
        return false
      }
      return true
    },
    [asOfDate, showValidation],
  )

  const applyPreset = (presetId: string) => {
    const preset = presets.find((item) => item.id === presetId)
    if (!preset) return
    const fromValue = toDateInput(preset.from)
    const toValue = toDateInput(preset.to)
    setFrom(fromValue)
    setTo(toValue)
    setAsOfDate(toValue)
  }

  const resetFilters = () => {
    setFrom('')
    setTo('')
    setAsOfDate('')
    setSellerTaxCode('')
    setCustomerTaxCode('')
    setOwnerId('')
    setGroupBy('customer')
    setFilterText('')
    setUseCustomAsOf(false)
    summaryPagination.reset()
    statementPagination.reset()
    agingPagination.reset()
  }

  const handleLoadOverview = useCallback(async () => {
    if (!token) {
      setError('Vui lòng đăng nhập.')
      return
    }
    if (!ensureDateRange('tải tổng quan')) return
    if (overviewInFlight.current) return
    overviewInFlight.current = true
    lastOverviewKey.current = JSON.stringify({ ...baseParams, dueSoonDays, topOutstandingCount })
    setError(null)
    setLoadingAction('overview')
    try {
      const result = await fetchReportOverview(token, {
        ...baseParams,
        dueSoonDays,
        top: topOutstandingCount,
      })
      setKpis(result.kpis)
      setCharts(result.charts)
      setInsights(result.insights)
      setOverviewLoaded(true)
      scrollToSection('overview')
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else {
        setError('Không tải được tổng quan báo cáo.')
      }
    } finally {
      setLoadingAction('')
      overviewInFlight.current = false
    }
  }, [token, ensureDateRange, baseParams, dueSoonDays, topOutstandingCount, scrollToSection])

  useEffect(() => {
    if (!overviewLoaded || loadingAction === 'overview') return
    if (!token) return
    if (!preferencesLoaded) return
    if (!from || !to) return
    if (overviewInFlight.current) return
    if (lastOverviewKey.current === overviewKey) return
    void handleLoadOverview()
  }, [
    overviewKey,
    handleLoadOverview,
    loadingAction,
    overviewLoaded,
    preferencesLoaded,
    from,
    to,
    token,
  ])

  const loadSummary = async (
    nextPage = summaryPagination.page,
    nextPageSize = summaryPagination.pageSize,
    nextSortKey = summarySortKey,
  ) => {
    if (!token) {
      setError('Vui lòng đăng nhập.')
      return
    }
    if (!ensureDateRange('tải báo cáo tổng hợp')) return
    setError(null)
    setLoadingAction('summary')
    try {
      const result = await fetchReportSummaryPaged(token, {
        ...baseParams,
        groupBy,
        page: nextPage,
        pageSize: nextPageSize,
        sortKey: nextSortKey || undefined,
        sortDirection: nextSortKey ? 'desc' : undefined,
      })
      setSummaryRows(result.items)
      summaryPagination.update({
        page: result.page,
        pageSize: result.pageSize,
        total: result.total,
      })
      setSummaryPageSizePreference(result.pageSize)
      setSummarySortKey(nextSortKey)
      scrollToSection('summary')
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else {
        setError('Không tải được báo cáo tổng hợp.')
      }
    } finally {
      setLoadingAction('')
    }
  }

  const handleSummary = async () => {
    await loadSummary(1, summaryPagination.pageSize, summarySortKey)
  }

  const loadStatement = async (
    nextPage = statementPagination.page,
    nextPageSize = statementPagination.pageSize,
  ) => {
    if (!token) {
      setError('Vui lòng đăng nhập.')
      return
    }
    if (!ensureDateRange('tải sao kê')) return
    setError(null)
    setLoadingAction('statement')
    try {
      const result = await fetchReportStatementPaged(token, {
        ...baseParams,
        page: nextPage,
        pageSize: nextPageSize,
      })
      setStatement(result)
      statementPagination.update({
        page: result.page,
        pageSize: result.pageSize,
        total: result.total,
      })
      setStatementPageSizePreference(result.pageSize)
      scrollToSection('statement')
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else {
        setError('Không tải được báo cáo sao kê.')
      }
    } finally {
      setLoadingAction('')
    }
  }

  const handleStatement = async () => {
    if (!customerTaxCode.trim()) {
      showConfirm(
        'Chưa chọn MST bên mua',
        'Hệ thống sẽ tải sao kê tổng hợp theo khoảng thời gian đã chọn. Bạn có thể chọn MST bên mua để xem riêng từng khách hàng.',
        () => {
          closeValidation()
          void loadStatement(1, statementPagination.pageSize)
        },
        'Tiếp tục tải',
      )
      return
    }

    await loadStatement(1, statementPagination.pageSize)
  }
  const loadAging = async (
    nextPage = agingPagination.page,
    nextPageSize = agingPagination.pageSize,
    nextSortKey = agingSortKey,
  ) => {
    if (!token) {
      setError('Vui lòng đăng nhập.')
      return
    }
    if (!ensureAsOfDate('tải tuổi nợ')) return
    setError(null)
    setLoadingAction('aging')
    try {
      const result = await fetchReportAgingPaged(token, {
        ...baseParams,
        page: nextPage,
        pageSize: nextPageSize,
        sortKey: nextSortKey || undefined,
        sortDirection: nextSortKey ? 'desc' : undefined,
      })
      setAgingRows(result.items)
      agingPagination.update({
        page: result.page,
        pageSize: result.pageSize,
        total: result.total,
      })
      setAgingPageSizePreference(result.pageSize)
      setAgingSortKey(nextSortKey)
      scrollToSection('aging')
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else {
        setError('Không tải được báo cáo tuổi nợ.')
      }
    } finally {
      setLoadingAction('')
    }
  }

  const handleAging = async () => {
    await loadAging(1, agingPagination.pageSize, agingSortKey)
  }

  const handleSummaryPageChange = (page: number) => {
    void loadSummary(page, summaryPagination.pageSize, summarySortKey)
  }

  const handleSummaryPageSizeChange = (pageSize: number) => {
    summaryPagination.setPageSize(pageSize)
    setSummaryPageSizePreference(pageSize)
    void loadSummary(1, pageSize, summarySortKey)
  }

  const handleSummarySortChange = (sortKey: string) => {
    setSummarySortKey(sortKey)
    summaryPagination.setPage(1)
    void loadSummary(1, summaryPagination.pageSize, sortKey)
  }

  const handleStatementPageChange = (page: number) => {
    void loadStatement(page, statementPagination.pageSize)
  }

  const handleStatementPageSizeChange = (pageSize: number) => {
    statementPagination.setPageSize(pageSize)
    setStatementPageSizePreference(pageSize)
    void loadStatement(1, pageSize)
  }

  const handleAgingPageChange = (page: number) => {
    void loadAging(page, agingPagination.pageSize, agingSortKey)
  }

  const handleAgingPageSizeChange = (pageSize: number) => {
    agingPagination.setPageSize(pageSize)
    setAgingPageSizePreference(pageSize)
    void loadAging(1, pageSize, agingSortKey)
  }

  const handleAgingSortChange = (sortKey: string) => {
    setAgingSortKey(sortKey)
    agingPagination.setPage(1)
    void loadAging(1, agingPagination.pageSize, sortKey)
  }

  const handleTopOutstandingCountChange = (value: number) => {
    setTopOutstandingCount(value)
  }

  const runExport = useCallback(
    async (
      kind: ReportExportKind,
      label: string,
      guards: (() => boolean)[],
      format: ReportExportFormat = 'Xlsx',
    ) => {
      if (!token) {
        setError('Vui lòng đăng nhập.')
        return
      }
      for (const guard of guards) {
        if (!guard()) return
      }
      setError(null)
      const actionKey = `export-${kind.toLowerCase()}-${format.toLowerCase()}`
      setLoadingAction(actionKey)
      const jobId = Date.now()
      const newJob: ExportJob = {
        id: jobId,
        state: 'running',
        startedAt: new Date().toISOString(),
        label,
      }
      setExportJobs((prev) => [newJob, ...prev].slice(0, 3))
      try {
        const result = await exportReport(
          token,
          {
            ...baseParams,
            filterText: filterText || undefined,
          },
          kind,
          format,
        )
        const url = window.URL.createObjectURL(result.blob)
        const link = document.createElement('a')
        link.href = url
        link.download = result.fileName
        document.body.appendChild(link)
        link.click()
        link.remove()
        // Keep object URL alive to avoid 0-byte files in browsers that defer save handling.
        setExportJobs((prev) =>
          prev.map((job) =>
            job.id === jobId ? { ...job, state: 'done', fileName: result.fileName } : job,
          ),
        )
      } catch (err) {
        if (err instanceof ApiError) {
          setError(err.message)
        } else {
          setError('Xuất báo cáo thất bại.')
        }
        setExportJobs((prev) =>
          prev.map((job) => (job.id === jobId ? { ...job, state: 'error' } : job)),
        )
      } finally {
        setLoadingAction('')
      }
    },
    [token, baseParams, filterText],
  )

  const handleExportFull = () =>
    runExport('Full', 'Xuất toàn bộ báo cáo', [() => ensureDateRange('xuất Excel')])
  const handleExportSummary = () =>
    runExport('Summary', 'Xuất báo cáo tổng hợp', [() => ensureDateRange('xuất Excel')])
  const handleExportSummaryPdf = () =>
    runExport(
      'Summary',
      'Xuất PDF tổng hợp',
      [() => ensureDateRange('xuất PDF tổng hợp')],
      'Pdf',
    )
  const handleExportStatement = () => {
    if (!customerTaxCode.trim()) {
      showConfirm(
        'Chưa chọn MST bên mua',
        'Hệ thống sẽ xuất sao kê tổng hợp theo khoảng thời gian đã chọn. Bạn có thể chọn MST bên mua để xuất riêng từng khách hàng.',
        () => {
          closeValidation()
          void runExport('Statement', 'Xuất sao kê khách hàng', [
            () => ensureDateRange('xuất sao kê'),
          ])
        },
        'Tiếp tục xuất',
      )
      return
    }

    void runExport('Statement', 'Xuất sao kê khách hàng', [
      () => ensureDateRange('xuất sao kê'),
    ])
  }
  const handleExportAging = () =>
    runExport('Aging', 'Xuất báo cáo tuổi nợ', [
      () => ensureDateRange('xuất tuổi nợ'),
      () => ensureAsOfDate('xuất tuổi nợ'),
    ])

  const handleMoveKpi = (key: string, direction: 'up' | 'down') => {
    setKpiOrder((prev) => {
      const current = prev.length ? [...prev] : [...defaultKpiOrder]
      const index = current.indexOf(key)
      if (index < 0) return current
      const nextIndex = direction === 'up' ? index - 1 : index + 1
      if (nextIndex < 0 || nextIndex >= current.length) return current
      const result = [...current]
      const [removed] = result.splice(index, 1)
      result.splice(nextIndex, 0, removed)
      return result
    })
  }

  const handleResetKpiOrder = () => setKpiOrder(defaultKpiOrder)
  const handleDueSoonDaysChange = (value: number) => setDueSoonDays(value)

  const filterChips = useMemo(() => {
    const chips = [
      from ? { label: 'Từ', value: formatDate(from) } : null,
      to ? { label: 'Đến', value: formatDate(to) } : null,
      asOfDate ? { label: 'Tính đến', value: formatDate(asOfDate) } : null,
      sellerTaxCode
        ? { label: 'Bên bán', value: resolveOptionLabel(sellerOptions, sellerTaxCode) }
        : null,
      customerTaxCode
        ? { label: 'Bên mua', value: resolveOptionLabel(customerOptions, customerTaxCode) }
        : null,
      ownerId ? { label: 'Phụ trách', value: resolveOptionLabel(ownerOptions, ownerId) } : null,
      filterText ? { label: 'Từ khóa', value: filterText } : null,
      { label: 'Nhóm', value: groupByLabels[groupBy] ?? groupBy },
    ].filter(Boolean) as { label: string; value: string }[]
    return chips
  }, [
    from,
    to,
    asOfDate,
    sellerTaxCode,
    customerTaxCode,
    ownerId,
    filterText,
    groupBy,
    sellerOptions,
    customerOptions,
    ownerOptions,
  ])

  const printTimestamp = useMemo(
    () =>
      new Intl.DateTimeFormat('vi-VN', {
        dateStyle: 'short',
        timeStyle: 'short',
      }).format(new Date()),
    [],
  )

  const printFilterSummary = useMemo(() => {
    if (filterChips.length === 0) return 'Không có bộ lọc.'
    return filterChips.map((chip) => `${chip.label}: ${chip.value}`).join(' | ')
  }, [filterChips])

  const statementCustomerName = useMemo(() => {
    const fromStatement = statement?.lines?.[0]?.customerName?.trim()
    if (fromStatement) return fromStatement
    if (!customerTaxCode) return ''
    const label = resolveOptionLabel(customerOptions, customerTaxCode)
    const parts = label.split(' - ')
    if (parts.length <= 1) return ''
    return parts.slice(1).join(' - ').trim()
  }, [statement, customerTaxCode, customerOptions])

  return (
    <div className="page-stack reports-page">
      <div className="page-header">
        <div>
          <h2>Tổng quan công nợ &amp; báo cáo chi tiết</h2>
          <p className="muted">
            Điều chỉnh bộ lọc, tải tổng quan và xuất báo cáo theo nhu cầu.
          </p>
        </div>
        <div className="header-actions">
          <button type="button" className="btn btn-ghost no-print" onClick={handlePrint}>
            In báo cáo
          </button>
          <a className="btn btn-ghost" href="#filters">
            Bộ lọc
          </a>
          <a className="btn btn-outline" href="#summary">
            Tổng hợp
          </a>
          <a className="btn btn-outline" href="#aging">
            Tuổi nợ
          </a>
        </div>
      </div>

      <section className="reports-print-header" aria-hidden="true">
        <div className="reports-print-header__brand">CongNo Golden</div>
        <h1>Báo cáo công nợ</h1>
        <p>Thời điểm in: {printTimestamp}</p>
        <p className="reports-print-header__filters">{printFilterSummary}</p>
      </section>

      <ReportsFilters
        filter={{
          from,
          to,
          asOfDate,
          sellerTaxCode,
          customerTaxCode,
          ownerId,
          groupBy,
          filterText,
        }}
        useCustomAsOf={useCustomAsOf}
        sellerOptions={sellerOptions}
        customerOptions={customerOptions}
        ownerOptions={ownerOptions}
        presets={presetOptions}
        filterChips={filterChips}
        loadingAction={loadingAction}
        onFromChange={handleFromChange}
        onToChange={handleToChange}
        onAsOfChange={handleAsOfChange}
        onToggleCustomAsOf={(nextValue) => {
          setUseCustomAsOf(nextValue)
          if (!nextValue) {
            setAsOfDate(to || '')
          } else if (!asOfDate && to) {
            setAsOfDate(to)
          }
        }}
        onSellerChange={handleSellerChange}
        onCustomerChange={handleCustomerChange}
        onOwnerChange={handleOwnerChange}
        onGroupByChange={handleGroupByChange}
        onFilterTextChange={handleFilterTextChange}
        onPresetSelect={applyPreset}
        onResetFilters={resetFilters}
        onLoadOverview={handleLoadOverview}
        onLoadSummary={handleSummary}
        onLoadStatement={handleStatement}
        onLoadAging={handleAging}
        onExport={handleExportFull}
      />

      {exportJobs.length > 0 && (
        <div className="export-queue">
          {exportJobs.map((job) => (
            <div className={`export-job export-job--${job.state}`} key={job.id} role="status">
              <div className="export-job__header">
                <div className="export-job__meta">
                  <span className="list-title">{job.label ?? 'Xuất báo cáo'}</span>
                  <span className="muted">
                    {job.state === 'done' ? job.fileName ?? 'File đã sẵn sàng' : 'Đang xử lý'}
                  </span>
                </div>
                <span
                  className={
                    job.state === 'done'
                      ? 'pill pill-ok'
                      : job.state === 'error'
                      ? 'pill pill-error'
                      : 'pill pill-info'
                  }
                >
                  {job.state === 'running'
                    ? 'Đang xuất'
                    : job.state === 'done'
                    ? 'Hoàn tất'
                    : 'Lỗi'}
                </span>
              </div>
              {job.state === 'running' && (
                <div className="progress progress--indeterminate">
                  <div className="progress__bar" />
                </div>
              )}
            </div>
          ))}
        </div>
      )}

      {error && (
        <div className="alert alert--error" role="alert" aria-live="assertive">
          {error}
        </div>
      )}

      <ReportsKpiSection
        sectionId="overview"
        kpis={kpis}
        kpiOrder={kpiOrder}
        dueSoonDays={dueSoonDays}
        onMoveKpi={handleMoveKpi}
        onResetKpiOrder={handleResetKpiOrder}
        onDueSoonDaysChange={handleDueSoonDaysChange}
      />

      <ReportsChartsSection charts={charts} loading={loadingAction === 'overview'} />

      <ReportsInsightsSection
        insights={insights}
        loading={loadingAction === 'overview'}
        topOutstandingCount={topOutstandingCount}
        topOutstandingOptions={TOP_OUTSTANDING_OPTIONS}
        onTopOutstandingCountChange={handleTopOutstandingCountChange}
      />

      <ReportsTablesSection
        summaryRows={summaryRows}
        statement={statement}
        agingRows={agingRows}
        summaryPagination={{
          page: summaryPagination.page,
          pageSize: summaryPagination.pageSize,
          total: summaryPagination.total,
        }}
        statementPagination={{
          page: statementPagination.page,
          pageSize: statementPagination.pageSize,
          total: statementPagination.total,
        }}
        agingPagination={{
          page: agingPagination.page,
          pageSize: agingPagination.pageSize,
          total: agingPagination.total,
        }}
        summarySortKey={summarySortKey}
        agingSortKey={agingSortKey}
        loadingSummary={loadingAction === 'summary'}
        loadingStatement={loadingAction === 'statement'}
        loadingAging={loadingAction === 'aging'}
        statementCustomerTaxCode={customerTaxCode || undefined}
        statementCustomerName={statementCustomerName || undefined}
        exportingSummary={loadingAction === 'export-summary-xlsx'}
        exportingSummaryPdf={loadingAction === 'export-summary-pdf'}
        exportingStatement={loadingAction === 'export-statement-xlsx'}
        exportingAging={loadingAction === 'export-aging-xlsx'}
        onExportSummary={handleExportSummary}
        onExportSummaryPdf={handleExportSummaryPdf}
        onExportStatement={handleExportStatement}
        onExportAging={handleExportAging}
        onSummaryPageChange={handleSummaryPageChange}
        onSummaryPageSizeChange={handleSummaryPageSizeChange}
        onSummarySortChange={handleSummarySortChange}
        onStatementPageChange={handleStatementPageChange}
        onStatementPageSizeChange={handleStatementPageSizeChange}
        onAgingPageChange={handleAgingPageChange}
        onAgingPageSizeChange={handleAgingPageSizeChange}
        onAgingSortChange={handleAgingSortChange}
      />
      <ReportsValidationModal
        open={Boolean(validationModal)}
        title={validationModal?.title ?? ''}
        message={validationModal?.message ?? ''}
        confirmLabel={validationModal?.confirmLabel}
        onConfirm={validationModal?.onConfirm}
        onClose={closeValidation}
      />
      <ReportsQuickActions
        open={quickActionsOpen}
        actions={quickActions}
        onToggle={() => setQuickActionsOpen((prev) => !prev)}
        onNavigate={scrollToSection}
      />
    </div>
  )
}
