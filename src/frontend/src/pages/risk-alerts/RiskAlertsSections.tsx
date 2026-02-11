import { useEffect, useState, type CSSProperties, type ReactNode } from 'react'
import DataTable from '../../components/DataTable'
import { formatDateTime, formatMoney } from '../../utils/format'
import type { NotificationItem } from '../../api/notifications'
import type { LookupOption } from '../../api/lookups'
import type { ReminderLogItem, ReminderRunResult, ReminderSettings } from '../../api/reminders'
import type { RiskCustomerItem, RiskOverview, RiskRule } from '../../api/risk'
import type { ZaloLinkCode, ZaloLinkStatus } from '../../api/zalo'

const RISK_SECTION_KEYS = {
  customers: 'pref.risk.section.customers',
  rules: 'pref.risk.section.rules',
  settings: 'pref.risk.section.settings',
  logs: 'pref.risk.section.logs',
  notifications: 'pref.risk.section.notifications',
} as const

const getStoredSectionState = (key: string, defaultOpen: boolean) => {
  if (typeof window === 'undefined') return defaultOpen
  const raw = window.localStorage.getItem(key)
  if (raw === null) return defaultOpen
  return raw === 'true'
}

const storeSectionState = (key: string, value: boolean) => {
  if (typeof window === 'undefined') return
  window.localStorage.setItem(key, String(value))
}

type CollapsibleSectionProps = {
  storageKey: string
  title: string
  description?: string
  actions?: ReactNode
  defaultExpanded?: boolean
  children: ReactNode
}

const buildSectionId = (key: string) =>
  `section-${key.replace(/[^a-z0-9]+/gi, '-')}`.replace(/-+/g, '-')

function CollapsibleSection({
  storageKey,
  title,
  description,
  actions,
  defaultExpanded = true,
  children,
}: CollapsibleSectionProps) {
  const [isOpen, setIsOpen] = useState(() => getStoredSectionState(storageKey, defaultExpanded))
  const contentId = buildSectionId(storageKey)

  useEffect(() => {
    storeSectionState(storageKey, isOpen)
  }, [storageKey, isOpen])

  return (
    <section className={`card collapsible-section ${isOpen ? 'is-open' : 'is-collapsed'}`}>
      <div className="card-row collapsible-header">
        <div>
          <h3>{title}</h3>
          {description && <p className="muted">{description}</p>}
        </div>
        <div className="collapsible-actions">
          {actions}
          <button
            className="collapsible-toggle"
            type="button"
            aria-expanded={isOpen}
            aria-controls={contentId}
            onClick={() => setIsOpen((prev) => !prev)}
          >
            <span className="collapsible-toggle__icon">{isOpen ? '▾' : '▸'}</span>
            <span>{isOpen ? 'Thu gọn' : 'Mở rộng'}</span>
          </button>
        </div>
      </div>
      <div id={contentId} className="collapsible-content" hidden={!isOpen}>
        {children}
      </div>
    </section>
  )
}

export type RiskSummaryCard = {
  level: string
  label: string
  customers: number
  totalOutstanding: number
  overdueAmount: number
}

export type RiskAlertsHeaderProps = {
  onSetToday: () => void
  onClearDate: () => void
}

export function RiskAlertsHeader({ onSetToday, onClearDate }: RiskAlertsHeaderProps) {
  return (
    <div className="page-header">
      <div>
        <h2>Rủi ro công nợ &amp; nhắc kế toán</h2>
        <p className="muted">
          Theo dõi nhóm rủi ro tự động, ngưỡng quá hạn và lịch nhắc kế toán mỗi tuần.
        </p>
      </div>
      <div className="header-actions">
        <button className="btn btn-outline" type="button" onClick={onSetToday}>
          Hôm nay
        </button>
        <button className="btn btn-ghost" type="button" onClick={onClearDate}>
          Xóa lọc
        </button>
      </div>
    </div>
  )
}

