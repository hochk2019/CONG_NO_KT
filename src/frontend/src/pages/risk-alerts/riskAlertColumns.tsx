import type { ReminderLogItem } from '../../api/reminders'
import type { RiskCustomerItem } from '../../api/risk'
import { formatDateTime, formatMoney } from '../../utils/format'
import {
  aiSignalLabels,
  channelLabels,
  formatRatio,
  resolveAiSignalPillClass,
  resolveRiskPillClass,
  riskLabels,
  statusLabels,
} from './riskAlertsUtils'

const topAiFactors = (row: RiskCustomerItem) => {
  return [...row.aiFactors]
    .sort((left, right) => Math.abs(right.contribution) - Math.abs(left.contribution))
    .slice(0, 2)
}

export const buildRiskCustomerColumns = () => [
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
    key: 'aiSignal',
    label: 'AI dự báo',
    render: (row: RiskCustomerItem) => (
      <div>
        <span className={resolveAiSignalPillClass(row.aiSignal)}>
          {aiSignalLabels[row.aiSignal] ?? row.aiSignal}
        </span>
        <div className="muted">{formatRatio(row.predictedOverdueProbability)}</div>
        {topAiFactors(row).map((factor) => (
          <div className="text-caption muted" key={factor.code}>
            {factor.label}: {formatRatio(factor.contribution)}
          </div>
        ))}
        <div className="text-caption">{row.aiRecommendation}</div>
      </div>
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

export const buildReminderLogColumns = () => [
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
      <span
        className={`pill ${row.status === 'FAILED' ? 'pill-error' : row.status === 'SENT' ? 'pill-ok' : 'pill-info'}`}
      >
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
