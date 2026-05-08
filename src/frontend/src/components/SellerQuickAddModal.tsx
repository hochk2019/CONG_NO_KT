import { useEffect, useState } from 'react'
import type { SellerCreateRequest } from '../api/lookups'

type SellerQuickAddModalProps = {
  open: boolean
  loading: boolean
  error: string | null
  initialTaxCode?: string
  onClose: () => void
  onSubmit: (payload: SellerCreateRequest) => Promise<void>
}

type FieldErrors = Partial<Record<'taxCode' | 'name', string>>

const normalizeTaxCode = (value: string) => value.trim().toUpperCase()

export default function SellerQuickAddModal({
  open,
  loading,
  error,
  initialTaxCode,
  onClose,
  onSubmit,
}: SellerQuickAddModalProps) {
  const [taxCode, setTaxCode] = useState('')
  const [name, setName] = useState('')
  const [shortName, setShortName] = useState('')
  const [address, setAddress] = useState('')
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})

  useEffect(() => {
    if (!open) return
    setTaxCode(normalizeTaxCode(initialTaxCode ?? ''))
    setName('')
    setShortName('')
    setAddress('')
    setFieldErrors({})
  }, [initialTaxCode, open])

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

    if (!normalizedTaxCode) {
      nextErrors.taxCode = 'Vui lòng nhập MST bên bán.'
    }
    if (!trimmedName) {
      nextErrors.name = 'Vui lòng nhập tên bên bán.'
    }

    setFieldErrors(nextErrors)
    if (Object.keys(nextErrors).length > 0) return

    await onSubmit({
      taxCode: normalizedTaxCode,
      name: trimmedName,
      shortName: shortName.trim() || null,
      address: address.trim() || null,
      status: 'ACTIVE',
    })
  }

  return (
    <div className="modal-backdrop" role="presentation">
      <button className="modal-scrim" type="button" aria-label="Đóng" onClick={onClose} />
      <div className="modal" role="dialog" aria-modal="true" aria-labelledby="seller-quick-add-title">
        <div className="modal-header">
          <div>
            <p className="section-subtitle">Thêm nhanh bên bán</p>
            <h3 id="seller-quick-add-title">Tạo nhà cung cấp mới</h3>
          </div>
          <button className="btn btn-ghost btn-table" type="button" onClick={onClose} disabled={loading}>
            Đóng
          </button>
        </div>

        <form className="modal-body" onSubmit={handleSubmit}>
          {error ? <div className="alert alert--error">{error}</div> : null}

          <label className={`field ${fieldErrors.taxCode ? 'field--error' : ''}`}>
            <span>Mã số thuế</span>
            <input
              value={taxCode}
              onChange={(event) => {
                setTaxCode(event.target.value.toUpperCase())
                clearFieldError('taxCode')
              }}
              placeholder="VD: 2301098313"
              autoFocus
            />
            {fieldErrors.taxCode ? <small className="field-error">{fieldErrors.taxCode}</small> : null}
          </label>

          <label className={`field ${fieldErrors.name ? 'field--error' : ''}`}>
            <span>Tên bên bán</span>
            <input
              value={name}
              onChange={(event) => {
                setName(event.target.value)
                clearFieldError('name')
              }}
              placeholder="Công ty ABC"
            />
            {fieldErrors.name ? <small className="field-error">{fieldErrors.name}</small> : null}
          </label>

          <label className="field">
            <span>Tên viết tắt</span>
            <input
              value={shortName}
              onChange={(event) => setShortName(event.target.value)}
              placeholder="ABC"
            />
          </label>

          <label className="field">
            <span>Địa chỉ</span>
            <textarea
              rows={3}
              value={address}
              onChange={(event) => setAddress(event.target.value)}
              placeholder="Địa chỉ bên bán"
            />
          </label>

          <div className="modal-actions">
            <button className="btn btn-outline btn-table" type="button" onClick={onClose} disabled={loading}>
              Hủy
            </button>
            <button className="btn btn-primary btn-table" type="submit" disabled={loading}>
              {loading ? 'Đang tạo...' : 'Tạo bên bán'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
