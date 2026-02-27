import { useEffect, useMemo, useState } from 'react'
import { ApiError } from '../../api/client'
import { type Customer360, fetchCustomer360 } from '../../api/customers'

type Customer360SectionProps = {
  token: string
  selectedTaxCode: string | null
  selectedName: string
}

const currencyFormatter = new Intl.NumberFormat('vi-VN', {
  style: 'currency',
  currency: 'VND',
  maximumFractionDigits: 0,
})

const percentFormatter = new Intl.NumberFormat('vi-VN', {
  style: 'percent',
  minimumFractionDigits: 1,
  maximumFractionDigits: 1,
})

const datetimeFormatter = new Intl.DateTimeFormat('vi-VN', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
})

const dateFormatter = new Intl.DateTimeFormat('vi-VN', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
})

const formatDate = (value?: string | null) => {
  if (!value) return '--'
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? '--' : dateFormatter.format(parsed)
}

const formatDateTime = (value?: string | null) => {
  if (!value) return '--'
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? '--' : datetimeFormatter.format(parsed)
}

const formatScore = (value?: number | null) => {
  if (value === null || value === undefined) return '--'
  return value.toFixed(4)
}

export default function Customer360Section({
  token,
  selectedTaxCode,
  selectedName,
}: Customer360SectionProps) {
  const [data, setData] = useState<Customer360 | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!token || !selectedTaxCode) {
      setData(null)
      setLoading(false)
      setError(null)
      return
    }

    let isActive = true
    const load = async () => {
      setLoading(true)
      setError(null)
      try {
        const result = await fetchCustomer360(token, selectedTaxCode)
        if (!isActive) return
        setData(result)
      } catch (err) {
        if (!isActive) return
        if (err instanceof ApiError) {
          setError(err.message)
        } else {
          setError('Không tải được hồ sơ Customer 360.')
        }
      } finally {
        if (isActive) {
          setLoading(false)
        }
      }
    }

    load()
    return () => {
      isActive = false
    }
  }, [token, selectedTaxCode])

  const summaryItems = useMemo(
    () =>
      data
        ? [
            { label: 'Tổng công nợ mở', value: currencyFormatter.format(data.summary.totalOutstanding) },
            { label: 'Nợ quá hạn', value: currencyFormatter.format(data.summary.overdueAmount) },
            { label: 'Tỷ lệ quá hạn', value: percentFormatter.format(data.summary.overdueRatio) },
            { label: 'Ngày trễ hạn max', value: `${data.summary.maxDaysPastDue} ngày` },
            { label: 'Số hóa đơn mở', value: String(data.summary.openInvoiceCount) },
            { label: 'Kỳ hạn gần nhất', value: formatDate(data.summary.nextDueDate) },
          ]
        : [],
    [data],
  )

  if (!selectedTaxCode) return null

  return (
    <section className="card customer-360">
      <div className="card-row customer-360__header">
        <div>
          <p className="section-title">Customer 360 View</p>
          <h3>{selectedName || selectedTaxCode}</h3>
          <p className="muted">MST: {selectedTaxCode}</p>
        </div>
      </div>

      {loading ? <p className="muted">Đang tải hồ sơ 360...</p> : null}
      {error ? <p className="error-text">{error}</p> : null}

      {!loading && !error && data ? (
        <div className="customer-360__content">
          <div className="customer-360__meta">
            <span>Trạng thái: {data.status}</span>
            <span>Phụ trách: {data.ownerName || '--'}</span>
            <span>Quản lý: {data.managerName || '--'}</span>
          </div>

          <div className="kpi-grid customer-360__kpis">
            {summaryItems.map((item) => (
              <article className="kpi-card" key={item.label}>
                <p className="kpi-card__label">{item.label}</p>
                <p>{item.value}</p>
              </article>
            ))}
          </div>

          <div className="customer-360__risk">
            <p className="subsection-title">Risk Snapshot</p>
            <div className="customer-360__risk-grid">
              <span>Score: {formatScore(data.riskSnapshot.score)}</span>
              <span>Signal: {data.riskSnapshot.signal || '--'}</span>
              <span>As of: {formatDate(data.riskSnapshot.asOfDate)}</span>
              <span>Model: {data.riskSnapshot.modelVersion || '--'}</span>
            </div>
          </div>

          <div className="customer-360__lists">
            <article>
              <p className="subsection-title">Reminder Timeline</p>
              {data.reminderTimeline.length === 0 ? (
                <p className="muted">Chưa có lịch sử nhắc nợ.</p>
              ) : (
                <ul className="customer-360__list">
                  {data.reminderTimeline.slice(0, 5).map((item) => (
                    <li key={item.id}>
                      <strong>{item.channel}</strong> - {item.status} ({item.riskLevel})
                      <div className="text-caption">{formatDateTime(item.createdAt)}</div>
                    </li>
                  ))}
                </ul>
              )}
            </article>

            <article>
              <p className="subsection-title">Response States</p>
              {data.responseStates.length === 0 ? (
                <p className="muted">Chưa có trạng thái phản hồi.</p>
              ) : (
                <ul className="customer-360__list">
                  {data.responseStates.map((item) => (
                    <li key={item.channel}>
                      <strong>{item.channel}</strong> - {item.responseStatus}
                      <div className="text-caption">
                        Escalation: L{item.currentEscalationLevel} | Attempt: {item.attemptCount}
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </article>
          </div>
        </div>
      ) : null}
    </section>
  )
}
