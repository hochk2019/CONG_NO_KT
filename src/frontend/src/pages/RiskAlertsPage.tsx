import { useEffect, useMemo, useRef, useState } from 'react'
import { ApiError } from '../api/client'
import {
  fetchRiskBootstrap,
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
import {
  channelLabels,
  riskLabels,
  toDateInput,
  toPercentInput,
} from './risk-alerts/riskAlertsUtils'
import { buildReminderLogColumns, buildRiskCustomerColumns } from './risk-alerts/riskAlertColumns'
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
const RISK_ACTIVE_TAB_KEY = 'pref.risk.activeTab'

type RiskTabKey = 'overview' | 'config' | 'history'

const riskTabs: { key: RiskTabKey; label: string }[] = [
  { key: 'overview', label: 'Overview' },
  { key: 'config', label: 'Config' },
  { key: 'history', label: 'History' },
]

const toRiskTabKey = (value: string | null): RiskTabKey => {
  if (value === 'config' || value === 'history') return value
  return 'overview'
}

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

export default function RiskAlertsPage() {
  const { state } = useAuth()
  const token = state.accessToken ?? ''
  const canManage = state.roles.includes('Admin') || state.roles.includes('Supervisor')
  const [activeTab, setActiveTab] = useState<RiskTabKey>(() => {
    if (typeof window === 'undefined') return 'overview'
    return toRiskTabKey(window.localStorage.getItem(RISK_ACTIVE_TAB_KEY))
  })

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
  const [bootstrapPending, setBootstrapPending] = useState(false)
  const skipOverviewLoadRef = useRef(false)
  const skipCustomersLoadRef = useRef(false)
  const skipRulesLoadRef = useRef(false)
  const skipSettingsLoadRef = useRef(false)
  const skipLogsLoadRef = useRef(false)
  const skipNotificationsLoadRef = useRef(false)
  const skipZaloLoadRef = useRef(false)
  const bootstrapInFlightRef = useRef(false)
  const bootstrapTokenRef = useRef<string | null>(null)

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
    if (!token) {
      bootstrapInFlightRef.current = false
      setBootstrapPending(false)
      bootstrapTokenRef.current = null
      return
    }
    if (bootstrapInFlightRef.current) return
    if (bootstrapTokenRef.current === token) return

    let isActive = true
    bootstrapInFlightRef.current = true
    setBootstrapPending(true)
    setOverviewLoading(true)
    setCustomersLoading(true)
    setLogsLoading(true)
    setNotificationsLoading(true)

    const loadBootstrap = async () => {
      try {
        const result = await fetchRiskBootstrap({
          token,
          search: debouncedSearch || undefined,
          ownerId: ownerId || undefined,
          level: level || undefined,
          asOfDate: asOfDate || undefined,
          page,
          pageSize,
          sort: sort?.key,
          order: sort?.direction,
          logChannel: logChannel || undefined,
          logStatus: logStatus || undefined,
          logPage,
          logPageSize,
          notificationPage: 1,
          notificationPageSize: 5,
        })

        if (!isActive) return

        setOverview(result.overview)
        setCustomers(result.customers.items)
        setCustomersTotal(result.customers.total)
        setRules(result.rules)
        setRulesDraft(result.rules)
        setSettings(result.settings)
        setSettingsDraft(result.settings)
        setLogs(result.logs.items)
        setLogsTotal(result.logs.total)
        setNotifications(result.notifications.items)
        setZaloStatus(result.zaloStatus)
        setOverviewError(null)
        setCustomersError(null)
        setRulesError(null)
        setSettingsError(null)
        setLogsError(null)
        setZaloError(null)
        skipOverviewLoadRef.current = true
        skipCustomersLoadRef.current = true
        skipRulesLoadRef.current = true
        skipSettingsLoadRef.current = true
        skipLogsLoadRef.current = true
        skipNotificationsLoadRef.current = true
        skipZaloLoadRef.current = true
      } catch {
        if (!isActive) return
        // Let individual effects load data as fallback.
      } finally {
        if (isActive) {
          bootstrapTokenRef.current = token
          setOverviewLoading(false)
          setCustomersLoading(false)
          setLogsLoading(false)
          setNotificationsLoading(false)
          bootstrapInFlightRef.current = false
          setBootstrapPending(false)
        }
      }
    }

    void loadBootstrap()
    return () => {
      isActive = false
      bootstrapInFlightRef.current = false
    }
  }, [
    token,
    debouncedSearch,
    ownerId,
    level,
    asOfDate,
    page,
    pageSize,
    sort,
    logChannel,
    logStatus,
    logPage,
    logPageSize,
  ])

  useEffect(() => {
    if (!token) return
    if (bootstrapPending) return
    if (bootstrapTokenRef.current !== token) return
    if (skipOverviewLoadRef.current) {
      skipOverviewLoadRef.current = false
      return
    }
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
  }, [token, asOfDate, bootstrapPending])

  useEffect(() => {
    if (!token) return
    if (bootstrapPending) return
    if (bootstrapTokenRef.current !== token) return
    if (skipCustomersLoadRef.current) {
      skipCustomersLoadRef.current = false
      return
    }
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
  }, [token, debouncedSearch, ownerId, level, asOfDate, page, pageSize, sort, bootstrapPending])

  useEffect(() => {
    if (!token) return
    if (bootstrapPending) return
    if (bootstrapTokenRef.current !== token) return
    if (skipRulesLoadRef.current) {
      skipRulesLoadRef.current = false
      return
    }
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
  }, [token, bootstrapPending])

  useEffect(() => {
    if (!token) return
    if (bootstrapPending) return
    if (bootstrapTokenRef.current !== token) return
    if (skipSettingsLoadRef.current) {
      skipSettingsLoadRef.current = false
      return
    }
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
  }, [token, bootstrapPending])

  useEffect(() => {
    if (!token) return
    if (bootstrapPending) return
    if (bootstrapTokenRef.current !== token) return
    if (skipLogsLoadRef.current) {
      skipLogsLoadRef.current = false
      return
    }
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
  }, [token, logChannel, logStatus, logPage, logPageSize, bootstrapPending])

  useEffect(() => {
    if (!token) return
    if (bootstrapPending) return
    if (bootstrapTokenRef.current !== token) return
    if (skipNotificationsLoadRef.current) {
      skipNotificationsLoadRef.current = false
      return
    }
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
  }, [token, bootstrapPending])

  useEffect(() => {
    if (!token) return
    if (bootstrapPending) return
    if (bootstrapTokenRef.current !== token) return
    if (skipZaloLoadRef.current) {
      skipZaloLoadRef.current = false
      return
    }
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
  }, [token, bootstrapPending])

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
        if (key === 'matchMode') {
          return { ...rule, matchMode: String(value).toUpperCase() === 'ALL' ? 'ALL' : 'ANY' }
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
        escalationMaxAttempts: settingsDraft.escalationMaxAttempts,
        escalationCooldownHours: settingsDraft.escalationCooldownHours,
        escalateToSupervisorAfter: settingsDraft.escalateToSupervisorAfter,
        escalateToAdminAfter: settingsDraft.escalateToAdminAfter,
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

  const customerColumns = buildRiskCustomerColumns()
  const logColumns = buildReminderLogColumns()

  useEffect(() => {
    if (typeof window === 'undefined') return
    window.localStorage.setItem(RISK_ACTIVE_TAB_KEY, activeTab)
  }, [activeTab])

  return (
    <div className="page-stack">
      <RiskAlertsHeader
        onSetToday={() => setAsOfDate(toDateInput(new Date()))}
        onClearDate={() => setAsOfDate('')}
      />
      <div className="tab-row" role="tablist" aria-label="Risk page tabs">
        {riskTabs.map((tab) => (
          <button
            key={tab.key}
            id={`risk-tab-${tab.key}`}
            type="button"
            role="tab"
            className={`tab ${activeTab === tab.key ? 'tab--active' : ''}`}
            aria-selected={activeTab === tab.key}
            aria-controls={`risk-panel-${tab.key}`}
            onClick={() => setActiveTab(tab.key)}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {activeTab === 'overview' && (
        <section id="risk-panel-overview" role="tabpanel" aria-labelledby="risk-tab-overview">
          <div className="page-stack">
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
          </div>
        </section>
      )}

      {activeTab === 'config' && (
        <section id="risk-panel-config" role="tabpanel" aria-labelledby="risk-tab-config">
          <div className="page-stack">
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
          </div>
        </section>
      )}

      {activeTab === 'history' && (
        <section id="risk-panel-history" role="tabpanel" aria-labelledby="risk-tab-history">
          <div className="page-stack">
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
            <RiskNotificationsSection
              notificationsLoading={notificationsLoading}
              notifications={notifications}
              onMarkRead={handleMarkRead}
            />
          </div>
        </section>
      )}
    </div>
  )
}
