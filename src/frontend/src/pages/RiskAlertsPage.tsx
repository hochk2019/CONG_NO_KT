import { useEffect, useMemo, useState } from 'react'
import { ApiError } from '../api/client'
import {
  fetchRiskCustomers,
  fetchRiskOverview,
  fetchRiskRules,
  updateRiskRules,
  type RiskCustomerItem,
  type RiskRule,
  type RiskOverview,
} from '../api/risk'
import {
  fetchReminderLogs,
  fetchReminderSettings,
  runReminders,
  updateReminderSettings,
  type ReminderLogItem,
  type ReminderSettings,
  type ReminderRunResult,
} from '../api/reminders'
import {
  fetchNotifications,
  markNotificationRead,
  type NotificationItem,
} from '../api/notifications'
import {
  fetchZaloLinkStatus,
  requestZaloLinkCode,
  type ZaloLinkCode,
  type ZaloLinkStatus,
} from '../api/zalo'
import { fetchOwnerLookup, mapOwnerOptions, type LookupOption } from '../api/lookups'
import { useAuth } from '../context/AuthStore'
import { useDebouncedValue } from '../hooks/useDebouncedValue'
import { formatDateTime, formatMoney } from '../utils/format'
import {
  RiskAlertsHeader,
  RiskCustomersSection,
  RiskLogsSection,
  RiskNotificationsSection,
  RiskOverviewSection,
  RiskRulesSection,
  RiskSettingsSection,
} from './risk-alerts/RiskAlertsSections'

const DEFAULT_PAGE_SIZE = 10
const PAGE_SIZE_STORAGE_KEY = 'pref.table.pageSize'
const RISK_LOG_STATUS_KEY = 'pref.risk.logs.status'

const getStoredPageSize = () => {
  if (typeof window === 'undefined') return DEFAULT_PAGE_SIZE
  const raw = window.localStorage.getItem(PAGE_SIZE_STORAGE_KEY)
  const parsed = Number(raw)
  return Number.isFinite(parsed) && parsed > 0 ? parsed : DEFAULT_PAGE_SIZE
}

const storePageSize = (value: number) => {
  if (typeof window === 'undefined') return
  window.localStorage.setItem(PAGE_SIZE_STORAGE_KEY, String(value))
}

const getStoredFilter = (key: string) => {
  if (typeof window === 'undefined') return ''
  return window.localStorage.getItem(key) ?? ''
}

const storeFilter = (key: string, value: string) => {
  if (typeof window === 'undefined') return
  if (!value) {
    window.localStorage.removeItem(key)
  } else {
    window.localStorage.setItem(key, value)
  }
}

const riskLabels: Record<string, string> = {
  VERY_HIGH: 'Rất cao',
  HIGH: 'Cao',
  MEDIUM: 'Trung bình',
  LOW: 'Thấp',
}

const channelLabels: Record<string, string> = {
  IN_APP: 'In-app',
  ZALO: 'Zalo',
}

const statusLabels: Record<string, string> = {
  SENT: 'Đã gửi',
  FAILED: 'Lỗi',
  SKIPPED: 'Bỏ qua',
}

