import { useRef, useState } from 'react'
import { useAuth } from '../../context/AuthStore'
import ReceiptFormSection, { type ReceiptFormHandle } from './ReceiptFormSection'
import ReceiptListSection from './ReceiptListSection'

export default function ReceiptsPage() {
  const { state } = useAuth()
  const token = state.accessToken ?? ''
  const [reloadSignal, setReloadSignal] = useState(0)
  const formRef = useRef<ReceiptFormHandle>(null)

  const handleReload = () => {
    setReloadSignal((prev) => prev + 1)
  }

  return (
    <div className="page-stack receipts-page">
      <div className="page-header">
        <div className="page-title">
          <h1>Nhập phiếu thu</h1>
        </div>
        <div className="header-actions">
          <button
            className="btn btn-primary"
            type="button"
            onClick={() => formRef.current?.createAndApprove()}
          >
            Lưu & duyệt
          </button>
          <button
            className="btn btn-outline"
            type="button"
            onClick={() => formRef.current?.createDraft()}
          >
            Lưu nháp
          </button>
        </div>
      </div>

      <ReceiptFormSection ref={formRef} token={token} onReload={handleReload} />
      <ReceiptListSection token={token} reloadSignal={reloadSignal} />
    </div>
  )
}
