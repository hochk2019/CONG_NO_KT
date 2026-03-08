import { useCallback, useEffect, useMemo, useState } from 'react'
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
import MoneyInput from '../../components/MoneyInput'
import { useDebouncedValue } from '../../hooks/useDebouncedValue'
import { formatMoney } from '../../utils/format'
import { ApiError } from '../../api/client'
import ReceiptAllocationModal from './ReceiptAllocationModal'
import ReceiptAdvancedModal from './ReceiptAdvancedModal'
import { allocationPriorityLabels, methodLabels } from './receiptLabels'

type ReceiptFormSectionProps = {
  token: string
  onReload: () => void
}

type FieldErrorKey =
  | 'sellerTaxCode'
  | 'customerTaxCode'
  | 'receiptDate'
  | 'amount'
  | 'selectedTargets'
  | 'overrideReason'

export default function ReceiptFormSection({ token, onReload }: ReceiptFormSectionProps) {
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
    const [fieldErrors, setFieldErrors] = useState<Partial<Record<FieldErrorKey, string>>>({})
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

    const clearFieldError = useCallback((field: FieldErrorKey) => {
      setFieldErrors((prev) => {
        if (!prev[field]) return prev
        const next = { ...prev }
        delete next[field]
        return next
      })
    }, [])

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

    const amountValue = Number(amount)
    const canSubmit =
      sellerTaxCode.trim().length > 0 &&
      customerTaxCode.trim().length > 0 &&
      receiptDate.trim().length > 0 &&
      Number.isFinite(amountValue) &&
      amountValue > 0 &&
      (!openItems.length || selectedTargets.length > 0) &&
      (!overridePeriodLock || overrideReason.trim().length > 0)

    const submitBlockers = useMemo(() => {
      const blockers: string[] = []
      if (!sellerTaxCode.trim()) blockers.push('Chưa chọn MST bên bán')
      if (!customerTaxCode.trim()) blockers.push('Chưa chọn MST bên mua')
      if (!receiptDate.trim()) blockers.push('Chưa chọn ngày thu')
      if (!Number.isFinite(amountValue) || amountValue <= 0) blockers.push('Số tiền chưa hợp lệ')
      if (openItems.length > 0 && selectedTargets.length === 0) {
        blockers.push('Chưa chọn chứng từ để phân bổ')
      }
      if (overridePeriodLock && !overrideReason.trim()) blockers.push('Thiếu lý do vượt khóa kỳ')
      return blockers
    }, [
      sellerTaxCode,
      customerTaxCode,
      receiptDate,
      amountValue,
      openItems.length,
      selectedTargets.length,
      overridePeriodLock,
      overrideReason,
    ])

    const handleApplyAllocation = (targets: ReceiptTargetRef[]) => {
      setSelectedTargets(targets)
      clearFieldError('selectedTargets')
      setModalOpen(false)
    }

    const validate = useCallback(() => {
      const amountNumber = Number(amount)
      const nextErrors: Partial<Record<FieldErrorKey, string>> = {}

      if (!sellerTaxCode.trim()) {
        nextErrors.sellerTaxCode = 'Vui lòng nhập MST bên bán.'
      }
      if (!customerTaxCode.trim()) {
        nextErrors.customerTaxCode = 'Vui lòng nhập MST bên mua.'
      }
      if (!receiptDate.trim()) {
        nextErrors.receiptDate = 'Vui lòng chọn ngày thu.'
      }
      if (!Number.isFinite(amountNumber) || amountNumber <= 0) {
        nextErrors.amount = 'Số tiền phải lớn hơn 0.'
      }
      if (openItems.length > 0 && selectedTargets.length === 0) {
        nextErrors.selectedTargets = 'Cần chọn chứng từ để phân bổ trước khi lưu.'
      }
      if (overridePeriodLock && !overrideReason.trim()) {
        nextErrors.overrideReason = 'Vui lòng nhập lý do vượt khóa kỳ.'
      }

      setFieldErrors(nextErrors)
      const isValid = Object.keys(nextErrors).length === 0
      setError(isValid ? null : 'Vui lòng kiểm tra các trường bắt buộc trước khi lưu.')
      return isValid
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
          setError(null)
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

  return (
    <>
      <section className="card receipt-step-card">
        <div className="card-header receipt-step-header">
          <div className="receipt-step-title">
            <span className="receipt-step-index">B1</span>
            <div>
              <h2>Thông tin khách hàng</h2>
              <p className="muted">Chọn bên bán/bên mua để hiển thị chứng từ còn nợ.</p>
            </div>
          </div>
        </div>
        <div className="form-grid form-grid--receipt">
          <LookupInput
            label="MST bên bán"
            value={sellerTaxCode}
            placeholder="VD: 2301098313"
            options={sellerOptions}
            helpText="Gõ để tìm và chọn từ gợi ý."
            errorText={fieldErrors.sellerTaxCode}
            onChange={(value) => {
              setSellerTaxCode(value)
              setSellerQuery(value)
              clearFieldError('sellerTaxCode')
            }}
          />
          <LookupInput
            label="MST bên mua"
            value={customerTaxCode}
            placeholder="VD: 2300328765"
            options={customerOptions}
            helpText={customerHelpText}
            errorText={fieldErrors.customerTaxCode}
            onChange={(value) => {
              setCustomerTaxCode(value)
              setCustomerQuery(value)
              clearFieldError('customerTaxCode')
            }}
          />
        </div>
      </section>

      <section className="card receipt-step-card">
        <div className="card-header receipt-step-header">
          <div className="receipt-step-title">
            <span className="receipt-step-index">B2</span>
            <div>
              <h2>Thông tin phiếu thu</h2>
              <p className="muted">Nhập các trường bắt buộc trước, trường tùy chọn đặt phía sau.</p>
            </div>
          </div>
          <button className="btn btn-outline btn-sm" type="button" onClick={() => setAdvancedOpen(true)}>
            Tùy chọn nâng cao
          </button>
        </div>

        <div className="form-grid form-grid--receipt">
          <label className={fieldErrors.receiptDate ? 'field field--error' : 'field'}>
            <span>Ngày thu</span>
            <input
              type="date"
              value={receiptDate}
              onChange={(event) => {
                setReceiptDate(event.target.value)
                clearFieldError('receiptDate')
              }}
            />
            {fieldErrors.receiptDate && <span className="field-error">{fieldErrors.receiptDate}</span>}
          </label>
          <label className={fieldErrors.amount ? 'field field--error' : 'field'}>
            <span>Số tiền</span>
            <MoneyInput
              value={amount}
              onValueChange={(nextValue) => {
                setAmount(nextValue)
                clearFieldError('amount')
              }}
              placeholder="VD: 10.000.000"
            />
            {fieldErrors.amount && <span className="field-error">{fieldErrors.amount}</span>}
          </label>
          <label className="field">
            <span>Hình thức</span>
            <select value={method} onChange={(event) => setMethod(event.target.value)}>
              <option value="BANK">{methodLabels.BANK}</option>
              <option value="CASH">{methodLabels.CASH}</option>
              <option value="OTHER">{methodLabels.OTHER}</option>
            </select>
          </label>
          <label className="field">
            <span>Số chứng từ</span>
            <input
              value={receiptNo}
              onChange={(event) => setReceiptNo(event.target.value)}
              placeholder="VD: PT-001"
            />
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
            Đang bật vượt khóa kỳ khi duyệt. {overrideReason.trim() ? `Lý do: ${overrideReason.trim()}` : ''}
          </div>
        )}
      </section>

      <section className="card receipt-step-card">
        <div className="card-header receipt-step-header">
          <div className="receipt-step-title">
            <span className="receipt-step-index">B3</span>
            <div>
              <h2>Phân bổ phiếu thu</h2>
              <p className="muted">Chọn chứng từ cần thanh toán theo ưu tiên.</p>
            </div>
          </div>
          <button
            className="btn btn-outline btn-sm"
            type="button"
            onClick={() => setModalOpen(true)}
            disabled={openItems.length === 0}
          >
            {openItemsLoading ? 'Đang tải...' : 'Chọn phân bổ'}
          </button>
        </div>

        <div className="receipt-allocation-row">
          <div className="helper">Ưu tiên: {priorityText}.</div>
        </div>

        <div className={fieldErrors.selectedTargets ? 'receipt-summary receipt-summary--error' : 'receipt-summary'}>
          <strong>Tóm tắt phân bổ</strong>
          <div className="receipt-pill-row">
            <span className="receipt-pill">Đã chọn: {selectedTargets.length} chứng từ</span>
            <span className="receipt-pill receipt-pill--success">Đã phân bổ: {formatMoney(selectedTotal)}</span>
            <span className="receipt-pill receipt-pill--warning">Treo: {formatMoney(unallocatedAmount)}</span>
          </div>
          {fieldErrors.selectedTargets && <span className="field-error">{fieldErrors.selectedTargets}</span>}
        </div>

        {openItemsError && <div className="alert alert--error">{openItemsError}</div>}
        {openItems.length === 0 && !openItemsLoading && (
          <div className="alert alert--info">
            Không có chứng từ còn nợ. Phiếu thu sẽ được treo và tự gợi ý phân bổ khi phát sinh chứng từ.
          </div>
        )}
      </section>

      <section className="card receipt-step-card receipt-submit-card">
        <div className="card-header receipt-step-header receipt-step-header--submit">
          <div className="receipt-step-title">
            <span className="receipt-step-index">B4</span>
            <div>
              <h2>Xác nhận và lưu</h2>
              <p className="muted">
                {canSubmit
                  ? 'Biểu mẫu đã sẵn sàng. Bạn có thể lưu nháp hoặc lưu và duyệt ngay.'
                  : submitBlockers[0] ?? 'Vui lòng hoàn thiện biểu mẫu.'}
              </p>
            </div>
          </div>
          <div className="receipt-submit-actions">
            <button className="btn btn-outline btn-sm" type="button" onClick={() => handleCreate(false)}>
              Lưu nháp
            </button>
            <button
              className="btn btn-primary btn-sm"
              type="button"
              onClick={() => handleCreate(true)}
              disabled={!canSubmit}
            >
              Lưu & duyệt
            </button>
          </div>
        </div>

        {!canSubmit && submitBlockers.length > 1 && (
          <div className="receipt-blockers">
            {submitBlockers.slice(1).map((blocker) => (
              <span key={blocker} className="receipt-pill">
                {blocker}
              </span>
            ))}
          </div>
        )}

        {createdReceipt && (
          <div className="alert alert--success">
            Đã tạo phiếu thu {createdReceipt.id} ({createdReceipt.status}) - {formatMoney(createdReceipt.amount)}.
          </div>
        )}
        {error && <div className="alert alert--error">{error}</div>}
      </section>

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
          if (!nextOverride || nextReason.trim()) {
            clearFieldError('overrideReason')
          }
          setAdvancedOpen(false)
        }}
        onClose={() => setAdvancedOpen(false)}
      />
    </>
  )
}
