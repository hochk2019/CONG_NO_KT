import { useCallback, useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import type { CustomerReceiptRef } from '../../../api/customers'
import { ApiError } from '../../../api/client'
import { fetchReceiptAllocations, type ReceiptAllocationDetail } from '../../../api/receipts'
import { shortId } from './utils'

type ReceiptModalState = {
  id: string
  receiptNo?: string | null
  receiptDate?: string
  allocatedAmount?: number
} | null

type ReceiptModalParams = {
  id: string
  receiptNo?: string | null
  receiptDate?: string
  allocatedAmount?: number
}

type UseReceiptModalResult = {
  receiptModal: ReceiptModalState
  receiptAllocations: ReceiptAllocationDetail[]
  receiptAllocLoading: boolean
  receiptAllocError: string | null
  openReceiptModal: (params: ReceiptModalParams) => void
  closeReceiptModal: () => void
  renderReceiptRefs: (refs: CustomerReceiptRef[]) => ReactNode
}

export const useReceiptModal = (token: string): UseReceiptModalResult => {
  const [receiptModal, setReceiptModal] = useState<ReceiptModalState>(null)
  const [receiptAllocations, setReceiptAllocations] = useState<ReceiptAllocationDetail[]>([])
  const [receiptAllocLoading, setReceiptAllocLoading] = useState(false)
  const [receiptAllocError, setReceiptAllocError] = useState<string | null>(null)

  const openReceiptModal = useCallback((params: ReceiptModalParams) => {
    setReceiptModal(params)
  }, [])

  const closeReceiptModal = useCallback(() => {
    setReceiptModal(null)
    setReceiptAllocations([])
    setReceiptAllocError(null)
  }, [])

  const renderReceiptRefs = useCallback(
    (refs: CustomerReceiptRef[]) => {
      if (!refs || refs.length === 0) {
        return <span className="muted">-</span>
      }
      const display = refs.slice(0, 2)
      return (
        <div className="chip-row">
          {display.map((ref) => (
            <button
              key={ref.id}
              type="button"
              className="chip chip--link"
              onClick={() =>
                openReceiptModal({
                  id: ref.id,
                  receiptNo: ref.receiptNo ?? null,
                  receiptDate: ref.receiptDate,
                  allocatedAmount: ref.amount,
                })
              }
            >
              {ref.receiptNo?.trim() ? ref.receiptNo : shortId(ref.id)}
            </button>
          ))}
          {refs.length > 2 && <span className="muted">+{refs.length - 2}</span>}
        </div>
      )
    },
    [openReceiptModal],
  )

  useEffect(() => {
    if (!token || !receiptModal?.id) {
      setReceiptAllocations([])
      setReceiptAllocError(null)
      return
    }
    let isActive = true

    const load = async () => {
      setReceiptAllocLoading(true)
      setReceiptAllocError(null)
      try {
        const result = await fetchReceiptAllocations(token, receiptModal.id)
        if (!isActive) return
        setReceiptAllocations(result)
      } catch (err) {
        if (!isActive) return
        if (err instanceof ApiError) {
          setReceiptAllocError(err.message)
        } else {
          setReceiptAllocError('Không tải được chi tiết phân bổ phiếu thu.')
        }
      } finally {
        if (isActive) {
          setReceiptAllocLoading(false)
        }
      }
    }

    load()
    return () => {
      isActive = false
    }
  }, [token, receiptModal?.id])

  return {
    receiptModal,
    receiptAllocations,
    receiptAllocLoading,
    receiptAllocError,
    openReceiptModal,
    closeReceiptModal,
    renderReceiptRefs,
  }
}