export type RiskOverviewSectionProps = {
  asOfDate: string
  onAsOfDateChange: (value: string) => void
  overviewLoading: boolean
  overviewError: string | null
  summaryCards: RiskSummaryCard[]
  overview: RiskOverview | null
}

export function RiskOverviewSection({
  asOfDate,
  onAsOfDateChange,
  overviewLoading,
  overviewError,
  summaryCards,
  overview,
}: RiskOverviewSectionProps) {
  return (
    <section className="card">
      <div className="card-row">
        <div>
          <h3>Tổng quan rủi ro</h3>
          <p className="muted">Phân bổ khách hàng theo nhóm rủi ro hiện tại.</p>
        </div>
        <label className="field">
          <span>Tính đến</span>
          <input
            type="date"
            value={asOfDate}
            onChange={(event) => onAsOfDateChange(event.target.value)}
          />
        </label>
      </div>
      {overviewLoading ? (
        <div className="empty-state">Đang tải tổng quan...</div>
      ) : overviewError ? (
        <div className="alert alert--error" role="alert" aria-live="assertive">
          {overviewError}
        </div>
      ) : (
        <div className="stat-grid">
          {summaryCards.map((card) => (
            <div className="stat-card" key={card.level}>
              <p className="stat-card__label">{card.label}</p>
              <h3>{card.customers}</h3>
              <span className="stat-card__meta">
                {formatMoney(card.overdueAmount)} quá hạn
              </span>
            </div>
          ))}
          {summaryCards.length === 0 && (
            <div className="empty-state">Chưa có dữ liệu rủi ro.</div>
          )}
        </div>
      )}
      {overview && (
        <div className="meta-row text-caption">
          <span>Tổng khách hàng: {overview.totalCustomers}</span>
          <span>Tổng dư nợ: {formatMoney(overview.totalOutstanding)}</span>
          <span>Quá hạn: {formatMoney(overview.totalOverdue)}</span>
        </div>
      )}
    </section>
  )
}

export type RiskCustomersSectionProps = {
  search: string
  ownerId: string
  level: string
  ownerOptions: LookupOption[]
  onSearchChange: (value: string) => void
  onOwnerChange: (value: string) => void
  onLevelChange: (value: string) => void
  customersLoading: boolean
  customersError: string | null
  customers: RiskCustomerItem[]
  customersTotal: number
  customerColumns: {
    key: string
    label: string
    render?: (row: RiskCustomerItem) => ReactNode
  }[]
  sort?: { key: string; direction: 'asc' | 'desc' }
  onSort: (next?: { key: string; direction: 'asc' | 'desc' }) => void
  page: number
  pageSize: number
  onPageChange: (next: number) => void
  onPageSizeChange: (next: number) => void
}

