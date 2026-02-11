import type { CustomerDetail } from '../../api/customers'
import type { LookupOption } from '../../api/lookups'
import { formatDate, formatMoney } from '../../utils/format'

type CustomerEditModalProps = {
  open: boolean
  onClose: () => void
  onCopy: (value: string, label: string) => void
  canManageCustomers: boolean
  selectedName: string
  selectedTaxCode: string | null
  detail: CustomerDetail | null
  detailLoading: boolean
  detailError: string | null
  copyMessage: string | null
  ownerOptions: LookupOption[]
  managerOptions: LookupOption[]
  ownerLoading: boolean
  managerLoading: boolean
  ownerError: string | null
  managerError: string | null
  editName: string
  editAddress: string
  editEmail: string
  editPhone: string
  editStatus: string
  editPaymentTermsDays: string
  editCreditLimit: string
  editOwnerId: string
  editManagerId: string
  setEditName: (value: string) => void
  setEditAddress: (value: string) => void
  setEditEmail: (value: string) => void
  setEditPhone: (value: string) => void
  setEditStatus: (value: string) => void
  setEditPaymentTermsDays: (value: string) => void
  setEditCreditLimit: (value: string) => void
  setEditOwnerId: (value: string) => void
  setEditManagerId: (value: string) => void
  editLoading: boolean
  editSuccess: string | null
  editError: string | null
  statusLabels: Record<string, string>
  onSave: () => void
  onReset: () => void
}

