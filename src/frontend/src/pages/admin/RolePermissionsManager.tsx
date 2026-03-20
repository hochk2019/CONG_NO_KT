import { useEffect, useMemo, useState } from 'react'
import type { AdminPermission, AdminRole } from '../../api/admin'
import {
  fetchAdminPermissions,
  fetchAdminRoles,
  updateRolePermissions,
} from '../../api/admin'
import { ApiError } from '../../api/client'
import { formatRoleDisplay } from '../../utils/roles'

type RolePermissionsManagerProps = {
  token: string
  variant?: 'card' | 'embedded'
}

type PermissionGroup = {
  key: string
  label: string
  permissions: AdminPermission[]
}

const PERMISSION_GROUP_LABELS: Record<string, string> = {
  admin: 'Quản trị',
  advance: 'Trả hộ',
  audit: 'Nhật ký',
  backup: 'Sao lưu',
  customer: 'Khách hàng',
  import: 'Nhập liệu',
  invoice: 'Hóa đơn',
  period: 'Khóa kỳ',
  receipt: 'Thu tiền',
  reports: 'Báo cáo',
  risk: 'Rủi ro',
}

const PERMISSION_DESCRIPTIONS: Record<string, string> = {
  'admin.health.view': 'Xem sức khỏe hệ thống',
  'admin.manage': 'Quản trị hệ thống',
  'advance.manage': 'Duyệt và quản lý trả hộ',
  'audit.view': 'Xem nhật ký hệ thống',
  'backup.manage': 'Quản lý sao lưu',
  'backup.restore': 'Khôi phục bản sao lưu',
  'customer.assignment.manage': 'Quản lý phân công khách hàng',
  'customer.edit.all': 'Sửa mọi khách hàng',
  'customer.edit.owned': 'Sửa khách hàng phụ trách',
  'customer.edit.unassigned': 'Sửa khách hàng chưa phân công',
  'customer.view': 'Xem khách hàng',
  'import.commit.advance': 'Ghi nhận nhập trả hộ',
  'import.commit.invoice': 'Ghi nhận nhập hóa đơn',
  'import.commit.receipt': 'Ghi nhận nhập thu tiền',
  'import.history': 'Xem lịch sử nhập liệu',
  'import.rollback': 'Hoàn tác đợt nhập',
  'import.upload': 'Tải tệp nhập liệu',
  'invoice.manage': 'Quản lý hóa đơn',
  'period.lock.manage': 'Quản lý khóa kỳ',
  'receipt.approve': 'Duyệt thu tiền',
  'reports.view': 'Xem báo cáo',
  'risk.manage': 'Quản lý cảnh báo rủi ro',
  'risk.view': 'Xem cảnh báo rủi ro',
}

const sortPermissionCodes = (permissions: string[]) => {
  return [...new Set(permissions)].sort((left, right) => left.localeCompare(right))
}

const getPermissionGroupKey = (permissionCode: string) => {
  const [groupKey] = permissionCode.split('.')
  return groupKey || 'other'
}

const getPermissionGroupLabel = (groupKey: string) => {
  return PERMISSION_GROUP_LABELS[groupKey] ?? groupKey
}

const getPermissionDescription = (permission: AdminPermission) => {
  const name = permission.name.trim()
  if (name && name !== permission.code) {
    return name
  }

  return PERMISSION_DESCRIPTIONS[permission.code] ?? permission.code
}

const formatPermissionDisplay = (permission: AdminPermission) => {
  return `${permission.code} (${getPermissionDescription(permission)})`
}

