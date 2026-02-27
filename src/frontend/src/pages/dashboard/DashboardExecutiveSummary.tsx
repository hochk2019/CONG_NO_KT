import type { DashboardOverview } from '../../api/dashboard'

type DashboardExecutiveSummaryProps = {
  summary: DashboardOverview['executiveSummary'] | null | undefined
}

const resolveSummaryTone = (status?: string) => {
  if (status === 'critical') return 'critical'
  if (status === 'warning') return 'warning'
  if (status === 'good') return 'good'
  return 'stable'
}

export default function DashboardExecutiveSummary({ summary }: DashboardExecutiveSummaryProps) {
  if (!summary) return null

  const tone = resolveSummaryTone(summary.status)

  return (
    <section className={`dashboard-summary dashboard-summary--${tone}`}>
      <div className="dashboard-summary__content">
        <p className="dashboard-summary__label">Tóm tắt điều hành</p>
        <h3 className="dashboard-summary__title">{summary.message}</h3>
        <p className="dashboard-summary__hint">{summary.actionHint}</p>
      </div>
      <div className="dashboard-summary__meta">
        <span>Cập nhật: {new Date(summary.generatedAt).toLocaleString('vi-VN')}</span>
      </div>
    </section>
  )
}
