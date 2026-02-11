import { useEffect, useRef, useState } from 'react'
import { ApiError } from '../api/client'
import { fetchAuditLogs, type AuditLogItem } from '../api/admin'
import DataTable from '../components/DataTable'
import { useAuth } from '../context/AuthStore'
import { formatDateTime } from '../utils/format'

const DEFAULT_PAGE_SIZE = 10
const PAGE_SIZE_STORAGE_KEY = 'pref.table.pageSize'
const COPY_RESET_MS = 1800

type CopySection = 'before' | 'after'

const formatAuditPayload = (value?: string | null) => {
  if (!value) return '-'
  try {
    return JSON.stringify(JSON.parse(value), null, 2)
  } catch {
    return value
  }
}

const copyTextToClipboard = async (value: string) => {
  if (navigator?.clipboard?.writeText) {
    await navigator.clipboard.writeText(value)
    return
  }
  const textarea = document.createElement('textarea')
  textarea.value = value
  textarea.setAttribute('readonly', 'true')
  textarea.style.position = 'fixed'
  textarea.style.opacity = '0'
  textarea.style.left = '-9999px'
  document.body.appendChild(textarea)
  textarea.focus()
  textarea.select()
  const ok = document.execCommand('copy')
  document.body.removeChild(textarea)
  if (!ok) {
    throw new Error('Copy failed')
  }
}

const getStoredPageSize = () => {
  if (typeof window === 'undefined') return DEFAULT_PAGE_SIZE
  const raw = window.localStorage.getItem(PAGE_SIZE_STORAGE_KEY)
  const parsed = Number(raw)
  return Number.isFinite(parsed) && parsed > 0 ? parsed : DEFAULT_PAGE_SIZE
}

const storePageSize = (value: number) => {
  if (typeof window === 'undefined') return
  window.localStorage.setItem(PAGE_SIZE_STORAGE_KEY, String(value))
}

