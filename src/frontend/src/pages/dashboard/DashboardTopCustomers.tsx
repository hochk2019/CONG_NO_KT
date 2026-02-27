import type { DashboardOverdueGroupItem, DashboardTopItem } from '../../api/dashboard'
import EmptyState from '../../components/EmptyState'
import DashboardOverdueChart from './DashboardOverdueChart'
import { formatMoney } from '../../utils/format'

type DashboardTopCustomersProps = {
  topCount: number
  onTopCountChange: (value: number) => void
  topOutstanding: DashboardTopItem[]
  topOverdueDays: DashboardTopItem[]
  topOnTime: DashboardTopItem[]
  overdueGroups: DashboardOverdueGroupItem[]
  overdueGroupsLoading: boolean
  overdueGroupsError: string | null
}

const renderTopList = (
  rows: DashboardTopItem[],
  emptyMessage: string,
  showDays: boolean,
) => {
  if (rows.length === 0) {
    return (
      <EmptyState
        title="Chưa có dữ liệu"
        description={emptyMessage}
        icon="📭"
        compact
      />
    )
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
            {showDays && row.daysPastDue !== null && row.daysPastDue !== undefined ? (
              <span className="muted">{row.daysPastDue} ngày</span>
            ) : null}
          </div>
        </div>
      ))}
    </div>
  )
}

export default function DashboardTopCustomers({
  topCount,
  onTopCountChange,
  topOutstanding,
  topOverdueDays,
  topOnTime,
  overdueGroups,
  overdueGroupsLoading,
  overdueGroupsError,
}: DashboardTopCustomersProps) {
  return (
    <section className="dashboard-panels">
      <section className="card">
        <div className="card-row">
          <div>
            <h3 className="section-title">Top cần chú ý</h3>
            <p className="muted">Công nợ, quá hạn và số ngày trễ.</p>
          </div>
          <label className="field">
            <span>Hiển thị</span>
            <select value={topCount} onChange={(event) => onTopCountChange(Number(event.target.value))}>
              <option value={5}>Top 5</option>
              <option value={10}>Top 10</option>
            </select>
          </label>
        </div>
        <div className="stack-section">
          <h4 className="subsection-title">Top công nợ lớn nhất</h4>
          {renderTopList(topOutstanding, 'Danh sách này sẽ xuất hiện sau khi có công nợ.', false)}
        </div>
        <div className="stack-section">
          <h4 className="subsection-title">Top quá hạn lâu nhất</h4>
          {renderTopList(topOverdueDays, 'Hiện chưa có khoản quá hạn.', true)}
        </div>
      </section>

      <div className="panel-stack">
        <section className="card">
          <h3>Top trả đúng hạn nhất</h3>
          <p className="muted">Khách hàng có tỷ lệ quá hạn thấp nhất trong kỳ.</p>
          {renderTopList(topOnTime, 'Danh sách sẽ hiển thị khi có lịch sử thu đúng hạn.', false)}
        </section>

        <section className="card">
          <h3>Quá hạn theo phụ trách</h3>
          <p className="muted">Tổng giá trị và tỷ lệ quá hạn theo nhóm phụ trách.</p>
          <DashboardOverdueChart
            rows={overdueGroups}
            loading={overdueGroupsLoading}
            error={overdueGroupsError}
          />
        </section>
      </div>
    </section>
  )
}
