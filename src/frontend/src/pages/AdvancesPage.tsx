import { useEffect } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../context/AuthStore'
import AdvancesHero from './advances/AdvancesHero'
import './advances/advances.css'
import ManualAdvancesSection from './imports/ManualAdvancesSection'

export default function AdvancesPage() {
  const { state } = useAuth()
  const token = state.accessToken ?? ''
  const canApproveManual = state.roles.includes('Admin') || state.roles.includes('Supervisor')
  const location = useLocation()
  const navigate = useNavigate()

  useEffect(() => {
    const tab = new URLSearchParams(location.search).get('tab')
    if (tab === 'import') {
      navigate('/imports?tab=batch&type=ADVANCE', { replace: true })
    }
  }, [location.search, navigate])

  const handleGoToAdvanceTemplateImport = () => {
    navigate('/imports?tab=batch&type=ADVANCE')
  }

  return (
    <div className="page-stack advances-page">
      <AdvancesHero onImportTemplate={handleGoToAdvanceTemplateImport} />
      <ManualAdvancesSection token={token} canApprove={canApproveManual} />
    </div>
  )
}
