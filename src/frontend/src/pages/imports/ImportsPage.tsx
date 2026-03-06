import { useEffect, useMemo } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../../context/AuthStore'
import ImportBatchSection from './ImportBatchSection'
import ManualInvoicesSection from './ManualInvoicesSection'

const TAB_STORAGE_KEY = 'pref.imports.tab'
const IMPORT_TYPES = ['INVOICE', 'ADVANCE', 'RECEIPT'] as const
type ImportType = (typeof IMPORT_TYPES)[number]

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

const resolveImportType = (value: string | null): ImportType | null => {
  if (!value) return null
  const normalized = value.toUpperCase()
  return IMPORT_TYPES.includes(normalized as ImportType) ? (normalized as ImportType) : null
}

export default function ImportsPage() {
  const { state } = useAuth()
  const token = state.accessToken ?? ''
  const canStage = state.roles.some((role) => ['Admin', 'Supervisor', 'Accountant'].includes(role))
  const canCommit = state.roles.includes('Admin') || state.roles.includes('Supervisor')

  const location = useLocation()
  const navigate = useNavigate()
  const storedTab = useMemo(() => resolveTab(getStoredTab()), [])
  const searchParams = useMemo(() => new URLSearchParams(location.search), [location.search])
  const queryTabParam = useMemo(() => searchParams.get('tab'), [searchParams])
  const fixedType = useMemo(() => resolveImportType(searchParams.get('type')), [searchParams])
  const activeTab = useMemo(() => resolveTab(queryTabParam ?? storedTab), [queryTabParam, storedTab])

  useEffect(() => {
    storeTab(activeTab)
  }, [activeTab])

  useEffect(() => {
    if (!queryTabParam) {
      const nextParams = new URLSearchParams()
      nextParams.set('tab', activeTab)
      if (fixedType) {
        nextParams.set('type', fixedType)
      }
      navigate(`/imports?${nextParams.toString()}`, { replace: true })
    }
  }, [queryTabParam, activeTab, fixedType, navigate])

  const handleTabChange = (tab: 'batch' | 'manual') => {
    storeTab(tab)
    const nextParams = new URLSearchParams()
    nextParams.set('tab', tab)
    if (fixedType) {
      nextParams.set('type', fixedType)
    }
    navigate(`/imports?${nextParams.toString()}`)
  }

  return (
    <div className="page-stack">
      <div className="page-header">
        <div>
          <h2>Nhập liệu công nợ</h2>
          <p className="muted">Nhập file template tập trung cho hóa đơn, khoản trả hộ và phiếu thu.</p>
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
          Nhập thủ công hóa đơn
        </button>
      </div>

      {activeTab === 'batch' && (
        <ImportBatchSection token={token} canStage={canStage} canCommit={canCommit} fixedType={fixedType ?? undefined} />
      )}
      {activeTab === 'manual' && <ManualInvoicesSection token={token} canCommit={canCommit} />}
    </div>
  )
}
