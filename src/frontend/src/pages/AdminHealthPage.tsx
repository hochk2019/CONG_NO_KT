import { useCallback, useEffect, useState } from 'react'
import type { CSSProperties } from 'react'
import { ApiError } from '../api/client'
import { fetchAdminHealth, type AdminHealthSummary, type AdminHealthTable } from '../api/adminHealth'
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

export default function AdminHealthPage() {
  const { state } = useAuth()
  const token = state.accessToken ?? ''

  const [summary, setSummary] = useState<AdminHealthSummary | null>(null)
  const [loading, setLoading] = useState(false)
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

  useEffect(() => {
    loadHealth()
  }, [loadHealth])

  const tables = summary?.tables ?? []

  return (
    <div className="page-stack">
      <div className="page-header">
        <div>
          <h2>Tình trạng dữ liệu</h2>
          <p className="muted">Theo dõi số lượng và thời điểm cập nhật gần nhất theo từng bảng.</p>
        </div>
        <div className="header-actions">
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