export default function RolePermissionsManager({
  token,
  variant = 'card',
}: RolePermissionsManagerProps) {
  const [roles, setRoles] = useState<AdminRole[]>([])
  const [permissions, setPermissions] = useState<AdminPermission[]>([])
  const [selectedRoleId, setSelectedRoleId] = useState<number | null>(null)
  const [selectedPermissions, setSelectedPermissions] = useState<string[]>([])
  const [collapsedGroups, setCollapsedGroups] = useState<Record<string, boolean>>({})
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  useEffect(() => {
    if (!token) {
      setRoles([])
      setPermissions([])
      setSelectedRoleId(null)
      setSelectedPermissions([])
      return
    }

    let isActive = true

    const load = async () => {
      setLoading(true)
      setError(null)
      setSuccess(null)

      try {
        const [loadedRoles, loadedPermissions] = await Promise.all([
          fetchAdminRoles(token),
          fetchAdminPermissions(token),
        ])

        if (!isActive) {
          return
        }

        setRoles(loadedRoles)
        setPermissions(loadedPermissions)
        setSelectedRoleId((currentRoleId) => {
          if (currentRoleId && loadedRoles.some((role) => role.id === currentRoleId)) {
            return currentRoleId
          }

          return loadedRoles[0]?.id ?? null
        })
      } catch (err) {
        if (!isActive) {
          return
        }

        if (err instanceof ApiError) {
          setError(err.message)
        } else {
          setError('Không tải được cấu hình quyền vai trò.')
        }
      } finally {
        if (isActive) {
          setLoading(false)
        }
      }
    }

    void load()

    return () => {
      isActive = false
    }
  }, [token])

  const selectedRole = useMemo(() => {
    if (selectedRoleId === null) {
      return null
    }

    return roles.find((role) => role.id === selectedRoleId) ?? null
  }, [roles, selectedRoleId])

  useEffect(() => {
    if (!selectedRole) {
      setSelectedPermissions([])
      return
    }

    setSelectedPermissions(sortPermissionCodes(selectedRole.permissions))
  }, [selectedRole])

  const groupedPermissions = useMemo<PermissionGroup[]>(() => {
    const permissionGroups = new Map<string, AdminPermission[]>()

    permissions.forEach((permission) => {
      const groupKey = getPermissionGroupKey(permission.code)
      const existingPermissions = permissionGroups.get(groupKey)
      if (existingPermissions) {
        existingPermissions.push(permission)
        return
      }

      permissionGroups.set(groupKey, [permission])
    })

    return [...permissionGroups.entries()]
      .map(([groupKey, items]) => ({
        key: groupKey,
        label: getPermissionGroupLabel(groupKey),
        permissions: [...items].sort((left, right) => left.code.localeCompare(right.code)),
      }))
      .sort((left, right) => left.label.localeCompare(right.label, 'vi'))
  }, [permissions])

  useEffect(() => {
    setCollapsedGroups((currentGroups) => {
      const nextGroups = Object.fromEntries(
        groupedPermissions.map((group) => [group.key, currentGroups[group.key] ?? false]),
      )

      const currentKeys = Object.keys(currentGroups)
      const nextKeys = Object.keys(nextGroups)
      if (
        currentKeys.length === nextKeys.length &&
        nextKeys.every((groupKey) => currentGroups[groupKey] === nextGroups[groupKey])
      ) {
        return currentGroups
      }

      return nextGroups
    })
  }, [groupedPermissions])

  const togglePermission = (permissionCode: string) => {
    setSuccess(null)
    setSelectedPermissions((currentPermissions) => {
      if (currentPermissions.includes(permissionCode)) {
        return currentPermissions.filter((permission) => permission !== permissionCode)
      }

      return sortPermissionCodes([...currentPermissions, permissionCode])
    })
  }

  const handleRoleChange = (nextValue: string) => {
    setSuccess(null)
    setSelectedRoleId(nextValue ? Number(nextValue) : null)
  }

  const toggleGroup = (groupKey: string) => {
    setCollapsedGroups((currentGroups) => ({
      ...currentGroups,
      [groupKey]: !currentGroups[groupKey],
    }))
  }

  const setAllGroupsCollapsed = (isCollapsed: boolean) => {
    setCollapsedGroups(
      Object.fromEntries(groupedPermissions.map((group) => [group.key, isCollapsed])),
    )
  }

  const handleSave = async () => {
    if (!token || !selectedRole) {
      return
    }

    setSaving(true)
    setError(null)
    setSuccess(null)

    const nextPermissions = sortPermissionCodes(selectedPermissions)

    try {
      await updateRolePermissions(token, selectedRole.id, nextPermissions)
      setRoles((currentRoles) =>
        currentRoles.map((role) =>
          role.id === selectedRole.id
            ? {
                ...role,
                permissions: nextPermissions,
              }
            : role,
        ),
      )
      setSuccess(`Đã cập nhật quyền cho ${formatRoleDisplay(selectedRole.code, selectedRole.name)}.`)
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else {
        setError('Không cập nhật được quyền vai trò.')
      }
    } finally {
      setSaving(false)
    }
  }

  const areAllGroupsCollapsed =
    groupedPermissions.length > 0 &&
    groupedPermissions.every((group) => collapsedGroups[group.key] ?? false)
  const areAllGroupsExpanded =
    groupedPermissions.length > 0 &&
    groupedPermissions.every((group) => !(collapsedGroups[group.key] ?? false))

  const content = (
    <>
      {variant === 'embedded' && loading && <div className="muted">Đang tải…</div>}

      {error && (
        <div className="alert alert--error" role="alert" aria-live="assertive">
          {error}
        </div>
      )}
      {success && (
        <div className="alert alert--success" role="alert" aria-live="assertive">
          {success}
        </div>
      )}

      {!loading && roles.length === 0 && (
        <div className="empty-state">Không có vai trò để cấu hình quyền.</div>
      )}

      {!loading && roles.length > 0 && (
        <div className="form-stack">
          <label className="field">
            <span>Vai trò</span>
            <select
              value={selectedRoleId === null ? '' : String(selectedRoleId)}
              onChange={(event) => handleRoleChange(event.target.value)}
            >
              {roles.map((role) => (
                <option key={role.id} value={role.id}>
                  {formatRoleDisplay(role.code, role.name)}
                </option>
              ))}
            </select>
          </label>

          {permissions.length === 0 ? (
            <div className="empty-state">Chưa có quyền nào để gán.</div>
          ) : (
            <>
              <div className="card-row">
                <p className="muted">Chọn các quyền mà vai trò được phép sử dụng.</p>
                {groupedPermissions.length > 1 && (
                  <div className="inline-actions inline-actions--tight">
                    <button
                      className="collapsible-toggle"
                      type="button"
                      disabled={areAllGroupsExpanded}
                      onClick={() => setAllGroupsCollapsed(false)}
                    >
                      <span className="collapsible-toggle__icon" aria-hidden="true">
                        ▾
                      </span>
                      <span>Mở rộng tất cả</span>
                    </button>
                    <button
                      className="collapsible-toggle"
                      type="button"
                      disabled={areAllGroupsCollapsed}
                      onClick={() => setAllGroupsCollapsed(true)}
                    >
                      <span className="collapsible-toggle__icon" aria-hidden="true">
                        ▸
                      </span>
                      <span>Thu gọn tất cả</span>
                    </button>
                  </div>
                )}
              </div>
              <div className="permission-groups">
                {groupedPermissions.map((group) => {
                  const isCollapsed = collapsedGroups[group.key] ?? false
                  const contentId = `permission-group-${group.key}`

                  return (
                    <section
                      className={`collapsible-section permission-group ${isCollapsed ? 'is-collapsed' : 'is-open'}`}
                      key={group.key}
                      aria-label={`Nhóm quyền ${group.label}`}
                    >
                      <div className="card-row collapsible-header">
                        <div>
                          <h4>{group.label}</h4>
                          <p className="muted">{group.permissions.length} quyền</p>
                        </div>
                        <div className="collapsible-actions">
                          <button
                            className="collapsible-toggle"
                            type="button"
                            aria-expanded={!isCollapsed}
                            aria-controls={contentId}
                            onClick={() => toggleGroup(group.key)}
                          >
                            <span className="collapsible-toggle__icon" aria-hidden="true">
                              {isCollapsed ? '▸' : '▾'}
                            </span>
                            <span>{isCollapsed ? `Mở rộng ${group.label}` : `Thu gọn ${group.label}`}</span>
                          </button>
                        </div>
                      </div>
                      <div id={contentId} className="collapsible-content" hidden={isCollapsed}>
                        <div className="filters-grid permission-group__grid">
                          {group.permissions.map((permission) => (
                            <label className="field field-inline" key={permission.code}>
                              <input
                                type="checkbox"
                                checked={selectedPermissions.includes(permission.code)}
                                onChange={() => togglePermission(permission.code)}
                              />
                              <span>{formatPermissionDisplay(permission)}</span>
                            </label>
                          ))}
                        </div>
                      </div>
                    </section>
                  )
                })}
              </div>
            </>
          )}

          <div className="inline-actions">
            <button
              className="btn btn-primary"
              type="button"
              disabled={saving || selectedRole === null}
              onClick={handleSave}
            >
              Lưu quyền
            </button>
          </div>
        </div>
      )}
    </>
  )

  if (variant === 'embedded') {
    return <div className="form-stack">{content}</div>
  }

  return (
    <section className="card">
      <div className="card-row">
        <div>
          <h3>Quyền theo vai trò</h3>
          <p className="muted">Quản lý tập quyền chi tiết cho từng vai trò hệ thống.</p>
        </div>
        {loading && <span className="muted">Đang tải…</span>}
      </div>
      {content}
    </section>
  )
}
