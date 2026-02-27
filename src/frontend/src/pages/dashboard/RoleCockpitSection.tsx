import { Link } from 'react-router-dom'
import type { DashboardKpis, DashboardTopItem } from '../../api/dashboard'
import { formatMoney } from '../../utils/format'

export type DashboardRoleView = 'director' | 'manager' | 'operator'

type RoleCockpitSectionProps = {
  roleView: DashboardRoleView
  kpis: DashboardKpis | undefined
  topOverdue: DashboardTopItem[]
}

type CockpitStatus = 'good' | 'warning' | 'critical'

type CockpitDecision = {
  id: string
  title: string
  value: string
  detail: string
  status: CockpitStatus
  ctaLabel: string
  to: string
}

type CockpitStep = {
  id: string
  title: string
  detail: string
  to: string
  ctaLabel: string
}

const roleMeta: Record<DashboardRoleView, { label: string; summary: string }> = {
  director: {
    label: 'Giám đốc',
    summary: 'Tập trung vào quyết định và mức độ rủi ro tổng quan.',
  },
  manager: {
    label: 'Quản lý',
    summary: 'Tập trung vào backlog thu hồi và điều phối xử lý.',
  },
  operator: {
    label: 'Người dùng',
    summary: 'Tập trung vào thao tác hằng ngày và cập nhật dữ liệu đúng hạn.',
  },
}

const resolveUnallocatedStatus = (amount: number, count: number): CockpitStatus => {
  if (amount >= 500_000_000 || count >= 25) return 'critical'
  if (amount > 0 || count > 0) return 'warning'
  return 'good'
}

const resolvePendingStatus = (
  pendingReceiptsCount: number,
  pendingImportBatches: number,
): CockpitStatus => {
  if (pendingReceiptsCount >= 30 || pendingImportBatches >= 8) return 'critical'
  if (pendingReceiptsCount > 0 || pendingImportBatches > 0) return 'warning'
  return 'good'
}

const buildRoleSteps = (roleView: DashboardRoleView): CockpitStep[] => {
  if (roleView === 'director') {
    return [
      {
        id: 'director-risk',
        title: 'Xác nhận nhóm rủi ro cao',
        detail: 'Review danh sách HIGH/CRITICAL và xác định mức độ ưu tiên theo tuần.',
        to: '/risk',
        ctaLabel: 'Mở cảnh báo',
      },
      {
        id: 'director-allocation',
        title: 'Khóa hành động giảm tồn',
        detail: 'Đảm bảo phiếu thu treo được xử lý để giảm nợ treo thực tế.',
        to: '/receipts',
        ctaLabel: 'Mở thu tiền',
      },
      {
        id: 'director-report',
        title: 'Theo dõi hiệu quả thu hồi',
        detail: 'Kiểm tra tỷ lệ Actual/Expected và chênh lệch để điều chỉnh kế hoạch.',
        to: '/reports',
        ctaLabel: 'Mở báo cáo',
      },
    ]
  }

  if (roleView === 'manager') {
    return [
      {
        id: 'manager-risk',
        title: 'Phân loại danh sách quá hạn',
        detail: 'Chốt danh sách cần xử lý trong ngày cho nhóm overdue.',
        to: '/risk',
        ctaLabel: 'Mở danh sách',
      },
      {
        id: 'manager-receipts',
        title: 'Giải quyết backlog phân bổ',
        detail: 'Ưu tiên phiếu thu treo để thu hẹp khoản chưa đối soát.',
        to: '/receipts',
        ctaLabel: 'Xử lý phiếu thu',
      },
      {
        id: 'manager-import',
        title: 'Đồng bộ dữ liệu nhập liệu',
        detail: 'Theo dõi batch nhập và xử lý lỗi trong ngày.',
        to: '/imports',
        ctaLabel: 'Mở nhập liệu',
      },
    ]
  }

  return [
    {
      id: 'operator-import',
      title: 'Cập nhật dữ liệu phát sinh',
      detail: 'Nhập dữ liệu hóa đơn/trả hộ đúng ngày để tránh tồn đọng.',
      to: '/imports',
      ctaLabel: 'Mở import',
    },
    {
      id: 'operator-receipts',
      title: 'Phân bổ phiếu thu cho đúng chứng từ',
      detail: 'Xử lý nhanh danh sách phiếu thu chưa phân bổ.',
      to: '/receipts',
      ctaLabel: 'Mở phiếu thu',
    },
    {
      id: 'operator-customers',
      title: 'Theo dõi khách hàng cần nhắc',
      detail: 'Kiểm tra công nợ và lịch sử giao dịch trên hồ sơ khách hàng.',
      to: '/customers',
      ctaLabel: 'Mở khách hàng',
    },
  ]
}

const statusLabel: Record<CockpitStatus, string> = {
  good: 'Đúng tiến độ',
  warning: 'Cần theo dõi',
  critical: 'Cần xử lý ngay',
}

