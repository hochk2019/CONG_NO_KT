import { useCallback, useEffect, useState } from 'react'
import type { CSSProperties } from 'react'
import { ApiError } from '../api/client'
import {
  fetchAdminHealth,
  runAdminBalanceReconcile,
  type AdminBalanceDriftItem,
  type AdminBalanceDriftSummary,
  type AdminBalanceReconcileResult,
  type AdminHealthSummary,
  type AdminHealthTable,
} from '../api/adminHealth'
import { useAuth } from '../context/AuthStore'
import { formatDateTime } from '../utils/format'

const tableLabels: Record<string, string> = {
  customers: 'Khách hàng',
  invoices: 'Hóa đơn',
  advances: 'Khoản trả hộ',
  receipts: 'Phiếu thu',
  import_batches: 'Lô nhập liệu',
  reminder_logs: 'Nhắc công nợ',
  notifications: 'Thông báo',
}

const formatOptional = (value?: string | null) => (value ? formatDateTime(value) : '-')
const formatDecimal = (value: number) => value.toLocaleString('vi-VN', { maximumFractionDigits: 2 })

export default function AdminHealthPage() {
  const { state } = useAuth()
  const token = state.accessToken ?? ''

  const [summary, setSummary] = useState<AdminHealthSummary | null>(null)
  const [loading, setLoading] = useState(false)
  const [reconcileLoading, setReconcileLoading] = useState(false)
  const [reconcileResult, setReconcileResult] = useState<AdminBalanceReconcileResult | null>(null)
  const [error, setError] = useState<string | null>(null)

  const loadHealth = useCallback(async () => {
    if (!token) return
    setLoading(true)
    setError(null)
    try {
      const data = await fetchAdminHealth(token)
      setSummary(data)
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else {
        setError('Không tải được tình trạng dữ liệu.')
      }
    } finally {
      setLoading(false)
    }
  }, [token])

  const runReconcile = useCallback(
    async (applyChanges: boolean) => {
      if (!token) return
      setReconcileLoading(true)
      setError(null)
      try {
        const result = await runAdminBalanceReconcile(token, {
          applyChanges,
          maxItems: 10,
          tolerance: 0.01,
        })
        setReconcileResult(result)
        await loadHealth()
      } catch (err) {
        if (err instanceof ApiError) {
          setError(err.message)
        } else {
          setError('Không chạy được đối soát số dư.')
        }
      } finally {
        setReconcileLoading(false)
      }
    },
    [loadHealth, token],
  )

  useEffect(() => {
    loadHealth()
  }, [loadHealth])

  const tables = summary?.tables ?? []
  const driftSummary = summary?.balanceDrift ?? null
  const driftRows = (reconcileResult?.topDrifts ?? driftSummary?.topDrifts ?? []) as AdminBalanceDriftItem[]
  const driftMetrics = (reconcileResult
    ? {
        checkedCustomers: reconcileResult.checkedCustomers,
        driftedCustomers: reconcileResult.driftedCustomers,
        totalAbsoluteDrift: reconcileResult.totalAbsoluteDrift,
        maxAbsoluteDrift: reconcileResult.maxAbsoluteDrift,
      }
    : driftSummary) as AdminBalanceDriftSummary | null

  return (
    <div className="page-stack">
      <div className="page-header">
        <div>
          <h2>Tình trạng dữ liệu</h2>
          <p className="muted">Theo dõi số lượng và thời điểm cập nhật gần nhất theo từng bảng.</p>
        </div>
        <div className="header-actions">
          <button className="btn btn-outline" onClick={() => runReconcile(false)} disabled={loading || reconcileLoading}>
            {reconcileLoading ? 'Đang xử lý...' : 'Kiểm tra lệch số dư'}
          </button>
          <button className="btn btn-primary" onClick={() => runReconcile(true)} disabled={loading || reconcileLoading}>
            {reconcileLoading ? 'Đang đồng bộ...' : 'Đồng bộ số dư ngay'}
          </button>
          <button className="btn btn-outline" onClick={loadHealth} disabled={loading}>
            {loading ? 'Đang tải...' : 'Làm mới'}
          </button>
        </div>
      </div>

      {error && (
        <div className="alert alert--error" role="alert" aria-live="assertive">
          {error}
        </div>
      )}

      <section className="card">
        <div className="card-row">
          <div>
            <h3>Đối soát current balance</h3>
            <p className="muted">
              So sánh số dư cache trên khách hàng với số dư tính lại từ hóa đơn/khoản trả hộ/phiếu thu.
            </p>
          </div>
          {reconcileResult?.executedAtUtc && (
            <span className="pill pill-info">Chạy lúc {formatDateTime(reconcileResult.executedAtUtc)}</span>
          )}
        </div>

        {driftMetrics ? (
          <div className="kpi-grid">
            <article className="kpi-card">
              <span className="kpi-card__label">Khách hàng đã kiểm tra</span>
              <strong>{driftMetrics.checkedCustomers.toLocaleString('vi-VN')}</strong>
            </article>
            <article className="kpi-card">
              <span className="kpi-card__label">Khách hàng lệch số dư</span>
              <strong>{driftMetrics.driftedCustomers.toLocaleString('vi-VN')}</strong>
            </article>
            <article className="kpi-card">
              <span className="kpi-card__label">Tổng độ lệch tuyệt đối</span>
              <strong>{formatDecimal(driftMetrics.totalAbsoluteDrift)}</strong>
            </article>
            <article className="kpi-card">
              <span className="kpi-card__label">Độ lệch lớn nhất</span>
              <strong>{formatDecimal(driftMetrics.maxAbsoluteDrift)}</strong>
            </article>
          </div>
        ) : (
          <div className="empty-state">Chưa có dữ liệu đối soát.</div>
        )}

        <div className="table-scroll">
          <table className="table" style={{ '--table-columns': 4, '--table-min-width': '720px' } as CSSProperties}>
            <thead className="table-head">
              <tr className="table-row">
                <th scope="col">MST khách hàng</th>
                <th scope="col">Số dư hiện tại</th>
                <th scope="col">Số dư tính lại</th>
                <th scope="col">Độ lệch</th>
              </tr>
            </thead>
            <tbody>
              {driftRows.map((item) => (
                <tr className="table-row" key={item.taxCode}>
                  <td>{item.taxCode}</td>
                  <td>{formatDecimal(item.currentBalance)}</td>
                  <td>{formatDecimal(item.expectedBalance)}</td>
                  <td>{formatDecimal(item.absoluteDrift)}</td>
                </tr>
              ))}
              {driftRows.length === 0 && (
                <tr className="table-row">
                  <td colSpan={4}>
                    <div className="empty-state">Không có lệch số dư trong ngưỡng đã cấu hình.</div>
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>

      <section className="card">
        <div className="card-row">
          <div>
            <h3>Tổng quan đồng bộ</h3>
            <p className="muted">Thời gian máy chủ: {summary ? formatDateTime(summary.serverTimeUtc) : '-'}</p>
          </div>
          <span className="pill pill-info">{tables.length} bảng</span>
        </div>

        {loading ? (
          <div className="empty-state">Đang tải dữ liệu...</div>
        ) : (
          <div className="table-scroll">
            <table className="table" style={{ '--table-columns': 4, '--table-min-width': '760px' } as CSSProperties}>
              <thead className="table-head">
                <tr className="table-row">
                  <th scope="col">Bảng</th>
                  <th scope="col">Số bản ghi</th>
                  <th scope="col">Tạo gần nhất</th>
                  <th scope="col">Cập nhật gần nhất</th>
                </tr>
              </thead>
              <tbody>
                {tables.map((item: AdminHealthTable) => (
                  <tr className="table-row" key={item.name}>
                    <td>{tableLabels[item.name] ?? item.name}</td>
                    <td>{item.count.toLocaleString('vi-VN')}</td>
                    <td>{formatOptional(item.lastCreatedAt)}</td>
                    <td>{formatOptional(item.lastUpdatedAt)}</td>
                  </tr>
                ))}
                {tables.length === 0 && (
                  <tr className="table-row">
                    <td colSpan={4}>
                      <div className="empty-state">Chưa có dữ liệu.</div>
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  )
}
