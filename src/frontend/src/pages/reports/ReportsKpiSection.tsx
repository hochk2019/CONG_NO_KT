import type { ChangeEvent, MouseEvent } from 'react'
import type { ReportKpi } from '../../api/reports'
import { formatMoney } from '../../utils/format'

type KpiCard = {
  key: string
  label: string
  value: string
  meta: string
  tone?: 'danger' | 'default'
}

type ReportsKpiSectionProps = {
  sectionId?: string
  kpis: ReportKpi | null
  kpiOrder: string[]
  dueSoonDays: number
  onMoveKpi: (key: string, direction: 'up' | 'down') => void
  onResetKpiOrder: () => void
  onDueSoonDaysChange: (value: number) => void
}

const kpiLabels: Record<string, string> = {
  totalOutstanding: 'Tổng dư công nợ',
  outstandingInvoice: 'Dư hóa đơn',
  outstandingAdvance: 'Dư khoản trả hộ',
  unallocatedReceipts: 'Đã thu chưa phân bổ',
  overdueAmount: 'Quá hạn',
  dueSoonAmount: 'Sắp đến hạn',
  onTimeCustomers: 'KH trả đúng hạn',
}

const buildKpiCards = (kpis: ReportKpi | null, dueSoonDays: number): KpiCard[] => {
  const safe = kpis ?? {
    totalOutstanding: 0,
    outstandingInvoice: 0,
    outstandingAdvance: 0,
    unallocatedReceiptsAmount: 0,
    unallocatedReceiptsCount: 0,
    overdueAmount: 0,
    overdueCustomers: 0,
    dueSoonAmount: 0,
    dueSoonCustomers: 0,
    onTimeCustomers: 0,
  }

  return [
    {
      key: 'totalOutstanding',
      label: kpiLabels.totalOutstanding,
      value: formatMoney(safe.totalOutstanding),
      meta: 'Gồm hóa đơn + trả hộ',
    },
    {
      key: 'outstandingInvoice',
      label: kpiLabels.outstandingInvoice,
      value: formatMoney(safe.outstandingInvoice),
      meta: 'Chưa phân bổ hết',
    },
    {
      key: 'outstandingAdvance',
      label: kpiLabels.outstandingAdvance,
      value: formatMoney(safe.outstandingAdvance),
      meta: 'Khoản trả hộ còn lại',
    },
    {
      key: 'unallocatedReceipts',
      label: kpiLabels.unallocatedReceipts,
      value: formatMoney(safe.unallocatedReceiptsAmount),
      meta: `${safe.unallocatedReceiptsCount} phiếu thu treo`,
    },
    {
      key: 'overdueAmount',
      label: kpiLabels.overdueAmount,
      value: formatMoney(safe.overdueAmount),
      meta: `${safe.overdueCustomers} khách hàng quá hạn`,
      tone: safe.overdueAmount > 0 ? 'danger' : 'default',
    },
    {
      key: 'dueSoonAmount',
      label: `${kpiLabels.dueSoonAmount} (${dueSoonDays} ngày)`,
      value: formatMoney(safe.dueSoonAmount),
      meta: `${safe.dueSoonCustomers} khách hàng sắp đến hạn`,
    },
    {
      key: 'onTimeCustomers',
      label: kpiLabels.onTimeCustomers,
      value: safe.onTimeCustomers.toLocaleString('vi-VN'),
      meta: 'Dư còn ≤5% trong kỳ',
    },
  ]
}

export function ReportsKpiSection({
  sectionId,
  kpis,
  kpiOrder,
  dueSoonDays,
  onMoveKpi,
  onResetKpiOrder,
  onDueSoonDaysChange,
}: ReportsKpiSectionProps) {
  const kpiCards = buildKpiCards(kpis, dueSoonDays)
  const order = kpiOrder.length ? kpiOrder : kpiCards.map((card) => card.key)
  const orderedCards = order
    .map((key) => kpiCards.find((card) => card.key === key))
    .filter((card): card is KpiCard => Boolean(card))

  const handleMoveClick = (event: MouseEvent<HTMLButtonElement>) => {
    const key = event.currentTarget.dataset.key
    const direction = event.currentTarget.dataset.direction as 'up' | 'down' | undefined
    if (key && direction) {
      onMoveKpi(key, direction)
    }
  }

  const handleResetClick = () => {
    onResetKpiOrder()
  }

  const handleDueSoonDaysChange = (event: ChangeEvent<HTMLInputElement>) => {
    const nextValue = Number(event.target.value)
    if (Number.isNaN(nextValue)) {
      return
    }
    onDueSoonDaysChange(nextValue)
  }

  return (
    <section className="card reports-kpi" id={sectionId}>
      <div className="card-row">
        <div>
          <h3>Tổng quan chỉ số</h3>
          <p className="muted">Các KPI trọng yếu theo bộ lọc đang chọn.</p>
        </div>
      </div>
      <div className="stat-grid stat-grid--primary">
        {orderedCards.map((card) => (
          <div
            className={`stat-card${card.tone === 'danger' ? ' stat-card--danger' : ''}`}
            key={card.key}
          >
            <div className="stat-card__label">{card.label}</div>
            <div className="stat-card__value">{card.value}</div>
            <div className="stat-card__meta">{card.meta}</div>
          </div>
        ))}
      </div>
      <details className="advanced-panel">
        <summary>Tùy chỉnh KPI &amp; nhắc đến hạn</summary>
        <div className="advanced-panel__content">
          <div className="form-grid">
            <label className="field">
              <span title="Chọn từ 1–10 ngày để tính nhóm sắp đến hạn.">
                Sắp đến hạn (ngày)
              </span>
              <input
                type="number"
                min={1}
                max={10}
                value={dueSoonDays}
                onChange={handleDueSoonDaysChange}
              />
              <span className="muted">Áp dụng cho KPI sắp đến hạn và cảnh báo liên quan (1–10 ngày).</span>
            </label>
          </div>
          <div className="reports-kpi-order">
            {order.map((key, index) => (
              <div className="reports-kpi-order__row" key={key}>
                <span>{kpiLabels[key] ?? key}</span>
                <div className="reports-kpi-order__actions">
                  <button
                    className="btn btn-ghost btn-table"
                    type="button"
                    data-key={key}
                    data-direction="up"
                    onClick={handleMoveClick}
                    disabled={index === 0}
                  >
                    ↑
                  </button>
                  <button
                    className="btn btn-ghost btn-table"
                    type="button"
                    data-key={key}
                    data-direction="down"
                    onClick={handleMoveClick}
                    disabled={index === order.length - 1}
                  >
                    ↓
                  </button>
                </div>
              </div>
            ))}
          </div>
          <button className="btn btn-outline" type="button" onClick={handleResetClick}>
            Khôi phục thứ tự mặc định
          </button>
        </div>
      </details>
    </section>
  )
}