export default function RoleCockpitSection({
  roleView,
  kpis,
  topOverdue,
}: RoleCockpitSectionProps) {
  const resolvedKpis = kpis ?? {
    totalOutstanding: 0,
    outstandingInvoice: 0,
    outstandingAdvance: 0,
    overdueTotal: 0,
    overdueCustomers: 0,
    onTimeCustomers: 0,
    unallocatedReceiptsAmount: 0,
    unallocatedReceiptsCount: 0,
    pendingReceiptsCount: 0,
    pendingReceiptsAmount: 0,
    pendingAdvancesCount: 0,
    pendingAdvancesAmount: 0,
    pendingImportBatches: 0,
    lockedPeriodsCount: 0,
  }

  const decisions: CockpitDecision[] = [
    {
      id: 'overdue',
      title: 'Áp lực công nợ quá hạn',
      value: formatMoney(resolvedKpis.overdueTotal),
      detail:
        resolvedKpis.overdueCustomers > 0
          ? `${resolvedKpis.overdueCustomers} khách hàng đang quá hạn.`
          : 'Không có khách hàng quá hạn.',
      status: resolvedKpis.overdueTotal > 0 ? 'critical' : 'good',
      ctaLabel: 'Xử lý quá hạn',
      to: '/risk',
    },
    {
      id: 'unallocated',
      title: 'Phiếu thu chưa phân bổ',
      value: formatMoney(resolvedKpis.unallocatedReceiptsAmount),
      detail: `${resolvedKpis.unallocatedReceiptsCount} phiếu thu đang chờ đối soát.`,
      status: resolveUnallocatedStatus(
        resolvedKpis.unallocatedReceiptsAmount,
        resolvedKpis.unallocatedReceiptsCount,
      ),
      ctaLabel: 'Mở phân bổ',
      to: '/receipts',
    },
    {
      id: 'pending',
      title: 'Hàng đợi xử lý trong ngày',
      value: `${resolvedKpis.pendingReceiptsCount + resolvedKpis.pendingImportBatches}`,
      detail: `${resolvedKpis.pendingReceiptsCount} phiếu thu pending, ${resolvedKpis.pendingImportBatches} batch import chờ commit.`,
      status: resolvePendingStatus(
        resolvedKpis.pendingReceiptsCount,
        resolvedKpis.pendingImportBatches,
      ),
      ctaLabel: 'Mở hàng đợi',
      to: '/imports',
    },
  ]

  const overdueHighlight = topOverdue[0]
  const steps = buildRoleSteps(roleView)

  return (
    <section className="card role-cockpit" aria-labelledby="role-cockpit-title">
      <div className="role-cockpit__header">
        <div>
          <h3 id="role-cockpit-title">Cockpit theo vai trò</h3>
          <p className="muted">{roleMeta[roleView].summary}</p>
        </div>
        <span className="role-cockpit__badge">Góc nhìn: {roleMeta[roleView].label}</span>
      </div>

      <div className="role-cockpit__decision-grid">
        {decisions.map((decision) => (
          <article
            key={decision.id}
            className={`role-cockpit__decision role-cockpit__decision--${decision.status}`}
          >
            <div className="role-cockpit__decision-head">
              <h4>{decision.title}</h4>
              <span className="role-cockpit__status">{statusLabel[decision.status]}</span>
            </div>
            <p className="role-cockpit__value">{decision.value}</p>
            <p className="role-cockpit__detail">{decision.detail}</p>
            <Link to={decision.to} className="btn btn-ghost role-cockpit__cta">
              {decision.ctaLabel}
            </Link>
          </article>
        ))}
      </div>

      <div className="role-cockpit__workflow">
        <section className="role-cockpit__steps">
          <h4>Luồng thao tác ưu tiên</h4>
          <ol>
            {steps.map((step) => (
              <li key={step.id}>
                <div>
                  <strong>{step.title}</strong>
                  <p className="muted">{step.detail}</p>
                </div>
                <Link to={step.to} className="btn btn-link">
                  {step.ctaLabel}
                </Link>
              </li>
            ))}
          </ol>
        </section>

        <section className="role-cockpit__highlight">
          <h4>Điểm nóng hiện tại</h4>
          {overdueHighlight ? (
            <div className="role-cockpit__highlight-card">
              <p className="role-cockpit__highlight-title">{overdueHighlight.customerName}</p>
              <p className="muted">{overdueHighlight.customerTaxCode}</p>
              <p className="role-cockpit__value">{formatMoney(overdueHighlight.amount)}</p>
              <Link to="/risk" className="btn btn-ghost">
                Mở chi tiết rủi ro
              </Link>
            </div>
          ) : (
            <div className="empty-state">Chưa có điểm nóng quá hạn.</div>
          )}
        </section>
      </div>
    </section>
  )
}
