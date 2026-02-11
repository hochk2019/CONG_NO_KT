import { useEffect, useMemo, useState } from 'react'
import type { CSSProperties } from 'react'
import { ApiError } from '../api/client'
import {
  createPeriodLock,
  listPeriodLocks,
  unlockPeriodLock,
} from '../api/periodLocks'
import { useAuth } from '../context/AuthStore'
import { formatDateTime } from '../utils/format'

const periodTypeLabels: Record<string, string> = {
  MONTH: 'Tháng',
  QUARTER: 'Quý',
  YEAR: 'Năm',
}

export default function AdminPeriodLocksPage() {
  const { state } = useAuth()
  const token = state.accessToken ?? ''

  const [locks, setLocks] = useState<
    {
      id: string
      periodType: string
      periodKey: string
      lockedAt: string
      lockedBy?: string | null
      note?: string | null
    }[]
  >([])
  const [periodType, setPeriodType] = useState('MONTH')
  const [periodMonth, setPeriodMonth] = useState('')
  const [periodQuarter, setPeriodQuarter] = useState('')
  const [periodQuarterYear, setPeriodQuarterYear] = useState('')
  const [periodYear, setPeriodYear] = useState('')
  const [note, setNote] = useState('')
  const [unlockId, setUnlockId] = useState('')
  const [unlockReason, setUnlockReason] = useState('')
  const [error, setError] = useState<string | null>(null)

  const yearOptions = useMemo(() => {
    const currentYear = new Date().getFullYear()
    const startYear = 2000
    const endYear = currentYear + 5
    const years = []
    for (let year = startYear; year <= endYear; year += 1) {
      years.push(String(year))
    }
    return years
  }, [])

  const resolvePeriodKey = () => {
    if (periodType === 'MONTH') {
      return periodMonth.trim()
    }
    if (periodType === 'QUARTER') {
      if (!periodQuarter || !periodQuarterYear) {
        return ''
      }
      return `${periodQuarterYear.trim()}-Q${periodQuarter}`
    }
    if (periodType === 'YEAR') {
      return periodYear.trim()
    }
    return ''
  }

  const formatPeriodKey = (type: string, key: string) => {
    if (!key) {
      return '-'
    }
    if (type === 'MONTH') {
      const match = /^(\d{4})-(\d{2})$/.exec(key)
      if (match) {
        return `${match[2]}/${match[1]}`
      }
    }
    if (type === 'QUARTER') {
      const match = /^(\d{4})-Q([1-4])$/.exec(key)
      if (match) {
        return `Q${match[2]}/${match[1]}`
      }
    }
    return key
  }

  useEffect(() => {
    if (!token) return
    let isActive = true

    const load = async () => {
      try {
        const result = await listPeriodLocks(token)
        if (!isActive) return
        setLocks(result)
      } catch (err) {
        if (!isActive) return
        if (err instanceof ApiError) {
          setError(err.message)
        } else {
          setError('Không tải được danh sách khóa kỳ.')
        }
      }
    }

    load()
    return () => {
      isActive = false
    }
  }, [token])

  const refreshLocks = async () => {
    if (!token) return
    try {
      const result = await listPeriodLocks(token)
      setLocks(result)
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else {
        setError('Không tải được danh sách khóa kỳ.')
      }
    }
  }

  const handleCreate = async () => {
    if (!token) return
    setError(null)
    try {
      const periodKey = resolvePeriodKey()
      if (!periodKey) {
        setError('Vui lòng chọn kỳ.')
        return
      }
      await createPeriodLock(token, {
        periodType,
        periodKey,
        note: note || undefined,
      })
      setPeriodMonth('')
      setPeriodQuarter('')
      setPeriodQuarterYear('')
      setPeriodYear('')
      setNote('')
      await refreshLocks()
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else {
        setError('Không tạo được khóa kỳ.')
      }
    }
  }

  const handleUnlock = async (id: string) => {
    if (!token) return
    if (!window.confirm('Bạn chắc chắn muốn mở khóa kỳ này?')) {
      return
    }
    setError(null)
    try {
      await unlockPeriodLock(token, id, unlockReason || 'Mở khóa')
      setUnlockId('')
      setUnlockReason('')
      await refreshLocks()
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else {
        setError('Không mở khóa được.')
      }
    }
  }

  return (
    <div className="page-stack">
      <div className="page-header">
        <div>
          <h2>Khóa kỳ kế toán</h2>
        </div>
        <div className="header-actions">
          <button className="btn btn-primary" onClick={handleCreate}>
            Khóa kỳ mới
          </button>
        </div>
      </div>

      <section className="card">
        <h3>Tạo khóa kỳ</h3>
        <div className="form-grid">
          <label className="field">
            <span>Loại</span>
            <select value={periodType} onChange={(e) => setPeriodType(e.target.value)}>
              <option value="MONTH">{periodTypeLabels.MONTH}</option>
              <option value="QUARTER">{periodTypeLabels.QUARTER}</option>
              <option value="YEAR">{periodTypeLabels.YEAR}</option>
            </select>
          </label>
          <label className="field">
            <span>Kỳ</span>
            {periodType === 'MONTH' && (
              <input
                type="month"
                value={periodMonth}
                onChange={(event) => setPeriodMonth(event.target.value)}
                placeholder="MM/YYYY"
              />
            )}
            {periodType === 'QUARTER' && (
              <div className="input-row">
                <select
                  value={periodQuarter}
                  onChange={(event) => setPeriodQuarter(event.target.value)}
                >
                  <option value="">Quý</option>
                  <option value="1">Q1</option>
                  <option value="2">Q2</option>
                  <option value="3">Q3</option>
                  <option value="4">Q4</option>
                </select>
                <select
                  value={periodQuarterYear}
                  onChange={(event) => setPeriodQuarterYear(event.target.value)}
                >
                  <option value="">Năm</option>
                  {yearOptions.map((year) => (
                    <option key={year} value={year}>
                      {year}
                    </option>
                  ))}
                </select>
              </div>
            )}
            {periodType === 'YEAR' && (
              <select
                value={periodYear}
                onChange={(event) => setPeriodYear(event.target.value)}
              >
                <option value="">Năm</option>
                {yearOptions.map((year) => (
                  <option key={year} value={year}>
                    {year}
                  </option>
                ))}
              </select>
            )}
          </label>
          <label className="field">
            <span>Ghi chú</span>
            <input
              value={note}
              onChange={(event) => setNote(event.target.value)}
              placeholder="Tuỳ chọn"
            />
          </label>
        </div>
        {error && <div className="alert alert--error" role="alert" aria-live="assertive">{error}</div>}
      </section>

      <section className="card">
        <div className="table-scroll">
          <table className="table" style={{ '--table-columns': 5 } as CSSProperties}>
            <thead className="table-head">
              <tr className="table-row">
                <th scope="col">Loại</th>
                <th scope="col">Kỳ</th>
                <th scope="col">Người khóa</th>
                <th scope="col">Thời gian</th>
                <th scope="col">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {locks.length === 0 ? (
                <tr className="table-row table-empty">
                  <td colSpan={5}>
                    <div className="empty-state">Chưa có khóa kỳ.</div>
                  </td>
                </tr>
              ) : (
                locks.map((lock) => (
                  <tr className="table-row" key={lock.id}>
                    <td>{periodTypeLabels[lock.periodType] ?? lock.periodType}</td>
                    <td>{formatPeriodKey(lock.periodType, lock.periodKey)}</td>
                    <td>{lock.lockedBy ?? '-'}</td>
                    <td>{formatDateTime(lock.lockedAt)}</td>
                    <td>
                      <button className="btn btn-outline-danger" onClick={() => setUnlockId(lock.id)}>
                        Mở khóa
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </section>

      {unlockId && (
        <section className="card">
          <h3>Mở khóa kỳ</h3>
          <div className="form-grid">
            <label className="field">
              <span>Lý do</span>
              <input
                value={unlockReason}
                onChange={(event) => setUnlockReason(event.target.value)}
                placeholder="Bắt buộc"
              />
            </label>
          </div>
          <div className="inline-actions">
            <button className="btn btn-danger" onClick={() => handleUnlock(unlockId)}>
              Xác nhận mở khóa
            </button>
            <button className="btn btn-outline" onClick={() => setUnlockId('')}>
              Hủy
            </button>
          </div>
        </section>
      )}
    </div>
  )
}
