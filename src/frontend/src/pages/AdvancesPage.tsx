import { useEffect, useMemo } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../context/AuthStore'
import ImportBatchSection from './imports/ImportBatchSection'
import ManualAdvancesSection from './imports/ManualAdvancesSection'

export default function AdvancesPage() {
  const { state } = useAuth()
  const token = state.accessToken ?? ''
  const canStage = state.roles.some((role) => ['Admin', 'Supervisor', 'Accountant'].includes(role))
  const canCommit = state.roles.includes('Admin') || state.roles.includes('Supervisor')
  const canApproveManual = state.roles.includes('Admin') || state.roles.includes('Supervisor')
  const location = useLocation()
  const navigate = useNavigate()

  const TAB_STORAGE_KEY = 'pref.advances.tab'
  const getStoredTab = () => {
    if (typeof window === 'undefined') return 'manual'
    return window.localStorage.getItem(TAB_STORAGE_KEY) ?? 'manual'
  }
  const storeTab = (value: string) => {
    if (typeof window === 'undefined') return
    window.localStorage.setItem(TAB_STORAGE_KEY, value)
  }
  const resolveTab = (value: string | null) => (value === 'import' ? 'import' : 'manual')

  const storedTab = useMemo(() => resolveTab(getStoredTab()), [])
  const queryTabParam = useMemo(() => new URLSearchParams(location.search).get('tab'), [location.search])
  const activeTab = useMemo(() => resolveTab(queryTabParam ?? storedTab), [queryTabParam, storedTab])

  useEffect(() => {
    storeTab(activeTab)
  }, [activeTab])

  useEffect(() => {
    if (!queryTabParam) {
      navigate(`/advances?tab=${activeTab}`, { replace: true })
    }
  }, [queryTabParam, activeTab, navigate])

  const handleTabChange = (tab: 'manual' | 'import') => {
    storeTab(tab)
    navigate(`/advances?tab=${tab}`)
  }

  return (
    <div className="page-stack">
      <div className="page-header">
        <div>
          <h2>Khoản trả hộ KH</h2>
          <p className="muted">
            Quản lý tập trung khoản trả hộ: nhập thủ công hoặc import từ file template.
          </p>
        </div>
      </div>

      <div className="tab-row" role="tablist" aria-label="Nghiệp vụ khoản trả hộ">
        <button
          className={`tab ${activeTab === 'manual' ? 'tab--active' : ''}`}
          type="button"
          role="tab"
          aria-selected={activeTab === 'manual'}
          onClick={() => handleTabChange('manual')}
        >
          Nhập thủ công
        </button>
        <button
          className={`tab ${activeTab === 'import' ? 'tab--active' : ''}`}
          type="button"
          role="tab"
          aria-selected={activeTab === 'import'}
          onClick={() => handleTabChange('import')}
        >
          Import từ template
        </button>
      </div>

      {activeTab === 'manual' && <ManualAdvancesSection token={token} canApprove={canApproveManual} />}
      {activeTab === 'import' && (
        <ImportBatchSection
          token={token}
          canStage={canStage}
          canCommit={canCommit}
          fixedType="ADVANCE"
        />
      )}
    </div>
  )
}