export function RiskCustomersSection({
  search,
  ownerId,
  level,
  ownerOptions,
  onSearchChange,
  onOwnerChange,
  onLevelChange,
  customersLoading,
  customersError,
  customers,
  customersTotal,
  customerColumns,
  sort,
  onSort,
  page,
  pageSize,
  onPageChange,
  onPageSizeChange,
}: RiskCustomersSectionProps) {
  return (
    <CollapsibleSection
      storageKey={RISK_SECTION_KEYS.customers}
      title="Danh sách cảnh báo"
      description="Lọc theo phụ trách, nhóm rủi ro hoặc từ khóa."
    >
      <div className="filters-grid">
        <label className="field">
          <span>Từ khóa</span>
          <input
            value={search}
            onChange={(event) => onSearchChange(event.target.value)}
            placeholder="Tên khách hàng, MST..."
          />
        </label>
        <label className="field">
          <span>Phụ trách</span>
          <select value={ownerId} onChange={(event) => onOwnerChange(event.target.value)}>
            <option value="">Tất cả</option>
            {ownerOptions.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </label>
        <label className="field">
          <span>Nhóm rủi ro</span>
          <select value={level} onChange={(event) => onLevelChange(event.target.value)}>
            <option value="">Tất cả</option>
            <option value="VERY_HIGH">Rất cao</option>
            <option value="HIGH">Cao</option>
            <option value="MEDIUM">Trung bình</option>
            <option value="LOW">Thấp</option>
          </select>
        </label>
      </div>
      {customersLoading ? (
        <div className="empty-state">Đang tải danh sách...</div>
      ) : customersError ? (
        <div className="alert alert--error" role="alert" aria-live="assertive">
          {customersError}
        </div>
      ) : (
        <DataTable
          columns={customerColumns}
          rows={customers}
          getRowKey={(row) => `${row.customerTaxCode}-${row.riskLevel}`}
          minWidth="1100px"
          sort={sort}
          onSort={onSort}
          pagination={{ page, pageSize, total: customersTotal }}
          onPageChange={onPageChange}
          onPageSizeChange={onPageSizeChange}
          emptyMessage="Chưa có khách hàng rủi ro."
        />
      )}
    </CollapsibleSection>
  )
}

export type RiskRulesSectionProps = {
  rulesDraft: RiskRule[]
  rulesError: string | null
  isRulesDirty: boolean
  rulesSaving: boolean
  canManage: boolean
  riskLabels: Record<string, string>
  toPercentInput: (value: number) => number
  onRuleChange: (index: number, key: keyof RiskRule, value: string | boolean) => void
  onSaveRules: () => void
}

export function RiskRulesSection({
  rulesDraft,
  rulesError,
  isRulesDirty,
  rulesSaving,
  canManage,
  riskLabels,
  toPercentInput,
  onRuleChange,
  onSaveRules,
}: RiskRulesSectionProps) {
  return (
    <CollapsibleSection
      storageKey={RISK_SECTION_KEYS.rules}
      title="Tiêu chí phân nhóm"
      description="Ngưỡng ngày quá hạn, tỷ lệ quá hạn và số lần trễ cho từng nhóm."
      actions={
        <div className="inline-actions inline-actions--tight">
          {isRulesDirty && <span className="text-caption">Có thay đổi chưa lưu</span>}
          {canManage && (
            <button className="btn btn-primary" onClick={onSaveRules} disabled={rulesSaving}>
              {rulesSaving ? 'Đang lưu...' : 'Lưu tiêu chí'}
            </button>
          )}
        </div>
      }
    >
      {rulesError && (
        <div className="alert alert--error" role="alert" aria-live="assertive">
          {rulesError}
        </div>
      )}
      <div className="table-scroll">
        <table
          className="table"
          style={{ '--table-columns': 5, '--table-min-width': '820px' } as CSSProperties}
        >
          <thead className="table-head">
            <tr className="table-row">
              <th scope="col">Nhóm</th>
              <th scope="col">Ngày quá hạn</th>
              <th scope="col">Tỷ lệ quá hạn (%)</th>
              <th scope="col">Số lần trễ</th>
              <th scope="col">Bật</th>
            </tr>
          </thead>
          <tbody>
            {rulesDraft.map((rule, index) => (
              <tr className="table-row" key={rule.level}>
                <td>{riskLabels[rule.level] ?? rule.level}</td>
                <td>
                  <input
                    type="number"
                    min={0}
                    value={rule.minOverdueDays}
                    disabled={!canManage}
                    onChange={(event) => onRuleChange(index, 'minOverdueDays', event.target.value)}
                  />
                </td>
                <td>
                  <input
                    type="number"
                    min={0}
                    max={100}
                    step="0.1"
                    value={toPercentInput(rule.minOverdueRatio)}
                    disabled={!canManage}
                    onChange={(event) => onRuleChange(index, 'minOverdueRatio', event.target.value)}
                  />
                </td>
                <td>
                  <input
                    type="number"
                    min={0}
                    value={rule.minLateCount}
                    disabled={!canManage}
                    onChange={(event) => onRuleChange(index, 'minLateCount', event.target.value)}
                  />
                </td>
                <td>
                  <label className="field-inline">
                    <input
                      type="checkbox"
                      checked={rule.isActive}
                      disabled={!canManage}
                      onChange={(event) => onRuleChange(index, 'isActive', event.target.checked)}
                    />
                    <span>{rule.isActive ? 'Bật' : 'Tắt'}</span>
                  </label>
                </td>
              </tr>
            ))}
            {rulesDraft.length === 0 && (
              <tr className="table-row">
                <td colSpan={5}>
                  <div className="empty-state">Chưa có tiêu chí.</div>
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </CollapsibleSection>
  )
}

export type RiskSettingsSectionProps = {
  settingsDraft: ReminderSettings | null
  settings: ReminderSettings | null
  settingsError: string | null
  settingsSaving: boolean
  runResult: ReminderRunResult | null
  canManage: boolean
  riskLabels: Record<string, string>
  channelLabels: Record<string, string>
  onToggleSettingList: (key: 'channels' | 'targetLevels', value: string) => void
  onSettingsDraftChange: (next: ReminderSettings) => void
  onSaveSettings: () => void
  onRunReminders: () => void
  zaloStatus: ZaloLinkStatus | null
  zaloCode: ZaloLinkCode | null
  zaloLoading: boolean
  zaloError: string | null
  onRequestZaloLink: () => void
  onReloadZaloStatus: () => void
}

export function RiskSettingsSection({
  settingsDraft,
  settings,
  settingsError,
  settingsSaving,
  runResult,
  canManage,
  riskLabels,
  channelLabels,
  onToggleSettingList,
  onSettingsDraftChange,
  onSaveSettings,
  onRunReminders,
  zaloStatus,
  zaloCode,
  zaloLoading,
  zaloError,
  onRequestZaloLink,
  onReloadZaloStatus,
}: RiskSettingsSectionProps) {
  return (
    <CollapsibleSection
      storageKey={RISK_SECTION_KEYS.settings}
      title="Thiết lập nhắc kế toán"
      description="Gửi nhắc mỗi 1 tuần qua in-app và Zalo."
      actions={
        canManage ? (
          <button className="btn btn-outline" type="button" onClick={onRunReminders}>
            Chạy nhắc ngay
          </button>
        ) : null
      }
    >
      {settingsError && (
        <div className="alert alert--error" role="alert" aria-live="assertive">
          {settingsError}
        </div>
      )}
      {settingsDraft ? (
        <div className="form-stack">
          <label className="field-inline">
            <input
              type="checkbox"
              checked={settingsDraft.enabled}
              disabled={!canManage}
              onChange={(event) =>
                onSettingsDraftChange({ ...settingsDraft, enabled: event.target.checked })
              }
            />
            <span>Bật nhắc tự động</span>
          </label>
          <label className="field">
            <span>Tần suất (ngày)</span>
            <input
              type="number"
              min={1}
              max={30}
              value={settingsDraft.frequencyDays}
              disabled={!canManage}
              onChange={(event) =>
                onSettingsDraftChange({
                  ...settingsDraft,
                  frequencyDays: Number(event.target.value) || 7,
                })
              }
            />
            <span className="muted">Mặc định: 7 ngày.</span>
          </label>
          <label className="field">
            <span>Nhắc sắp đến hạn (ngày)</span>
            <input
              type="number"
              min={1}
              max={30}
              value={settingsDraft.upcomingDueDays}
              disabled={!canManage}
              onChange={(event) =>
                onSettingsDraftChange({
                  ...settingsDraft,
                  upcomingDueDays: Number(event.target.value) || 7,
                })
              }
            />
            <span className="muted">Mặc định: 7 ngày.</span>
          </label>
          <div>
            <p className="text-caption">Kênh nhắc</p>
            <div className="field-inline">
              {['IN_APP', 'ZALO'].map((item) => (
                <label className="field-inline" key={item}>
                  <input
                    type="checkbox"
                    checked={settingsDraft.channels.includes(item)}
                    disabled={!canManage}
                    onChange={() => onToggleSettingList('channels', item)}
                  />
                  <span>{channelLabels[item]}</span>
                </label>
              ))}
            </div>
          </div>
          <div>
            <p className="text-caption">Nhắc cho nhóm</p>
            <div className="field-inline">
              {['VERY_HIGH', 'HIGH', 'MEDIUM', 'LOW'].map((item) => (
                <label className="field-inline" key={item}>
                  <input
                    type="checkbox"
                    checked={settingsDraft.targetLevels.includes(item)}
                    disabled={!canManage}
                    onChange={() => onToggleSettingList('targetLevels', item)}
                  />
                  <span>{riskLabels[item]}</span>
                </label>
              ))}
            </div>
          </div>
          <div className="zalo-link-panel">
            <div className="zalo-link-header">
              <div>
                <p className="text-caption">Liên kết Zalo OA</p>
                <p className="muted">
                  Gửi mã liên kết để nhận nhắc công nợ qua Zalo OA.
                </p>
              </div>
              <span className={`pill ${zaloStatus?.linked ? 'pill-ok' : 'pill-warn'}`}>
                {zaloStatus?.linked ? 'Đã liên kết' : 'Chưa liên kết'}
              </span>
            </div>
            {zaloStatus?.linked ? (
              <div className="zalo-link-meta text-caption">
                Zalo user_id: {zaloStatus.zaloUserId ?? '-'} • Liên kết lúc:{' '}
                {formatDateTime(zaloStatus.linkedAt)}
              </div>
            ) : (
              <div className="zalo-link-meta text-caption">
                Chưa có Zalo user_id. Hãy tạo mã và gửi cho OA để liên kết.
              </div>
            )}
            {zaloCode && !zaloStatus?.linked && (
              <div className="zalo-link-code">
                <span className="zalo-link-code__label">Mã liên kết</span>
                <div className="zalo-link-code__value">{zaloCode.code}</div>
                <span className="muted">
                  Hiệu lực đến {formatDateTime(zaloCode.expiresAt)}. Gửi tin:{' '}
                  <strong>LINK {zaloCode.code}</strong>
                </span>
              </div>
            )}
            <div className="inline-actions">
              <button
                className="btn btn-outline"
                type="button"
                onClick={onRequestZaloLink}
                disabled={zaloLoading || Boolean(zaloStatus?.linked)}
              >
                {zaloLoading ? 'Đang tạo...' : 'Tạo mã liên kết'}
              </button>
              <button
                className="btn btn-ghost"
                type="button"
                onClick={onReloadZaloStatus}
                disabled={zaloLoading}
              >
                Kiểm tra liên kết
              </button>
            </div>
            {zaloError && (
              <div className="alert alert--error" role="alert" aria-live="assertive">
                {zaloError}
              </div>
            )}
          </div>
          <div className="meta-row">
            <span>Lần chạy gần nhất: {formatDateTime(settings?.lastRunAt)}</span>
            <span>Lần kế tiếp: {formatDateTime(settings?.nextRunAt)}</span>
          </div>
          {runResult && (
            <div className="alert alert--info" role="status">
              Đã nhắc {runResult.sentCount}/{runResult.totalCandidates} khách hàng (
              {runResult.failedCount} lỗi, {runResult.skippedCount} bỏ qua).
            </div>
          )}
          {canManage && (
            <button
              className="btn btn-primary"
              type="button"
              onClick={onSaveSettings}
              disabled={settingsSaving}
            >
              {settingsSaving ? 'Đang lưu...' : 'Lưu thiết lập'}
            </button>
          )}
        </div>
      ) : (
        <div className="empty-state">Đang tải thiết lập...</div>
      )}
    </CollapsibleSection>
  )
}

export type RiskLogsSectionProps = {
  logChannel: string
  logStatus: string
  onLogChannelChange: (value: string) => void
  onLogStatusChange: (value: string) => void
  logsLoading: boolean
  logsError: string | null
  logs: ReminderLogItem[]
  logColumns: {
    key: string
    label: string
    render?: (row: ReminderLogItem) => ReactNode
  }[]
  logPage: number
  logPageSize: number
  logsTotal: number
  onLogPageChange: (next: number) => void
  onLogPageSizeChange: (next: number) => void
}

export function RiskLogsSection({
  logChannel,
  logStatus,
  onLogChannelChange,
  onLogStatusChange,
  logsLoading,
  logsError,
  logs,
  logColumns,
  logPage,
  logPageSize,
  logsTotal,
  onLogPageChange,
  onLogPageSizeChange,
}: RiskLogsSectionProps) {
  return (
    <CollapsibleSection
      storageKey={RISK_SECTION_KEYS.logs}
      title="Nhật ký nhắc"
      description="Theo dõi gửi nhắc theo kênh và trạng thái."
    >
      <div className="filters-grid">
        <label className="field">
          <span>Kênh</span>
          <select value={logChannel} onChange={(event) => onLogChannelChange(event.target.value)}>
            <option value="">Tất cả</option>
            <option value="IN_APP">In-app</option>
            <option value="ZALO">Zalo</option>
          </select>
        </label>
        <label className="field">
          <span>Trạng thái</span>
          <select
            value={logStatus}
            onChange={(event) => onLogStatusChange(event.target.value)}
          >
            <option value="">Tất cả</option>
            <option value="SENT">Đã gửi</option>
            <option value="FAILED">Lỗi</option>
            <option value="SKIPPED">Bỏ qua</option>
          </select>
        </label>
      </div>
      {logsLoading ? (
        <div className="empty-state">Đang tải nhật ký...</div>
      ) : logsError ? (
        <div className="alert alert--error" role="alert" aria-live="assertive">
          {logsError}
        </div>
      ) : (
        <DataTable
          columns={logColumns}
          rows={logs}
          getRowKey={(row) => row.id}
          minWidth="980px"
          pagination={{ page: logPage, pageSize: logPageSize, total: logsTotal }}
          onPageChange={onLogPageChange}
          onPageSizeChange={onLogPageSizeChange}
          emptyMessage="Chưa có nhật ký nhắc."
        />
      )}
    </CollapsibleSection>
  )
}

export type RiskNotificationsSectionProps = {
  notificationsLoading: boolean
  notifications: NotificationItem[]
  onMarkRead: (id: string) => void
}

export function RiskNotificationsSection({
  notificationsLoading,
  notifications,
  onMarkRead,
}: RiskNotificationsSectionProps) {
  return (
    <CollapsibleSection
      storageKey={RISK_SECTION_KEYS.notifications}
      title="Thông báo nội bộ"
      description="Nhắc chưa đọc dành cho bạn."
    >
      {notificationsLoading ? (
        <div className="empty-state">Đang tải thông báo...</div>
      ) : notifications.length === 0 ? (
        <div className="empty-state">Không có thông báo mới.</div>
      ) : (
        <div className="notification-list">
          {notifications.map((item) => (
            <div className="notification-item" key={item.id}>
              <div>
                <div className="list-title">{item.title}</div>
                <div className="muted">{item.body}</div>
              </div>
              <div className="notification-meta">
                <span className="muted">{formatDateTime(item.createdAt)}</span>
                <button
                  className="btn btn-ghost btn-table"
                  type="button"
                  onClick={() => onMarkRead(item.id)}
                >
                  Đã đọc
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </CollapsibleSection>
  )
}