export default function AdminAuditPage() {
  const { state } = useAuth()
  const token = state.accessToken ?? ''

  const [rows, setRows] = useState<AuditLogItem[]>([])
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(() => getStoredPageSize())
  const [total, setTotal] = useState(0)
  const [entityType, setEntityType] = useState('')
  const [entityId, setEntityId] = useState('')
  const [action, setAction] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [appliedFilters, setAppliedFilters] = useState(() => ({
    entityType: '',
    entityId: '',
    action: '',
    from: '',
    to: '',
  }))
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [detailLog, setDetailLog] = useState<AuditLogItem | null>(null)
  const [expandedSections, setExpandedSections] = useState({ before: false, after: false })
  const [copyStatus, setCopyStatus] = useState<{ section: CopySection | null; ok: boolean }>({
    section: null,
    ok: true,
  })
  const copyTimeoutRef = useRef<number | null>(null)
  const {
    entityType: appliedEntityType,
    entityId: appliedEntityId,
    action: appliedAction,
    from: appliedFrom,
    to: appliedTo,
  } = appliedFilters

  useEffect(() => {
    if (!token) return
    let isActive = true

    const load = async () => {
      setLoading(true)
      setError(null)
      try {
        const result = await fetchAuditLogs({
          token,
          entityType: appliedEntityType || undefined,
          entityId: appliedEntityId || undefined,
          action: appliedAction || undefined,
          from: appliedFrom || undefined,
          to: appliedTo || undefined,
          page,
          pageSize,
        })
        if (!isActive) return
        setRows(result.items)
        setTotal(result.total)
      } catch (err) {
        if (!isActive) return
        if (err instanceof ApiError) {
          setError(err.message)
        } else {
          setError('Không tải được nhật ký hệ thống.')
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
  }, [token, page, pageSize, appliedEntityType, appliedEntityId, appliedAction, appliedFrom, appliedTo])

  useEffect(() => {
    setExpandedSections({ before: false, after: false })
    setCopyStatus({ section: null, ok: true })
    if (copyTimeoutRef.current) {
      window.clearTimeout(copyTimeoutRef.current)
      copyTimeoutRef.current = null
    }
  }, [detailLog?.id])

  useEffect(() => {
    return () => {
      if (copyTimeoutRef.current) {
        window.clearTimeout(copyTimeoutRef.current)
      }
    }
  }, [])

  const handleApply = () => {
    setPage(1)
    setAppliedFilters({
      entityType,
      entityId,
      action,
      from,
      to,
    })
  }

  const handleClear = () => {
    setEntityType('')
    setEntityId('')
    setAction('')
    setFrom('')
    setTo('')
    setPage(1)
    setAppliedFilters({
      entityType: '',
      entityId: '',
      action: '',
      from: '',
      to: '',
    })
  }

  const handleCopy = async (section: CopySection, value: string) => {
    if (!value || value === '-') return
    try {
      await copyTextToClipboard(value)
      setCopyStatus({ section, ok: true })
    } catch {
      setCopyStatus({ section, ok: false })
    }
    if (copyTimeoutRef.current) {
      window.clearTimeout(copyTimeoutRef.current)
    }
    copyTimeoutRef.current = window.setTimeout(() => {
      setCopyStatus({ section: null, ok: true })
    }, COPY_RESET_MS)
  }

  const toggleExpanded = (section: CopySection) => {
    setExpandedSections((prev) => ({ ...prev, [section]: !prev[section] }))
  }

  const getCopyLabel = (section: CopySection) => {
    if (copyStatus.section !== section) return 'Sao chép'
    return copyStatus.ok ? 'Đã sao chép' : 'Không thể sao chép'
  }

  const columns = [
    {
      key: 'action',
      label: 'Hành động',
    },
    {
      key: 'entity',
      label: 'Đối tượng',
      render: (row: AuditLogItem) => `${row.entityType}:${row.entityId}`,
    },
    {
      key: 'userName',
      label: 'Người dùng',
      render: (row: AuditLogItem) => row.userName ?? '-',
    },
    {
      key: 'createdAt',
      label: 'Thời gian',
      render: (row: AuditLogItem) => formatDateTime(row.createdAt),
    },
    {
      key: 'details',
      label: 'Chi tiết',
      render: (row: AuditLogItem) => (
        <button
          className="btn btn-ghost"
          type="button"
          onClick={() => setDetailLog(row)}
        >
          Xem
        </button>
      ),
    },
  ]

  const beforeText = detailLog ? formatAuditPayload(detailLog.beforeData) : '-'
  const afterText = detailLog ? formatAuditPayload(detailLog.afterData) : '-'

  return (
    <div className="page-stack">
      <div className="page-header">
        <div>
          <h2>Nhật ký hệ thống</h2>
        </div>
      </div>

      <section className="card">
        <div className="card-row">
          <div>
            <h3>Bộ lọc</h3>
            <p className="muted">Lọc theo hành động, đối tượng, thời gian.</p>
          </div>
          {loading && <span className="muted">Đang tải...</span>}
        </div>
        <div className="filters-grid">
          <label className="field">
            <span>Loại đối tượng</span>
            <input value={entityType} onChange={(event) => setEntityType(event.target.value)} />
          </label>
          <label className="field">
            <span>Entity ID</span>
            <input value={entityId} onChange={(event) => setEntityId(event.target.value)} />
          </label>
          <label className="field">
            <span>Hành động</span>
            <input value={action} onChange={(event) => setAction(event.target.value)} />
          </label>
          <label className="field">
            <span>Từ ngày</span>
            <input
              type="date"
              value={from}
              onChange={(event) => setFrom(event.target.value)}
              placeholder="DD/MM/YYYY"
            />
          </label>
          <label className="field">
            <span>Đến ngày</span>
            <input
              type="date"
              value={to}
              onChange={(event) => setTo(event.target.value)}
              placeholder="DD/MM/YYYY"
            />
          </label>
        </div>
        <div className="inline-actions">
          <button className="btn btn-primary" type="button" onClick={handleApply}>
            Áp dụng
          </button>
          <button className="btn btn-outline" type="button" onClick={handleClear}>
            Xóa lọc
          </button>
        </div>
        {error && <div className="alert alert--error" role="alert" aria-live="assertive">{error}</div>}
      </section>

      <section className="card">
        <DataTable
          columns={columns}
          rows={rows}
          getRowKey={(row) => row.id}
          minWidth="900px"
          emptyMessage={loading ? 'Đang tải...' : 'Không có nhật ký hệ thống.'}
          pagination={{ page, pageSize, total }}
          onPageChange={setPage}
          onPageSizeChange={(size) => {
            storePageSize(size)
            setPageSize(size)
            setPage(1)
          }}
        />
      </section>

      {detailLog && (
        <div className="modal-backdrop">
          <button
            type="button"
            className="modal-scrim"
            aria-label="Đóng hộp thoại"
            onClick={() => setDetailLog(null)}
          />
          <div
            className="modal modal--wide"
            role="dialog"
            aria-modal="true"
            aria-labelledby="audit-log-detail-title"
          >
            <div className="modal-header">
              <div>
                <h3 id="audit-log-detail-title">Chi tiết nhật ký</h3>
                <p className="muted">
                  {detailLog.action} · {detailLog.entityType}:{detailLog.entityId}
                </p>
              </div>
              <button
                type="button"
                className="btn btn-ghost"
                onClick={() => setDetailLog(null)}
                aria-label="Đóng"
              >
                ✕
              </button>
            </div>
            <div className="modal-body form-stack">
              <div className="audit-detail">
                <div className="audit-detail__meta">
                  <div className="detail-meta">
                    <span>{detailLog.userName ?? '-'}</span>
                    <span>{formatDateTime(detailLog.createdAt)}</span>
                  </div>
                  <div className="audit-pill-group">
                    <span className="audit-pill">{detailLog.action}</span>
                    <span className="audit-pill audit-pill--outline">
                      {detailLog.entityType}:{detailLog.entityId}
                    </span>
                  </div>
                </div>
                <div className="audit-diff">
                  <section className="audit-diff__item">
                    <div className="audit-diff__header">
                      <strong>Before</strong>
                      <div className="audit-diff__actions">
                        <button
                          type="button"
                          className="btn btn-ghost btn-table"
                          onClick={() => handleCopy('before', beforeText)}
                          disabled={beforeText === '-'}
                        >
                          {getCopyLabel('before')}
                        </button>
                        <button
                          type="button"
                          className="btn btn-ghost btn-table"
                          aria-expanded={expandedSections.before}
                          onClick={() => toggleExpanded('before')}
                        >
                          {expandedSections.before ? 'Thu gọn' : 'Mở rộng'}
                        </button>
                      </div>
                    </div>
                    <pre
                      className={`code-block audit-code ${
                        expandedSections.before ? 'audit-code--expanded' : 'audit-code--collapsed'
                      }`}
                    >
                      {beforeText}
                    </pre>
                  </section>
                  <section className="audit-diff__item">
                    <div className="audit-diff__header">
                      <strong>After</strong>
                      <div className="audit-diff__actions">
                        <button
                          type="button"
                          className="btn btn-ghost btn-table"
                          onClick={() => handleCopy('after', afterText)}
                          disabled={afterText === '-'}
                        >
                          {getCopyLabel('after')}
                        </button>
                        <button
                          type="button"
                          className="btn btn-ghost btn-table"
                          aria-expanded={expandedSections.after}
                          onClick={() => toggleExpanded('after')}
                        >
                          {expandedSections.after ? 'Thu gọn' : 'Mở rộng'}
                        </button>
                      </div>
                    </div>
                    <pre
                      className={`code-block audit-code ${
                        expandedSections.after ? 'audit-code--expanded' : 'audit-code--collapsed'
                      }`}
                    >
                      {afterText}
                    </pre>
                  </section>
                </div>
              </div>
            </div>
            <div className="modal-footer modal-footer--end">
              <button type="button" className="btn btn-primary" onClick={() => setDetailLog(null)}>
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
