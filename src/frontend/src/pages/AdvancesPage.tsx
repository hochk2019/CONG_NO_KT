import { useEffect } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../context/AuthStore'
import './advances/advances.css'
import ManualAdvancesSection from './imports/ManualAdvancesSection'

export default function AdvancesPage() {
  const { state } = useAuth()
  const token = state.accessToken ?? ''
  const canApproveManual = state.permissions.includes('advance.manage')
  const location = useLocation()
  const navigate = useNavigate()

  const handleOpenImportTemplate = () => {
    navigate('/imports?tab=batch&type=ADVANCE')
  }

  useEffect(() => {
    const tab = new URLSearchParams(location.search).get('tab')
    if (tab === 'import') {
      navigate('/imports?tab=batch&type=ADVANCE', { replace: true })
    }
  }, [location.search, navigate])

  return (
    <div className="page-stack advances-page">
      <ManualAdvancesSection
        token={token}
        canApprove={canApproveManual}
        onImportTemplate={handleOpenImportTemplate}
      />
    </div>
  )
}