export default function CustomerEditModal({
  open,
  onClose,
  onCopy,
  canManageCustomers,
  selectedName,
  selectedTaxCode,
  detail,
  detailLoading,
  detailError,
  copyMessage,
  ownerOptions,
  managerOptions,
  ownerLoading,
  managerLoading,
  ownerError,
  managerError,
  editName,
  editAddress,
  editEmail,
  editPhone,
  editStatus,
  editPaymentTermsDays,
  editCreditLimit,
  editOwnerId,
  editManagerId,
  setEditName,
  setEditAddress,
  setEditEmail,
  setEditPhone,
  setEditStatus,
  setEditPaymentTermsDays,
  setEditCreditLimit,
  setEditOwnerId,
  setEditManagerId,
  editLoading,
  editSuccess,
  editError,
  statusLabels,
  onSave,
  onReset,
}: CustomerEditModalProps) {
  if (!open) return null
  const email = detail?.email ?? undefined
  const phone = detail?.phone ?? undefined

  return (
    <div className="modal-backdrop">
      <button
        type="button"
        className="modal-scrim"
        aria-label="Đóng hộp thoại"
        onClick={onClose}
      />
      <div
        className="modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="customer-edit-title"
      >
        <div className="modal-header">
          <div>
            <p className="eyebrow">Sửa khách hàng</p>
            <h3 id="customer-edit-title">{detail?.name ?? selectedName}</h3>
            <p className="muted">MST: {selectedTaxCode || '-'}</p>
          </div>
          <div className="customer-actions">
            {selectedTaxCode && (
              <button
                className="btn btn-outline btn-table"
                type="button"
                onClick={() => onCopy(selectedTaxCode, 'MST')}
              >
                Sao chép MST
              </button>
            )}
            {email && (
              <button
                className="btn btn-outline btn-table"
                type="button"
                onClick={() => onCopy(email, 'email')}
              >
                Sao chép email
              </button>
            )}
            {phone && (
              <button
                className="btn btn-outline btn-table"
                type="button"
                onClick={() => onCopy(phone, 'số điện thoại')}
              >
                Sao chép SĐT
              </button>
            )}
            <button className="btn btn-ghost btn-table" type="button" onClick={onClose}>
              Đóng
            </button>
          </div>
        </div>
        <div className="modal-body">
          {copyMessage && (
            <div className="alert alert--info" role="status" aria-live="polite">
              {copyMessage}
            </div>
          )}
          {detailLoading && <div className="muted">Đang tải...</div>}
          {detailError && (
            <div className="alert alert--error" role="alert">
              {detailError}
            </div>
          )}
          {selectedTaxCode && detail ? (
            <>
              <div className="summary-grid summary-grid--emphasis">
                <div>
                  <strong>{statusLabels[detail.status] ?? detail.status}</strong>
                  <span>Trạng thái</span>
                </div>
                <div>
                  <strong>{formatMoney(detail.currentBalance)}</strong>
                  <span>Dư nợ hiện tại</span>
                </div>
                <div>
                  <strong>{detail.paymentTermsDays}</strong>
                  <span>Điều khoản thanh toán (ngày)</span>
                </div>
                <div>
                  <strong>{formatMoney(detail.creditLimit ?? null)}</strong>
                  <span>Hạn mức tín dụng</span>
                </div>
              </div>
              <div className="grid-split customer-detail-meta">
                <div>
                  <div className="list-row">
                    <span>Phụ trách</span>
                    <span>{detail.ownerName ?? '-'}</span>
                  </div>
                  <div className="list-row">
                    <span>Quản lý</span>
                    <span>{detail.managerName ?? '-'}</span>
                  </div>
                  <div className="list-row">
                    <span>Địa chỉ</span>
                    <span>{detail.address ?? '-'}</span>
                  </div>
                </div>
                <div>
                  <div className="list-row">
                    <span>Email</span>
                    <span>{detail.email ?? '-'}</span>
                  </div>
                  <div className="list-row">
                    <span>Điện thoại</span>
                    <span>{detail.phone ?? '-'}</span>
                  </div>
                  <div className="list-row">
                    <span>Cập nhật</span>
                    <span>{formatDate(detail.updatedAt)}</span>
                  </div>
                </div>
              </div>
              {canManageCustomers && (
                <div className="form-stack">
                  <div>
                    <h4>Cập nhật thông tin khách hàng</h4>
                    <p className="muted">
                      Phụ trách/Quản lý là người dùng trong hệ thống. Tạo ở Admin &gt; Người dùng và
                      chọn từ danh sách.
                    </p>
                  </div>
                  <div className="form-grid">
                    <label className="field">
                      <span>Tên khách hàng</span>
                      <input value={editName} onChange={(event) => setEditName(event.target.value)} />
                    </label>
                    <label className="field">
                      <span>Trạng thái</span>
                      <select
                        value={editStatus}
                        onChange={(event) => setEditStatus(event.target.value)}
                      >
                        <option value="ACTIVE">Đang hoạt động</option>
                        <option value="INACTIVE">Ngừng hoạt động</option>
                      </select>
                    </label>
                    <label className="field">
                      <span>Điều khoản thanh toán (ngày)</span>
                      <input
                        type="number"
                        value={editPaymentTermsDays}
                        onChange={(event) => setEditPaymentTermsDays(event.target.value)}
                      />
                    </label>
                    <label className="field">
                      <span>Hạn mức tín dụng</span>
                      <input
                        type="number"
                        value={editCreditLimit}
                        onChange={(event) => setEditCreditLimit(event.target.value)}
                      />
                    </label>
                    <label className="field">
                      <span>Địa chỉ</span>
                      <input value={editAddress} onChange={(event) => setEditAddress(event.target.value)} />
                    </label>
                    <label className="field">
                      <span>Email</span>
                      <input value={editEmail} onChange={(event) => setEditEmail(event.target.value)} />
                    </label>
                    <label className="field">
                      <span>Điện thoại</span>
                      <input value={editPhone} onChange={(event) => setEditPhone(event.target.value)} />
                    </label>
                    <label className="field">
                      <span>Phụ trách</span>
                      <select value={editOwnerId} onChange={(event) => setEditOwnerId(event.target.value)}>
                        <option value="">Chưa phân công</option>
                        {ownerOptions.map((option) => (
                          <option key={option.value} value={option.value}>
                            {option.label}
                          </option>
                        ))}
                      </select>
                    </label>
                    <label className="field">
                      <span>Quản lý</span>
                      <select value={editManagerId} onChange={(event) => setEditManagerId(event.target.value)}>
                        <option value="">Chưa phân công</option>
                        {managerOptions.map((option) => (
                          <option key={option.value} value={option.value}>
                            {option.label}
                          </option>
                        ))}
                      </select>
                    </label>
                  </div>
                  <div className="inline-actions">
                    <button className="btn btn-primary" type="button" onClick={onSave} disabled={editLoading}>
                      {editLoading ? 'Đang lưu...' : 'Lưu thông tin'}
                    </button>
                    <button className="btn btn-outline" type="button" onClick={onReset} disabled={editLoading}>
                      Hoàn tác
                    </button>
                    {(ownerLoading || managerLoading) && (
                      <span className="muted">Đang tải danh sách người dùng...</span>
                    )}
                  </div>
                  {(ownerError || managerError) && (
                    <div className="alert alert--error" role="alert">
                      {ownerError ?? managerError}
                    </div>
                  )}
                  {editSuccess && (
                    <div className="alert alert--success" role="alert">
                      {editSuccess}
                    </div>
                  )}
                  {editError && (
                    <div className="alert alert--error" role="alert">
                      {editError}
                    </div>
                  )}
                </div>
              )}
            </>
          ) : (
            <div className="empty-state">Không có dữ liệu chi tiết.</div>
          )}
        </div>
      </div>
    </div>
  )
}
