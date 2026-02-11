import { useEffect, useMemo } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../../context/AuthStore'
import ImportBatchSection from './ImportBatchSection'
import ManualAdvancesSection from './ManualAdvancesSection'

const TAB_STORAGE_KEY = 'pref.imports.tab'

const getStoredTab = () => {
  if (typeof window === 'undefined') return 'batch'
  return window.localStorage.getItem(TAB_STORAGE_KEY) ?? 'batch'
}

const storeTab = (value: string) => {
  if (typeof window === 'undefined') return
  window.localStorage.setItem(TAB_STORAGE_KEY, value)
}

const resolveTab = (value: string | null) => {
  if (value === 'manual') return 'manual'
  return 'batch'
}

export default function ImportsPage() {
  const { state } = useAuth()
  const token = state.accessToken ?? ''
  const canStage = state.roles.some((role) => ['Admin', 'Supervisor', 'Accountant'].includes(role))
  const canCommit = state.roles.includes('Admin') || state.roles.includes('Supervisor')
  const canApproveManual = state.roles.includes('Admin') || state.roles.includes('Supervisor')

  const location = useLocation()
  const navigate = useNavigate()
  const storedTab = useMemo(() => resolveTab(getStoredTab()), [])
  const queryTabParam = useMemo(() => new URLSearchParams(location.search).get('tab'), [location.search])
  const activeTab = useMemo(() => resolveTab(queryTabParam ?? storedTab), [queryTabParam, storedTab])

  useEffect(() => {
    storeTab(activeTab)
  }, [activeTab])

  useEffect(() => {
    if (!queryTabParam) {
      navigate(`/imports?tab=${activeTab}`, { replace: true })
    }
  }, [queryTabParam, activeTab, navigate])

  const handleTabChange = (tab: 'batch' | 'manual') => {
    storeTab(tab)
    navigate(`/imports?tab=${tab}`)
  }

  return (
    <div className="page-stack">
      <div className="page-header">
        <div>
          <h2>Nhập liệu công nợ</h2>
          <p className="muted">Chọn hình thức nhập phù hợp (file hoặc thủ công).</p>
        </div>
      </div>

      <div className="tab-row" role="tablist" aria-label="Hình thức nhập liệu">
        <button
          className={`tab ${activeTab === 'batch' ? 'tab--active' : ''}`}
          type="button"
          role="tab"
          aria-selected={activeTab === 'batch'}
          onClick={() => handleTabChange('batch')}
        >
          Nhập file
        </button>
        <button
          className={`tab ${activeTab === 'manual' ? 'tab--active' : ''}`}
          type="button"
          role="tab"
          aria-selected={activeTab === 'manual'}
          onClick={() => handleTabChange('manual')}
        >
          Nhập thủ công (Khoản trả hộ KH)
        </button>
      </div>

      {activeTab === 'batch' && <ImportBatchSection token={token} canStage={canStage} canCommit={canCommit} />}
      {activeTab === 'manual' && (
        <ManualAdvancesSection token={token} canApprove={canApproveManual} />
      )}
    </div>
  )
}
