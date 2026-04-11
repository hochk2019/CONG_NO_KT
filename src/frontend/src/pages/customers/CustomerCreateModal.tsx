import { useEffect, useState } from 'react'
import type { CustomerCreateRequest } from '../../api/customers'
import type { LookupOption } from '../../api/lookups'

type CustomerCreateModalProps = {
  open: boolean
  loading: boolean
  error: string | null
  ownerOptions: LookupOption[]
  managerOptions: LookupOption[]
  ownerLoading: boolean
  managerLoading: boolean
  ownerError: string | null
  managerError: string | null
  onClose: () => void
  onSubmit: (payload: CustomerCreateRequest) => Promise<void>
}

type FieldErrors = Partial<Record<'taxCode' | 'name' | 'paymentTermsDays' | 'creditLimit', string>>

const normalizeTaxCode = (value: string) => value.trim().toUpperCase()

export default function CustomerCreateModal({
  open,
  loading,
  error,
  ownerOptions,
  managerOptions,
  ownerLoading,
  managerLoading,
  ownerError,
  managerError,
  onClose,
  onSubmit,
}: CustomerCreateModalProps) {
  const [taxCode, setTaxCode] = useState('')
  const [name, setName] = useState('')
  const [address, setAddress] = useState('')
  const [email, setEmail] = useState('')
  const [phone, setPhone] = useState('')
  const [paymentTermsDays, setPaymentTermsDays] = useState('0')
  const [creditLimit, setCreditLimit] = useState('')
  const [ownerId, setOwnerId] = useState('')
  const [managerId, setManagerId] = useState('')
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})

  useEffect(() => {
    if (!open) return
    setTaxCode('')
    setName('')
    setAddress('')
    setEmail('')
    setPhone('')
    setPaymentTermsDays('0')
    setCreditLimit('')
    setOwnerId('')
    setManagerId('')
    setFieldErrors({})
  }, [open])

  if (!open) return null

  const clearFieldError = (field: keyof FieldErrors) => {
    setFieldErrors((prev) => {
      if (!prev[field]) return prev
      const next = { ...prev }
      delete next[field]
      return next
    })
  }

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()

    const normalizedTaxCode = normalizeTaxCode(taxCode)
    const trimmedName = name.trim()
    const nextErrors: FieldErrors = {}
    const paymentTermsValue = Number(paymentTermsDays)
    const creditLimitValue = creditLimit.trim() === '' ? null : Number(creditLimit)

    if (!normalizedTaxCode) {
      nextErrors.taxCode = 'Vui lòng nhập MST khách hàng.'
    }
    if (!trimmedName) {
      nextErrors.name = 'Vui lòng nhập tên khách hàng.'
    }
    if (!Number.isInteger(paymentTermsValue) || paymentTermsValue < 0) {
      nextErrors.paymentTermsDays = 'Số ngày công nợ phải là số nguyên không âm.'
    }
    if (creditLimitValue !== null && (!Number.isFinite(creditLimitValue) || creditLimitValue < 0)) {
      nextErrors.creditLimit = 'Hạn mức công nợ phải lớn hơn hoặc bằng 0.'
    }

    setFieldErrors(nextErrors)
    if (Object.keys(nextErrors).length > 0) return

    await onSubmit({
      taxCode: normalizedTaxCode,
      name: trimmedName,
      address: address.trim() || null,
      email: email.trim() || null,
      phone: phone.trim() || null,
      status: 'ACTIVE',
      paymentTermsDays: paymentTermsValue,
      creditLimit: creditLimitValue,
      ownerId: ownerId || null,
      managerId: managerId || null,
    })
  }

  return (
    <div className="modal-backdrop" role="presentation">
      <button className="modal-scrim" type="button" aria-label="Đóng" onClick={onClose} />
      <div className="modal" role="dialog" aria-modal="true" aria-labelledby="customer-create-title">
        <div className="modal-header">
          <div>
            <p className="section-subtitle">Thêm khách hàng</p>
            <h3 id="customer-create-title">Tạo hồ sơ khách hàng mới</h3>
          </div>
          <button className="btn btn-ghost btn-table" type="button" onClick={onClose} disabled={loading}>
            Đóng
          </button>
        </div>

        <form className="modal-body" onSubmit={handleSubmit}>
          {error ? <div className="alert alert--error">{error}</div> : null}
          {ownerError ? <div className="alert alert--info">{ownerError}</div> : null}
          {managerError ? <div className="alert alert--info">{managerError}</div> : null}

          <label className={`field ${fieldErrors.taxCode ? 'field--error' : ''}`}>
            <span>Mã số thuế</span>
            <input
              value={taxCode}
              onChange={(event) => {
                setTaxCode(event.target.value.toUpperCase())
                clearFieldError('taxCode')
              }}
              placeholder="VD: 0101234567"
              autoFocus
            />
            {fieldErrors.taxCode ? <small className="field-error">{fieldErrors.taxCode}</small> : null}
          </label>

          <label className={`field ${fieldErrors.name ? 'field--error' : ''}`}>
            <span>Tên khách hàng</span>
            <input
              value={name}
              onChange={(event) => {
                setName(event.target.value)
                clearFieldError('name')
              }}
              placeholder="Công ty Khách hàng"
            />
            {fieldErrors.name ? <small className="field-error">{fieldErrors.name}</small> : null}
          </label>

          <label className="field">
            <span>Địa chỉ</span>
            <textarea
              rows={3}
              value={address}
              onChange={(event) => setAddress(event.target.value)}
              placeholder="Địa chỉ giao dịch"
            />
          </label>

          <div className="modal-grid">
            <label className="field">
              <span>Email</span>
              <input
                type="email"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                placeholder="contact@example.com"
              />
            </label>

            <label className="field">
              <span>Số điện thoại</span>
              <input
                value={phone}
                onChange={(event) => setPhone(event.target.value)}
                placeholder="090..."
              />
            </label>
          </div>

          <div className="modal-grid">
            <label className={`field ${fieldErrors.paymentTermsDays ? 'field--error' : ''}`}>
              <span>Số ngày công nợ</span>
              <input
                type="number"
                min="0"
                step="1"
                value={paymentTermsDays}
                onChange={(event) => {
                  setPaymentTermsDays(event.target.value)
                  clearFieldError('paymentTermsDays')
                }}
              />
              {fieldErrors.paymentTermsDays ? (
                <small className="field-error">{fieldErrors.paymentTermsDays}</small>
              ) : null}
            </label>

            <label className={`field ${fieldErrors.creditLimit ? 'field--error' : ''}`}>
              <span>Hạn mức công nợ</span>
              <input
                type="number"
                min="0"
                step="1000"
                value={creditLimit}
                onChange={(event) => {
                  setCreditLimit(event.target.value)
                  clearFieldError('creditLimit')
                }}
                placeholder="Để trống nếu không giới hạn"
              />
              {fieldErrors.creditLimit ? <small className="field-error">{fieldErrors.creditLimit}</small> : null}
            </label>
          </div>

          <div className="modal-grid">
            <label className="field">
              <span>Phụ trách</span>
              <select value={ownerId} onChange={(event) => setOwnerId(event.target.value)} disabled={ownerLoading}>
                <option value="">{ownerLoading ? 'Đang tải...' : 'Chọn phụ trách'}</option>
                {ownerOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>

            <label className="field">
              <span>Quản lý</span>
              <select
                value={managerId}
                onChange={(event) => setManagerId(event.target.value)}
                disabled={managerLoading}
              >
                <option value="">{managerLoading ? 'Đang tải...' : 'Chọn quản lý'}</option>
                {managerOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>
          </div>

          <div className="modal-actions">
            <button className="btn btn-outline btn-table" type="button" onClick={onClose} disabled={loading}>
              Hủy
            </button>
            <button className="btn btn-primary btn-table" type="submit" disabled={loading}>
              {loading ? 'Đang tạo...' : 'Tạo khách hàng'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
