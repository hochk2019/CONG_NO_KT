import RolePermissionsManager from './RolePermissionsManager'

type AdminRolePermissionsDialogProps = {
  isOpen: boolean
  token: string
  onClose: () => void
}

export default function AdminRolePermissionsDialog({
  isOpen,
  token,
  onClose,
}: AdminRolePermissionsDialogProps) {
  if (!isOpen) {
    return null
  }

  return (
    <div className="modal-backdrop">
      <button
        type="button"
        className="modal-scrim"
        aria-label="Đóng hộp thoại"
        onClick={onClose}
      />
      <div
        className="modal modal--wide"
        role="dialog"
        aria-modal="true"
        aria-labelledby="role-permissions-title"
      >
        <div className="modal-header">
          <div>
            <h3 id="role-permissions-title">Quyền theo vai trò</h3>
            <p className="muted">Quản lý tập quyền chi tiết cho từng vai trò hệ thống.</p>
          </div>
          <button type="button" className="btn btn-ghost" onClick={onClose} aria-label="Đóng">
            ✕
          </button>
        </div>
        <div className="modal-body">
          <RolePermissionsManager token={token} variant="embedded" />
        </div>
      </div>
    </div>
  )
}
