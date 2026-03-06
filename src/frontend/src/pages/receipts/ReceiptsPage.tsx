import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../../context/AuthStore'
import ReceiptFormSection from './ReceiptFormSection'
import ReceiptListSection from './ReceiptListSection'

export default function ReceiptsPage() {
  const { state } = useAuth()
  const token = state.accessToken ?? ''
  const [reloadSignal, setReloadSignal] = useState(0)
  const navigate = useNavigate()

  const handleReload = () => {
    setReloadSignal((prev) => prev + 1)
  }

  const handleGoToReceiptTemplateImport = () => {
    navigate('/imports?tab=batch&type=RECEIPT')
  }

  return (
    <div className="page-stack receipts-page">
      <div className="page-header">
        <div className="page-title">
          <h1>Nhập phiếu thu</h1>
          <p className="muted">Luồng nhập liệu tuần tự từ trên xuống dưới để giảm sai sót.</p>
        </div>
        <div className="header-actions">
          <button
            className="btn btn-outline btn-sm"
            type="button"
            onClick={handleGoToReceiptTemplateImport}
          >
            Import từ template
          </button>
        </div>
      </div>

      <ReceiptFormSection token={token} onReload={handleReload} />
      <ReceiptListSection token={token} reloadSignal={reloadSignal} />
    </div>
  )
}
