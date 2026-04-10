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
  const queryTypeParam = useMemo(() => searchParams.get('type'), [searchParams])
  const fixedType = useMemo(() => resolveImportType(searchParams.get('type')), [searchParams])
  const hasInvalidTypeParam = useMemo(() => Boolean(queryTypeParam) && !fixedType, [fixedType, queryTypeParam])
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
    if (queryTabParam !== 'batch' || hasInvalidTypeParam) {
      const nextParams = new URLSearchParams()
      nextParams.set('tab', 'batch')
      if (fixedType) {
        nextParams.set('type', fixedType)
      }
      navigate(`/imports?${nextParams.toString()}`, { replace: true })
    }
  }, [fixedType, hasInvalidTypeParam, navigate, queryTabParam])

  return (
    <div className="page-stack">
      <div className="page-header">
        <div>
          <h2>Import từ Template</h2>
          <p className="muted">Điểm vào tập trung để nhập file template cho hóa đơn, trả hộ và phiếu thu theo đúng flow vận hành.</p>
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
