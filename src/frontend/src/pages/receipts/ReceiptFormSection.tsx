import { forwardRef, useCallback, useEffect, useImperativeHandle, useMemo, useState } from 'react'
import {
  createReceipt,
  approveReceipt,
  fetchReceiptOpenItems,
  type ReceiptOpenItem,
  type ReceiptTargetRef,
} from '../../api/receipts'
import {
  fetchCustomerLookup,
  fetchSellerLookup,
  mapTaxCodeOptions,
  type LookupOption,
} from '../../api/lookups'
import LookupInput from '../../components/LookupInput'
import { useDebouncedValue } from '../../hooks/useDebouncedValue'
import { formatMoney } from '../../utils/format'
import { ApiError } from '../../api/client'
import ReceiptAllocationModal from './ReceiptAllocationModal'
import ReceiptAdvancedModal from './ReceiptAdvancedModal'
import { allocationPriorityLabels, methodLabels } from './receiptLabels'

export type ReceiptFormHandle = {
  createDraft: () => void
  createAndApprove: () => void
}

type ReceiptFormSectionProps = {
  token: string
  onReload: () => void
}

const ReceiptFormSection = forwardRef<ReceiptFormHandle, ReceiptFormSectionProps>(
  ({ token, onReload }, ref) => {
    const [sellerOptions, setSellerOptions] = useState<LookupOption[]>([])
    const [customerOptions, setCustomerOptions] = useState<LookupOption[]>([])
    const [sellerQuery, setSellerQuery] = useState('')
    const [customerQuery, setCustomerQuery] = useState('')
    const debouncedSellerQuery = useDebouncedValue(sellerQuery, 300)
    const debouncedCustomerQuery = useDebouncedValue(customerQuery, 300)

    const [sellerTaxCode, setSellerTaxCode] = useState('')
    const [customerTaxCode, setCustomerTaxCode] = useState('')
    const [receiptNo, setReceiptNo] = useState('')
    const [receiptDate, setReceiptDate] = useState('')
    const [amount, setAmount] = useState('')
    const [method, setMethod] = useState('BANK')
    const [description, setDescription] = useState('')
    const [allocationPriority, setAllocationPriority] = useState('ISSUE_DATE')
    const [selectedTargets, setSelectedTargets] = useState<ReceiptTargetRef[]>([])
    const [openItems, setOpenItems] = useState<ReceiptOpenItem[]>([])
    const [openItemsLoading, setOpenItemsLoading] = useState(false)
    const [openItemsError, setOpenItemsError] = useState<string | null>(null)
    const [modalOpen, setModalOpen] = useState(false)
    const [overridePeriodLock, setOverridePeriodLock] = useState(false)
    const [overrideReason, setOverrideReason] = useState('')
    const [advancedOpen, setAdvancedOpen] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [createdReceipt, setCreatedReceipt] = useState<{
      id: string
      status: string
      amount: number
    } | null>(null)

    useEffect(() => {
      if (!token) return
      let isActive = true

      const loadSellers = async () => {
        try {
          const result = await fetchSellerLookup({
            token,
            search: debouncedSellerQuery || undefined,
            limit: 200,
          })
          if (!isActive) return
          setSellerOptions(mapTaxCodeOptions(result))
        } catch {
          if (!isActive) return
          setSellerOptions([])
        }
      }

      loadSellers()
      return () => {
        isActive = false
      }
    }, [token, debouncedSellerQuery])

    useEffect(() => {
      if (!token) return
      let isActive = true

      const loadCustomers = async () => {
        try {
          const result = await fetchCustomerLookup({
            token,
            search: debouncedCustomerQuery || undefined,
            limit: 200,
          })
          if (!isActive) return
          setCustomerOptions(mapTaxCodeOptions(result))
        } catch {
          if (!isActive) return
          setCustomerOptions([])
        }
      }

      loadCustomers()
      return () => {
        isActive = false
      }
    }, [token, debouncedCustomerQuery])

    useEffect(() => {
      if (!token || !sellerTaxCode.trim() || !customerTaxCode.trim()) {
        setOpenItems([])
        setSelectedTargets([])
        return
      }
      let isActive = true
      const loadOpenItems = async () => {
        setOpenItemsLoading(true)
        setOpenItemsError(null)
        try {
          const result = await fetchReceiptOpenItems({
            token,
            sellerTaxCode: sellerTaxCode.trim(),
            customerTaxCode: customerTaxCode.trim(),
          })
          if (!isActive) return
          setOpenItems(result)
        } catch (err) {
          if (!isActive) return
          if (err instanceof ApiError) {
            setOpenItemsError(err.message)
          } else {
            setOpenItemsError('Không tải được danh sách chứng từ.')
          }
        } finally {
          if (isActive) setOpenItemsLoading(false)
        }
      }

      loadOpenItems()
      return () => {
        isActive = false
      }
    }, [token, sellerTaxCode, customerTaxCode])

    useEffect(() => {
      setSelectedTargets([])
    }, [sellerTaxCode, customerTaxCode])

    const selectedCustomerOption = useMemo(
      () => customerOptions.find((option) => option.value === customerTaxCode.trim()),
      [customerOptions, customerTaxCode],
    )

    const customerHelpText = selectedCustomerOption?.label
      ? `Đã chọn: ${selectedCustomerOption.label}.`
      : 'Chọn khách hàng để xem chứng từ còn nợ.'

    const selectedTotal = useMemo(() => {
      if (selectedTargets.length === 0) return 0
      const amountValue = Number(amount) || 0
      const selectedOutstanding = selectedTargets.reduce((sum, target) => {
        const match = openItems.find(
          (item) => item.targetId === target.id && item.targetType === target.targetType,
        )
        return sum + (match?.outstandingAmount ?? 0)
      }, 0)
      return Math.min(amountValue, selectedOutstanding)
    }, [selectedTargets, openItems, amount])

    const unallocatedAmount = Math.max(0, (Number(amount) || 0) - selectedTotal)

    const priorityLabel = allocationPriorityLabels[allocationPriority] ?? allocationPriority
    const priorityText =
      allocationPriority === 'ISSUE_DATE' || allocationPriority === 'DUE_DATE'
        ? `${priorityLabel} (cũ hơn trước)`
        : priorityLabel

    const handleApplyAllocation = (targets: ReceiptTargetRef[]) => {
      setSelectedTargets(targets)
      setModalOpen(false)
    }

    const validate = useCallback(() => {
      setError(null)
      const amountValue = Number(amount)
      if (!sellerTaxCode.trim()) {
        setError('Vui lòng nhập MST bên bán.')
        return false
      }
      if (!customerTaxCode.trim()) {
        setError('Vui lòng nhập MST bên mua.')
        return false
      }
      if (!receiptDate.trim()) {
        setError('Vui lòng chọn ngày thu.')
        return false
      }
      if (!Number.isFinite(amountValue) || amountValue <= 0) {
        setError('Số tiền không hợp lệ.')
        return false
      }
      if (openItems.length > 0 && selectedTargets.length === 0) {
        setError('Cần chọn chứng từ để phân bổ trước khi lưu.')
        return false
      }
      if (overridePeriodLock && !overrideReason.trim()) {
        setError('Vui lòng nhập lý do vượt khóa kỳ.')
        return false
      }
      return true
    }, [
      amount,
      sellerTaxCode,
      customerTaxCode,
      receiptDate,
      openItems.length,
      selectedTargets.length,
      overridePeriodLock,
      overrideReason,
    ])

    const handleCreate = useCallback(
      async (approveNow: boolean) => {
        if (!token) {
          setError('Vui lòng đăng nhập.')
          return
        }
        if (!validate()) return

        const amountValue = Number(amount)
        try {
          const created = await createReceipt(token, {
            sellerTaxCode: sellerTaxCode.trim(),
            customerTaxCode: customerTaxCode.trim(),
            receiptNo: receiptNo.trim() || null,
            receiptDate: receiptDate.trim(),
            amount: amountValue,
            allocationMode: 'MANUAL',
            allocationPriority,
            selectedTargets: selectedTargets.length > 0 ? selectedTargets : null,
            method,
            description: description.trim() || null,
          })
          setCreatedReceipt({
            id: created.id,
            status: created.status,
            amount: created.amount,
          })

          if (approveNow) {
            await approveReceipt(token, created.id, {
              selectedTargets: selectedTargets.length > 0 ? selectedTargets : undefined,
              version: created.version,
              overridePeriodLock,
              overrideReason: overrideReason || undefined,
            })
          }

          onReload()
        } catch (err) {
          if (err instanceof ApiError) {
            setError(err.message)
          } else {
            setError('Không thể tạo phiếu thu.')
          }
        }
    },
      [
        token,
        sellerTaxCode,
        customerTaxCode,
        receiptNo,
        receiptDate,
        amount,
        allocationPriority,
        selectedTargets,
        method,
        description,
        overridePeriodLock,
        overrideReason,
        onReload,
        validate,
      ],
    )

  useImperativeHandle(
    ref,
    () => ({
      createDraft: () => handleCreate(false),
      createAndApprove: () => handleCreate(true),
    }),
    [handleCreate],
  )

  return (
    <>
      <section className="card">
        <div className="card-header">
          <div>
            <h2>Thông tin khách hàng</h2>
            <p className="muted">Chọn bên bán/bên mua để hiển thị chứng từ còn nợ.</p>
          </div>
        </div>
        <div className="form-grid form-grid--receipt">
          <LookupInput
            label="MST bên bán"
            value={sellerTaxCode}
            placeholder="VD: 2301098313"
            options={sellerOptions}
            helpText="Gõ để tìm và chọn từ gợi ý."
            onChange={(value) => {
              setSellerTaxCode(value)
              setSellerQuery(value)
            }}
          />
          <LookupInput
            label="MST bên mua"
            value={customerTaxCode}
            placeholder="VD: 2300328765"
            options={customerOptions}
            helpText={customerHelpText}
            onChange={(value) => {
              setCustomerTaxCode(value)
              setCustomerQuery(value)
            }}
          />
        </div>
      </section>

      <section className="card">
        <div className="card-header">
          <div>
            <h2>Thông tin phiếu thu</h2>
            <p className="muted">Các trường bắt buộc được ưu tiên đặt lên trước.</p>
          </div>
          <button className="btn btn-outline" type="button" onClick={() => setAdvancedOpen(true)}>
            Tùy chọn nâng cao
          </button>
        </div>

        <div className="form-grid form-grid--receipt">
          <label className="field">
            <span>Số chứng từ</span>
            <input
              value={receiptNo}
              onChange={(event) => setReceiptNo(event.target.value)}
              placeholder="VD: PT-001"
            />
          </label>
          <label className="field">
            <span>Ngày thu</span>
            <input
              type="date"
              value={receiptDate}
              onChange={(event) => setReceiptDate(event.target.value)}
            />
          </label>
          <label className="field">
            <span>Số tiền</span>
            <input
              type="number"
              min="0"
              inputMode="decimal"
              value={amount}
              onChange={(event) => setAmount(event.target.value)}
              placeholder="VD: 10000000"
            />
          </label>
          <label className="field">
            <span>Hình thức</span>
            <select value={method} onChange={(event) => setMethod(event.target.value)}>
              <option value="BANK">{methodLabels.BANK}</option>
              <option value="CASH">{methodLabels.CASH}</option>
              <option value="OTHER">{methodLabels.OTHER}</option>
            </select>
          </label>
          <label className="field field-span-full field-wide">
            <span>Diễn giải</span>
            <input
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              placeholder="Tùy chọn"
            />
          </label>
        </div>

        {(overridePeriodLock || overrideReason.trim()) && (
          <div className="alert alert--info">
            Đang bật vượt khóa kỳ khi duyệt.{' '}
            {overrideReason.trim() ? `Lý do: ${overrideReason.trim()}` : ''}
          </div>
        )}
      </section>

      <section className="card">
        <div className="card-header">
          <div>
            <h2>Phân bổ phiếu thu</h2>
            <p className="muted">Chọn chứng từ cần thanh toán theo ưu tiên.</p>
          </div>
          <button
            className="btn btn-outline"
            type="button"
            onClick={() => setModalOpen(true)}
            disabled={openItems.length === 0}
          >
            {openItemsLoading ? 'Đang tải...' : 'Chọn phân bổ'}
          </button>
        </div>

        <div className="receipt-allocation-row">
          <div className="helper">
            Ưu tiên: {priorityText}.
          </div>
        </div>

        <div className="receipt-summary">
          <strong>Tóm tắt phân bổ</strong>
          <div className="receipt-pill-row">
            <span className="receipt-pill">Đã chọn: {selectedTargets.length} chứng từ</span>
            <span className="receipt-pill receipt-pill--success">
              Đã phân bổ: {formatMoney(selectedTotal)}
            </span>
            <span className="receipt-pill receipt-pill--warning">
              Treo: {formatMoney(unallocatedAmount)}
            </span>
          </div>
        </div>

        {openItemsError && <div className="alert alert--error">{openItemsError}</div>}
        {openItems.length === 0 && !openItemsLoading && (
          <div className="alert alert--info">
            Không có chứng từ còn nợ. Phiếu thu sẽ được treo và tự gợi ý phân bổ khi phát sinh chứng từ.
          </div>
        )}
      </section>

      {createdReceipt && (
        <div className="alert alert--success">
          Đã tạo phiếu thu {createdReceipt.id} ({createdReceipt.status}) -{' '}
          {formatMoney(createdReceipt.amount)}.
        </div>
      )}
      {error && <div className="alert alert--error">{error}</div>}

      <ReceiptAllocationModal
        isOpen={modalOpen}
        token={token}
        sellerTaxCode={sellerTaxCode.trim()}
        customerTaxCode={customerTaxCode.trim()}
        amount={Number(amount) || 0}
        allocationPriority={allocationPriority}
        onPriorityChange={setAllocationPriority}
        openItems={openItems}
        selectedTargets={selectedTargets}
        onApply={handleApplyAllocation}
        onClose={() => setModalOpen(false)}
      />

      <ReceiptAdvancedModal
        key={`${advancedOpen}-${overridePeriodLock}-${overrideReason}`}
        isOpen={advancedOpen}
        overridePeriodLock={overridePeriodLock}
        overrideReason={overrideReason}
        onSave={(nextOverride, nextReason) => {
          setOverridePeriodLock(nextOverride)
          setOverrideReason(nextReason)
          setAdvancedOpen(false)
        }}
        onClose={() => setAdvancedOpen(false)}
      />
    </>
  )
  },
)

ReceiptFormSection.displayName = 'ReceiptFormSection'

export default ReceiptFormSection
