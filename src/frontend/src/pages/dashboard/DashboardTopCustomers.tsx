import type { DashboardOverdueGroupItem, DashboardTopItem } from '../../api/dashboard'
import EmptyState from '../../components/EmptyState'
import DashboardOverdueChart from './DashboardOverdueChart'
import { formatMoney } from '../../utils/format'
import { renderTopList } from '../shared/topListRenderer'

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
          {renderTopList({
            rows: topOutstanding,
            emptyMessage: 'Danh sách này sẽ xuất hiện sau khi có công nợ.',
            formatAmount: formatMoney,
            buildMeta: () => null,
            emptyRenderer: (message) => (
              <EmptyState title="Chưa có dữ liệu" description={message} icon="📭" compact />
            ),
          })}
        </div>
        <div className="stack-section">
          <h4 className="subsection-title">Top quá hạn lâu nhất</h4>
          {renderTopList({
            rows: topOverdueDays,
            emptyMessage: 'Hiện chưa có khoản quá hạn.',
            formatAmount: formatMoney,
            buildMeta: (row) =>
              row.daysPastDue !== null && row.daysPastDue !== undefined
                ? `${row.daysPastDue} ngày`
                : null,
            emptyRenderer: (message) => (
              <EmptyState title="Chưa có dữ liệu" description={message} icon="📭" compact />
            ),
          })}
        </div>
      </section>

      <div className="panel-stack">
        <section className="card">
          <h3>Top trả đúng hạn nhất</h3>
          <p className="muted">Khách hàng có tỷ lệ quá hạn thấp nhất trong kỳ.</p>
          {renderTopList({
            rows: topOnTime,
            emptyMessage: 'Danh sách sẽ hiển thị khi có lịch sử thu đúng hạn.',
            formatAmount: formatMoney,
            buildMeta: () => null,
            emptyRenderer: (message) => (
              <EmptyState title="Chưa có dữ liệu" description={message} icon="📭" compact />
            ),
          })}
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
