import { useEffect, useMemo } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../../context/AuthStore'
import ImportBatchSection from './ImportBatchSection'

const IMPORT_TYPES = ['INVOICE', 'ADVANCE', 'RECEIPT'] as const
type ImportType = (typeof IMPORT_TYPES)[number]

const resolveImportType = (value: string | null): ImportType | null => {
  if (!value) return null
  const normalized = value.toUpperCase()
  return IMPORT_TYPES.includes(normalized as ImportType) ? (normalized as ImportType) : null
}

export default function ImportsPage() {
  const { state } = useAuth()
  const token = state.accessToken ?? ''
  const hasPermission = (permission: string) => state.permissions.includes(permission)

  const location = useLocation()
  const navigate = useNavigate()
  const searchParams = useMemo(() => new URLSearchParams(location.search), [location.search])
  const queryTabParam = useMemo(() => searchParams.get('tab'), [searchParams])
  const fixedType = useMemo(() => resolveImportType(searchParams.get('type')), [searchParams])
  const canStage = hasPermission('import.upload')
  const canCommitByType = {
    INVOICE: hasPermission('import.commit.invoice'),
    ADVANCE: hasPermission('import.commit.advance'),
    RECEIPT: hasPermission('import.commit.receipt'),
  } satisfies Record<ImportType, boolean>
  const batchCanCommit = fixedType
    ? canCommitByType[fixedType]
    : canCommitByType.INVOICE || canCommitByType.ADVANCE || canCommitByType.RECEIPT

  useEffect(() => {
    if (queryTabParam !== 'batch') {
      const nextParams = new URLSearchParams()
      nextParams.set('tab', 'batch')
      if (fixedType) {
        nextParams.set('type', fixedType)
      }
      navigate(`/imports?${nextParams.toString()}`, { replace: true })
    }
  }, [fixedType, navigate, queryTabParam])

  return (
    <div className="page-stack">
      <div className="page-header">
        <div>
          <h2>Import batch công nợ</h2>
          <p className="muted">Nhập file template tập trung cho hóa đơn, khoản trả hộ và phiếu thu.</p>
        </div>
      </div>
      <ImportBatchSection
        token={token}
        canStage={canStage}
        canCommit={batchCanCommit}
        fixedType={fixedType ?? undefined}
      />
    </div>
  )
}