const toDateInput = (value: Date) => {
  const year = value.getFullYear()
  const month = String(value.getMonth() + 1).padStart(2, '0')
  const day = String(value.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

const resolveRiskPillClass = (level: string) => {
  switch (level) {
    case 'VERY_HIGH':
      return 'risk-pill risk-pill--very-high'
    case 'HIGH':
      return 'risk-pill risk-pill--high'
    case 'MEDIUM':
      return 'risk-pill risk-pill--medium'
    default:
      return 'risk-pill risk-pill--low'
  }
}

const formatRatio = (value: number) => {
  if (!Number.isFinite(value)) {
    return '-'
  }
  return `${Math.round(value * 1000) / 10}%`
}

const toPercentInput = (value: number) => {
  if (!Number.isFinite(value)) {
    return 0
  }
  return Math.round(value * 1000) / 10
}

export default function RiskAlertsPage() {
  const { state } = useAuth()
  const token = state.accessToken ?? ''
  const canManage = state.roles.includes('Admin') || state.roles.includes('Supervisor')

  const [overview, setOverview] = useState<RiskOverview | null>(null)
  const [overviewError, setOverviewError] = useState<string | null>(null)
  const [overviewLoading, setOverviewLoading] = useState(false)

  const [ownerOptions, setOwnerOptions] = useState<LookupOption[]>([])
  const [search, setSearch] = useState('')
  const [ownerId, setOwnerId] = useState('')
  const [level, setLevel] = useState('')
  const [asOfDate, setAsOfDate] = useState('')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(() => getStoredPageSize())
  const [sort, setSort] = useState<{ key: string; direction: 'asc' | 'desc' }>()
  const [customers, setCustomers] = useState<RiskCustomerItem[]>([])
  const [customersTotal, setCustomersTotal] = useState(0)
  const [customersError, setCustomersError] = useState<string | null>(null)
  const [customersLoading, setCustomersLoading] = useState(false)
  const debouncedSearch = useDebouncedValue(search, 400)

  const [rules, setRules] = useState<RiskRule[]>([])
  const [rulesDraft, setRulesDraft] = useState<RiskRule[]>([])
  const [rulesError, setRulesError] = useState<string | null>(null)
  const [rulesSaving, setRulesSaving] = useState(false)
  const isRulesDirty = useMemo(
    () => JSON.stringify(rules) !== JSON.stringify(rulesDraft),
    [rules, rulesDraft],
  )

  const [settings, setSettings] = useState<ReminderSettings | null>(null)
  const [settingsDraft, setSettingsDraft] = useState<ReminderSettings | null>(null)
  const [settingsError, setSettingsError] = useState<string | null>(null)
  const [settingsSaving, setSettingsSaving] = useState(false)
  const [runResult, setRunResult] = useState<ReminderRunResult | null>(null)

  const [logPage, setLogPage] = useState(1)
  const [logPageSize, setLogPageSize] = useState(() => getStoredPageSize())
  const [logChannel, setLogChannel] = useState('')
  const [logStatus, setLogStatus] = useState(() => getStoredFilter(RISK_LOG_STATUS_KEY))
  const [logs, setLogs] = useState<ReminderLogItem[]>([])
  const [logsTotal, setLogsTotal] = useState(0)
  const [logsLoading, setLogsLoading] = useState(false)
  const [logsError, setLogsError] = useState<string | null>(null)

  const [notifications, setNotifications] = useState<NotificationItem[]>([])
  const [notificationsLoading, setNotificationsLoading] = useState(false)

  const [zaloStatus, setZaloStatus] = useState<ZaloLinkStatus | null>(null)
  const [zaloCode, setZaloCode] = useState<ZaloLinkCode | null>(null)
  const [zaloLoading, setZaloLoading] = useState(false)
  const [zaloError, setZaloError] = useState<string | null>(null)

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
    setOverviewLoading(true)
    setOverviewError(null)

    const loadOverview = async () => {
      try {
        const result = await fetchRiskOverview({
          token,
          asOfDate: asOfDate || undefined,
        })
        if (!isActive) return
        setOverview(result)
      } catch (err) {
        if (!isActive) return
        if (err instanceof ApiError) {
          setOverviewError(err.message)
        } else {
          setOverviewError('Không tải được tổng quan rủi ro.')
        }
      } finally {
        if (isActive) setOverviewLoading(false)
      }
    }

    loadOverview()
    return () => {
      isActive = false
    }
  }, [token, asOfDate])

  useEffect(() => {
    if (!token) return
    let isActive = true
    setCustomersLoading(true)
    setCustomersError(null)

    const loadCustomers = async () => {
      try {
        const result = await fetchRiskCustomers({
          token,
          search: debouncedSearch || undefined,
          ownerId: ownerId || undefined,
          level: level || undefined,
          asOfDate: asOfDate || undefined,
          page,
          pageSize,
          sort: sort?.key,
          order: sort?.direction,
        })
        if (!isActive) return
        setCustomers(result.items)
        setCustomersTotal(result.total)
      } catch (err) {
        if (!isActive) return
        if (err instanceof ApiError) {
          setCustomersError(err.message)
        } else {
          setCustomersError('Không tải được danh sách rủi ro.')
        }
      } finally {
        if (isActive) setCustomersLoading(false)
      }
    }

    loadCustomers()
    return () => {
      isActive = false
    }
  }, [token, debouncedSearch, ownerId, level, asOfDate, page, pageSize, sort])

  useEffect(() => {
    if (!token) return
    let isActive = true

    const loadRules = async () => {
      try {
        const result = await fetchRiskRules(token)
        if (!isActive) return
        setRules(result)
        setRulesDraft(result)
      } catch (err) {
        if (!isActive) return
        if (err instanceof ApiError) {
          setRulesError(err.message)
        } else {
          setRulesError('Không tải được tiêu chí rủi ro.')
        }
      }
    }

    loadRules()
    return () => {
      isActive = false
    }
  }, [token])

  useEffect(() => {
    if (!token) return
    let isActive = true

    const loadSettings = async () => {
      try {
        const result = await fetchReminderSettings(token)
        if (!isActive) return
        setSettings(result)
        setSettingsDraft(result)
      } catch (err) {
        if (!isActive) return
        if (err instanceof ApiError) {
          setSettingsError(err.message)
        } else {
          setSettingsError('Không tải được thiết lập nhắc.')
        }
      }
    }

    loadSettings()
    return () => {
      isActive = false
    }
  }, [token])

  useEffect(() => {
    if (!token) return
    let isActive = true
    setLogsLoading(true)
    setLogsError(null)

    const loadLogs = async () => {
      try {
        const result = await fetchReminderLogs({
          token,
          channel: logChannel || undefined,
          status: logStatus || undefined,
          page: logPage,
          pageSize: logPageSize,
        })
        if (!isActive) return
        setLogs(result.items)
        setLogsTotal(result.total)
      } catch (err) {
        if (!isActive) return
        if (err instanceof ApiError) {
          setLogsError(err.message)
        } else {
          setLogsError('Không tải được nhật ký nhắc.')
        }
      } finally {
        if (isActive) setLogsLoading(false)
      }
    }

    loadLogs()
    return () => {
      isActive = false
    }
  }, [token, logChannel, logStatus, logPage, logPageSize])

  useEffect(() => {
    if (!token) return
    let isActive = true
    setNotificationsLoading(true)

    const loadNotifications = async () => {
      try {
        const result = await fetchNotifications({
          token,
          unreadOnly: true,
          page: 1,
          pageSize: 5,
        })
        if (!isActive) return
        setNotifications(result.items)
      } catch {
        if (!isActive) return
        setNotifications([])
      } finally {
        if (isActive) setNotificationsLoading(false)
      }
    }

    loadNotifications()
    return () => {
      isActive = false
    }
  }, [token])

  useEffect(() => {
    if (!token) return
    let isActive = true
    setZaloError(null)

    const loadZaloStatus = async () => {
      try {
        const result = await fetchZaloLinkStatus(token)
        if (!isActive) return
        setZaloStatus(result)
      } catch {
        if (!isActive) return
        setZaloStatus(null)
      }
    }

    loadZaloStatus()
    return () => {
      isActive = false
    }
  }, [token])

  const summaryCards = useMemo(() => {
    const items = overview?.items ?? []
    return items.map((item) => ({
      level: item.level,
      label: riskLabels[item.level] ?? item.level,
      customers: item.customers,
      totalOutstanding: item.totalOutstanding,
      overdueAmount: item.overdueAmount,
    }))
  }, [overview])

  const handleRuleChange = (index: number, key: keyof RiskRule, value: string | boolean) => {
    setRulesDraft((prev) =>
      prev.map((rule, idx) => {
        if (idx !== index) return rule
        if (key === 'isActive') {
          return { ...rule, isActive: Boolean(value) }
        }
        const numericValue = Number(value)
        if (key === 'minOverdueRatio') {
          const percentValue = Number.isFinite(numericValue) ? numericValue : 0
          const clamped = Math.min(Math.max(percentValue, 0), 100)
          return { ...rule, [key]: clamped / 100 }
        }
        return { ...rule, [key]: Number.isFinite(numericValue) ? numericValue : 0 }
      }),
    )
  }

  const handleSaveRules = async () => {
    if (!token) return
    setRulesError(null)
    setRulesSaving(true)
    try {
      await updateRiskRules(token, rulesDraft)
      setRules(rulesDraft)
    } catch (err) {
      if (err instanceof ApiError) {
        setRulesError(err.message)
      } else {
        setRulesError('Không lưu được tiêu chí rủi ro.')
      }
    } finally {
      setRulesSaving(false)
    }
  }

  const toggleSettingList = (key: 'channels' | 'targetLevels', value: string) => {
    if (!settingsDraft) return
    const next = new Set(settingsDraft[key])
    if (next.has(value)) {
      next.delete(value)
    } else {
      next.add(value)
    }
    setSettingsDraft({ ...settingsDraft, [key]: Array.from(next) })
  }

  const handleSaveSettings = async () => {
    if (!token || !settingsDraft) return
    setSettingsSaving(true)
    setSettingsError(null)
    try {
      await updateReminderSettings(token, {
        enabled: settingsDraft.enabled,
        frequencyDays: settingsDraft.frequencyDays,
        upcomingDueDays: settingsDraft.upcomingDueDays,
        channels: settingsDraft.channels,
        targetLevels: settingsDraft.targetLevels,
      })
      setSettings(settingsDraft)
    } catch (err) {
      if (err instanceof ApiError) {
        setSettingsError(err.message)
      } else {
        setSettingsError('Không lưu được thiết lập nhắc.')
      }
    } finally {
      setSettingsSaving(false)
    }
  }

  const handleRunReminders = async () => {
    if (!token) return
    setSettingsError(null)
    try {
      const result = await runReminders(token)
      setRunResult(result)
      const refresh = await fetchReminderSettings(token)
      setSettings(refresh)
      setSettingsDraft(refresh)
      setLogPage(1)
    } catch (err) {
      if (err instanceof ApiError) {
        setSettingsError(err.message)
      } else {
        setSettingsError('Không chạy được nhắc.')
      }
    }
  }

  const handleMarkRead = async (id: string) => {
    if (!token) return
    try {
      await markNotificationRead(token, id)
      setNotifications((prev) => prev.filter((item) => item.id !== id))
    } catch {
      // ignore
    }
  }

  const reloadZaloStatus = async () => {
    if (!token) return
    setZaloLoading(true)
    setZaloError(null)
    try {
      const result = await fetchZaloLinkStatus(token)
      setZaloStatus(result)
      if (result.linked) {
        setZaloCode(null)
      }
    } catch {
      setZaloError('Không tải được trạng thái liên kết Zalo.')
    } finally {
      setZaloLoading(false)
    }
  }

  const handleRequestZaloLink = async () => {
    if (!token) return
    setZaloLoading(true)
    setZaloError(null)
    try {
      const result = await requestZaloLinkCode(token)
      setZaloCode(result)
      await reloadZaloStatus()
    } catch (err) {
      if (err instanceof ApiError) {
        setZaloError(err.message)
      } else {
        setZaloError('Không tạo được mã liên kết Zalo.')
      }
    } finally {
      setZaloLoading(false)
    }
  }

  const customerColumns = [
    {
      key: 'customer',
      label: 'Khách hàng',
      render: (row: RiskCustomerItem) => (
        <div>
          <div className="list-title">{row.customerName}</div>
          <div className="muted">{row.customerTaxCode}</div>
        </div>
      ),
    },
    {
      key: 'owner',
      label: 'Phụ trách',
      render: (row: RiskCustomerItem) => row.ownerName ?? 'Chưa phân công',
    },
    {
      key: 'riskLevel',
      label: 'Nhóm rủi ro',
      sortable: true,
      render: (row: RiskCustomerItem) => (
        <span className={resolveRiskPillClass(row.riskLevel)}>
          {riskLabels[row.riskLevel] ?? row.riskLevel}
        </span>
      ),
    },
    {
      key: 'maxDaysPastDue',
      label: 'Ngày quá hạn',
      sortable: true,
      align: 'center' as const,
      render: (row: RiskCustomerItem) => `${row.maxDaysPastDue} ngày`,
    },
    {
      key: 'overdueRatio',
      label: 'Tỷ lệ quá hạn',
      sortable: true,
      align: 'center' as const,
      render: (row: RiskCustomerItem) => formatRatio(row.overdueRatio),
    },
    {
      key: 'overdueAmount',
      label: 'Giá trị quá hạn',
      sortable: true,
      align: 'center' as const,
      render: (row: RiskCustomerItem) => formatMoney(row.overdueAmount),
    },
    {
      key: 'totalOutstanding',
      label: 'Tổng dư nợ',
      sortable: true,
      align: 'center' as const,
      render: (row: RiskCustomerItem) => formatMoney(row.totalOutstanding),
    },
    {
      key: 'lateCount',
      label: 'Số lần trễ',
      sortable: true,
      align: 'center' as const,
      render: (row: RiskCustomerItem) => row.lateCount,
    },
  ]

  const logColumns = [
    {
      key: 'customer',
      label: 'Khách hàng',
      render: (row: ReminderLogItem) => (
        <div>
          <div className="list-title">{row.customerName}</div>
          <div className="muted">{row.customerTaxCode}</div>
        </div>
      ),
    },
    {
      key: 'owner',
      label: 'Phụ trách',
      render: (row: ReminderLogItem) => row.ownerName ?? 'Chưa phân công',
    },
    {
      key: 'riskLevel',
      label: 'Nhóm',
      render: (row: ReminderLogItem) => (
        <span className={resolveRiskPillClass(row.riskLevel)}>
          {riskLabels[row.riskLevel] ?? row.riskLevel}
        </span>
      ),
    },
    {
      key: 'channel',
      label: 'Kênh',
      render: (row: ReminderLogItem) => channelLabels[row.channel] ?? row.channel,
    },
    {
      key: 'status',
      label: 'Trạng thái',
      render: (row: ReminderLogItem) => (
        <span className={`pill ${row.status === 'FAILED' ? 'pill-error' : row.status === 'SENT' ? 'pill-ok' : 'pill-info'}`}>
          {statusLabels[row.status] ?? row.status}
        </span>
      ),
    },
    {
      key: 'sentAt',
      label: 'Thời gian',
      render: (row: ReminderLogItem) => formatDateTime(row.sentAt ?? row.createdAt),
    },
  ]

  return (
    <div className="page-stack">
      <RiskAlertsHeader
        onSetToday={() => setAsOfDate(toDateInput(new Date()))}
        onClearDate={() => setAsOfDate('')}
      />
      <RiskOverviewSection
        asOfDate={asOfDate}
        onAsOfDateChange={setAsOfDate}
        overviewLoading={overviewLoading}
        overviewError={overviewError}
        summaryCards={summaryCards}
        overview={overview}
      />
      <RiskCustomersSection
        search={search}
        ownerId={ownerId}
        level={level}
        ownerOptions={ownerOptions}
        onSearchChange={(value) => {
          setSearch(value)
          setPage(1)
        }}
        onOwnerChange={(value) => {
          setOwnerId(value)
          setPage(1)
        }}
        onLevelChange={(value) => {
          setLevel(value)
          setPage(1)
        }}
        customersLoading={customersLoading}
        customersError={customersError}
        customers={customers}
        customersTotal={customersTotal}
        customerColumns={customerColumns}
        sort={sort}
        onSort={(next) => {
          setSort(next)
          setPage(1)
        }}
        page={page}
        pageSize={pageSize}
        onPageChange={setPage}
        onPageSizeChange={(size) => {
          storePageSize(size)
          setPageSize(size)
          setPage(1)
        }}
      />
      <RiskRulesSection
        rulesDraft={rulesDraft}
        rulesError={rulesError}
        isRulesDirty={isRulesDirty}
        rulesSaving={rulesSaving}
        canManage={canManage}
        riskLabels={riskLabels}
        toPercentInput={toPercentInput}
        onRuleChange={handleRuleChange}
        onSaveRules={handleSaveRules}
      />
      <div className="grid-split">
        <RiskSettingsSection
          settingsDraft={settingsDraft}
          settings={settings}
          settingsError={settingsError}
          settingsSaving={settingsSaving}
          runResult={runResult}
          canManage={canManage}
          riskLabels={riskLabels}
          channelLabels={channelLabels}
          onToggleSettingList={toggleSettingList}
          onSettingsDraftChange={(next) => setSettingsDraft(next)}
          onSaveSettings={handleSaveSettings}
          onRunReminders={handleRunReminders}
          zaloStatus={zaloStatus}
          zaloCode={zaloCode}
          zaloLoading={zaloLoading}
          zaloError={zaloError}
          onRequestZaloLink={handleRequestZaloLink}
          onReloadZaloStatus={reloadZaloStatus}
        />
        <RiskLogsSection
          logChannel={logChannel}
          logStatus={logStatus}
          onLogChannelChange={setLogChannel}
          onLogStatusChange={(value) => {
            setLogStatus(value)
            storeFilter(RISK_LOG_STATUS_KEY, value)
          }}
          logsLoading={logsLoading}
          logsError={logsError}
          logs={logs}
          logColumns={logColumns}
          logPage={logPage}
          logPageSize={logPageSize}
          logsTotal={logsTotal}
          onLogPageChange={setLogPage}
          onLogPageSizeChange={(size) => {
            storePageSize(size)
            setLogPageSize(size)
            setLogPage(1)
          }}
        />
      </div>
      <RiskNotificationsSection
        notificationsLoading={notificationsLoading}
        notifications={notifications}
        onMarkRead={handleMarkRead}
      />
    </div>
  )
}
