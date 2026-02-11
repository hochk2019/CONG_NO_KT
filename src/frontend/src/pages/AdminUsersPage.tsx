import { useEffect, useRef, useState } from 'react'
import { ApiError } from '../api/client'
import {
  type AdminRole,
  type AdminUser,
  createAdminUser,
  fetchAdminRoles,
  fetchAdminUsers,
  updateUserRoles,
  updateUserStatus,
  updateUserZalo,
} from '../api/admin'
import DataTable from '../components/DataTable'
import { useAuth } from '../context/AuthStore'
import { formatDateTime } from '../utils/format'
import { formatRoleDisplay } from '../utils/roles'

const DEFAULT_PAGE_SIZE = 10
const PAGE_SIZE_STORAGE_KEY = 'pref.table.pageSize'

const getStoredPageSize = () => {
  if (typeof window === 'undefined') return DEFAULT_PAGE_SIZE
  const raw = window.localStorage.getItem(PAGE_SIZE_STORAGE_KEY)
  const parsed = Number(raw)
  return Number.isFinite(parsed) && parsed > 0 ? parsed : DEFAULT_PAGE_SIZE
}

const storePageSize = (value: number) => {
  if (typeof window === 'undefined') return
  window.localStorage.setItem(PAGE_SIZE_STORAGE_KEY, String(value))
}

export default function AdminUsersPage() {
  const { state } = useAuth()
  const token = state.accessToken ?? ''

  const [rows, setRows] = useState<AdminUser[]>([])
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(() => getStoredPageSize())
  const [total, setTotal] = useState(0)
  const [search, setSearch] = useState('')
  const [listLoading, setListLoading] = useState(false)
  const [listError, setListError] = useState<string | null>(null)
  const [reload, setReload] = useState(0)

  const [roles, setRoles] = useState<AdminRole[]>([])
  const [rolesLoading, setRolesLoading] = useState(false)
  const [rolesError, setRolesError] = useState<string | null>(null)

  const [editingUser, setEditingUser] = useState<AdminUser | null>(null)
  const [selectedRoles, setSelectedRoles] = useState<string[]>([])
  const [loadingAction, setLoadingAction] = useState('')
  const [linkingUser, setLinkingUser] = useState<AdminUser | null>(null)
  const [linkingValue, setLinkingValue] = useState('')
  const [linkingError, setLinkingError] = useState<string | null>(null)
  const zaloInputRef = useRef<HTMLInputElement | null>(null)
  const rolesFirstInputRef = useRef<HTMLInputElement | null>(null)

  useEffect(() => {
    if (!linkingUser) return
    requestAnimationFrame(() => {
      zaloInputRef.current?.focus()
      zaloInputRef.current?.select()
    })
  }, [linkingUser])

  useEffect(() => {
    if (!editingUser) return
    requestAnimationFrame(() => {
      rolesFirstInputRef.current?.focus()
    })
  }, [editingUser])

  useEffect(() => {
    if (!linkingUser && !editingUser) return
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return
      if (linkingUser) {
        closeLinkingModal()
      } else if (editingUser) {
        closeRolesModal()
      }
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => {
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [linkingUser, editingUser])

  const [createUsername, setCreateUsername] = useState('')
  const [createPassword, setCreatePassword] = useState('')
  const [createFullName, setCreateFullName] = useState('')
  const [createEmail, setCreateEmail] = useState('')
  const [createPhone, setCreatePhone] = useState('')
  const [createIsActive, setCreateIsActive] = useState(true)
  const [createRoles, setCreateRoles] = useState<string[]>([])
  const [createError, setCreateError] = useState<string | null>(null)
  const [createSuccess, setCreateSuccess] = useState<string | null>(null)

  useEffect(() => {
    if (!token) return
    let isActive = true

    const loadRoles = async () => {
      setRolesLoading(true)
      setRolesError(null)
      try {
        const result = await fetchAdminRoles(token)
        if (!isActive) return
        setRoles(result)
      } catch (err) {
        if (!isActive) return
        if (err instanceof ApiError) {
          setRolesError(err.message)
        } else {
          setRolesError('Không tải được danh sách vai trò.')
        }
      } finally {
        if (isActive) {
          setRolesLoading(false)
        }
      }
    }

    loadRoles()
    return () => {
      isActive = false
    }
  }, [token])

  useEffect(() => {
    if (!token) return
    let isActive = true

    const load = async () => {
      setListLoading(true)
      setListError(null)
      try {
        const result = await fetchAdminUsers({
          token,
          search: search || undefined,
          page,
          pageSize,
        })
        if (!isActive) return
        setRows(result.items)
        setTotal(result.total)
      } catch (err) {
        if (!isActive) return
        if (err instanceof ApiError) {
          setListError(err.message)
        } else {
          setListError('Không tải được danh sách người dùng.')
        }
      } finally {
        if (isActive) {
          setListLoading(false)
        }
      }
    }

    load()
    return () => {
      isActive = false
    }
  }, [token, search, page, pageSize, reload])

  const handleEditRoles = (user: AdminUser) => {
    setEditingUser(user)
    setSelectedRoles(user.roles)
    setRolesError(null)
  }

  const handleSaveRoles = async () => {
    if (!token || !editingUser) return
    setRolesError(null)
    setLoadingAction(`roles-${editingUser.id}`)
    try {
      await updateUserRoles(token, editingUser.id, selectedRoles)
      closeRolesModal()
      setReload((value) => value + 1)
    } catch (err) {
      if (err instanceof ApiError) {
        setRolesError(err.message)
      } else {
        setRolesError('Không cập nhật được vai trò.')
      }
    } finally {
      setLoadingAction('')
    }
  }

  const handleToggleStatus = async (user: AdminUser) => {
    if (!token) return
    setLoadingAction(`status-${user.id}`)
    try {
      await updateUserStatus(token, user.id, !user.isActive)
      setReload((value) => value + 1)
    } catch (err) {
      if (err instanceof ApiError) {
        setListError(err.message)
      } else {
        setListError('Không cập nhật được trạng thái.')
      }
    } finally {
      setLoadingAction('')
    }
  }

  const handleLinkZalo = (user: AdminUser) => {
    setLinkingUser(user)
    setLinkingValue(user.zaloUserId ?? '')
    setLinkingError(null)
  }

  const closeLinkingModal = () => {
    setLinkingUser(null)
    setLinkingValue('')
    setLinkingError(null)
  }

  const closeRolesModal = () => {
    setEditingUser(null)
    setSelectedRoles([])
  }

  const handleSaveZalo = async () => {
    if (!token || !linkingUser) return
    setLinkingError(null)
    setLoadingAction(`zalo-${linkingUser.id}`)
    try {
      const payload = linkingValue.trim()
      await updateUserZalo(token, linkingUser.id, payload ? payload : null)
      closeLinkingModal()
      setReload((value) => value + 1)
    } catch (err) {
      if (err instanceof ApiError) {
        setLinkingError(err.message)
      } else {
        setLinkingError('Không cập nhật được Zalo user_id.')
      }
    } finally {
      setLoadingAction('')
    }
  }

  const formatZaloId = (value?: string | null) => {
    if (!value) return '-'
    const trimmed = value.trim()
    if (trimmed.length <= 8) {
      return trimmed
    }
    return `${trimmed.slice(0, 4)}…${trimmed.slice(-4)}`
  }

  const toggleRole = (code: string) => {
    setSelectedRoles((prev) =>
      prev.includes(code) ? prev.filter((item) => item !== code) : [...prev, code],
    )
  }

  const toggleCreateRole = (code: string) => {
    setCreateRoles((prev) =>
      prev.includes(code) ? prev.filter((item) => item !== code) : [...prev, code],
    )
  }

  const resetCreateForm = (clearSuccess = false) => {
    setCreateUsername('')
    setCreatePassword('')
    setCreateFullName('')
    setCreateEmail('')
    setCreatePhone('')
    setCreateIsActive(true)
    setCreateRoles([])
    setCreateError(null)
    if (clearSuccess) {
      setCreateSuccess(null)
    }
  }

  const handleCreateUser = async () => {
    if (!token) return
    const username = createUsername.trim()
    const password = createPassword.trim()
    setCreateError(null)
    setCreateSuccess(null)

    if (!username) {
      setCreateError('Vui lòng nhập tên đăng nhập.')
      return
    }
    if (password.length < 6) {
      setCreateError('Mật khẩu tối thiểu 6 ký tự.')
      return
    }
    if (createRoles.length === 0) {
      setCreateError('Vui lòng chọn ít nhất 1 vai trò.')
      return
    }

    setLoadingAction('create-user')
    try {
      await createAdminUser(token, {
        username,
        password,
        fullName: createFullName.trim() || null,
        email: createEmail.trim() || null,
        phone: createPhone.trim() || null,
        isActive: createIsActive,
        roles: createRoles,
      })
      setCreateSuccess(`Đã tạo người dùng ${username}.`)
      resetCreateForm()
      setReload((value) => value + 1)
    } catch (err) {
      if (err instanceof ApiError) {
        setCreateError(err.message)
      } else {
        setCreateError('Không tạo được người dùng.')
      }
    } finally {
      setLoadingAction('')
    }
  }

  const columns = [
    {
      key: 'username',
      label: 'Tên đăng nhập',
    },
    {
      key: 'fullName',
      label: 'Họ tên',
      render: (row: AdminUser) => row.fullName ?? '-',
    },
    {
      key: 'email',
      label: 'Email',
      render: (row: AdminUser) => row.email ?? '-',
    },
    {
      key: 'roles',
      label: 'Vai trò',
      render: (row: AdminUser) =>
        row.roles.length > 0 ? row.roles.map((code) => formatRoleDisplay(code)).join(', ') : '-',
    },
    {
      key: 'zalo',
      label: 'Zalo',
      render: (row: AdminUser) => (
        <div className="zalo-cell">
          <span className={row.zaloUserId ? 'pill pill-ok' : 'pill pill-warn'}>
            {row.zaloUserId ? 'Đã liên kết' : 'Chưa liên kết'}
          </span>
          {row.zaloUserId && (
            <span className="zalo-cell__id" title={row.zaloUserId}>
              {formatZaloId(row.zaloUserId)}
            </span>
          )}
        </div>
      ),
    },
    {
      key: 'status',
      label: 'Trạng thái',
      render: (row: AdminUser) => (
        <span className={row.isActive ? 'pill pill-ok' : 'pill pill-warn'}>
          {row.isActive ? 'Đang hoạt động' : 'Ngưng hoạt động'}
        </span>
      ),
    },
    {
      key: 'actions',
      label: 'Thao tác',
      render: (row: AdminUser) => (
        <div className="input-row">
          <button className="btn btn-ghost" type="button" onClick={() => handleEditRoles(row)}>
            Sửa vai trò
          </button>
          <button className="btn btn-ghost" type="button" onClick={() => handleLinkZalo(row)}>
            Liên kết Zalo
          </button>
          <button
            className="btn btn-ghost"
            type="button"
            disabled={loadingAction === `status-${row.id}`}
            onClick={() => handleToggleStatus(row)}
          >
            {row.isActive ? 'Vô hiệu hóa' : 'Kích hoạt'}
          </button>
        </div>
      ),
    },
  ]

  return (
    <div className="page-stack">
      <div className="page-header">
        <div>
          <h2>Người dùng & vai trò</h2>
        </div>
      </div>

      <section className="card" id="create-user">
        <div className="card-row">
          <div>
            <h3>Tạo người dùng mới</h3>
            <p className="muted">Mật khẩu tối thiểu 6 ký tự.</p>
          </div>
        </div>
        <div className="form-grid">
          <label className="field">
            <span>Tên đăng nhập</span>
            <input
              name="username"
              value={createUsername}
              onChange={(event) => setCreateUsername(event.target.value)}
              placeholder="username"
              autoComplete="username"
              spellCheck={false}
            />
          </label>
          <label className="field">
            <span>Mật khẩu</span>
            <input
              type="password"
              name="password"
              value={createPassword}
              onChange={(event) => setCreatePassword(event.target.value)}
              placeholder="••••••"
              autoComplete="new-password"
            />
          </label>
          <label className="field">
            <span>Họ tên</span>
            <input
              name="fullName"
              value={createFullName}
              onChange={(event) => setCreateFullName(event.target.value)}
              placeholder="Nguyễn Văn A"
              autoComplete="name"
            />
          </label>
          <label className="field">
            <span>Email</span>
            <input
              type="email"
              name="email"
              value={createEmail}
              onChange={(event) => setCreateEmail(event.target.value)}
              placeholder="email@congty.vn"
              autoComplete="email"
              spellCheck={false}
            />
          </label>
          <label className="field">
            <span>Số điện thoại</span>
            <input
              type="tel"
              name="phone"
              value={createPhone}
              onChange={(event) => setCreatePhone(event.target.value)}
              placeholder="0909…"
              autoComplete="tel"
            />
          </label>
          <label className="field field-inline">
            <input
              type="checkbox"
              name="createIsActive"
              checked={createIsActive}
              onChange={(event) => setCreateIsActive(event.target.checked)}
            />
            <span>Kích hoạt ngay</span>
          </label>
        </div>
        <div className="card-row">
          <div>
            <h4>Vai trò</h4>
            <p className="muted">Chọn ít nhất 1 vai trò.</p>
          </div>
          {rolesLoading && <span className="muted">Đang tải vai trò…</span>}
        </div>
        {rolesError && <div className="alert alert--error" role="alert" aria-live="assertive">{rolesError}</div>}
        {!rolesLoading && roles.length === 0 && (
          <div className="empty-state">Không có vai trò.</div>
        )}
        <div className="filters-grid">
          {roles.map((role) => (
            <label className="field field-inline" key={`create-${role.code}`}>
              <input
                type="checkbox"
                name={`create-role-${role.code}`}
                checked={createRoles.includes(role.code)}
                onChange={() => toggleCreateRole(role.code)}
              />
                <span>{formatRoleDisplay(role.code, role.name)}</span>
            </label>
          ))}
        </div>
        {createError && <div className="alert alert--error" role="alert" aria-live="assertive">{createError}</div>}
        {createSuccess && <div className="alert alert--success" role="alert" aria-live="assertive">{createSuccess}</div>}
        <div className="inline-actions">
          <button
            className="btn btn-primary"
            type="button"
            disabled={loadingAction === 'create-user' || rolesLoading}
            onClick={handleCreateUser}
          >
            Tạo người dùng
          </button>
          <button className="btn btn-outline" type="button" onClick={() => resetCreateForm(true)}>
            Làm mới
          </button>
        </div>
      </section>

      <section className="card">
        <div className="card-row">
          <div>
            <h3>Danh sách người dùng</h3>
            <p className="muted">Tìm theo username, họ tên hoặc email.</p>
          </div>
          {listLoading && <span className="muted">Đang tải…</span>}
        </div>
        <div className="filters-grid">
          <label className="field">
            <span>Tìm kiếm</span>
            <input
              name="search"
              value={search}
              onChange={(event) => {
                setSearch(event.target.value)
                setPage(1)
              }}
              placeholder="username, họ tên, email"
              autoComplete="off"
              spellCheck={false}
            />
          </label>
        </div>
        {listError && <div className="alert alert--error" role="alert" aria-live="assertive">{listError}</div>}
        <DataTable
          columns={columns}
          rows={rows}
          getRowKey={(row) => row.id}
          minWidth="1120px"
          emptyMessage={listLoading ? 'Đang tải…' : 'Không có người dùng.'}
          pagination={{ page, pageSize, total }}
          onPageChange={setPage}
          onPageSizeChange={(size) => {
            storePageSize(size)
            setPageSize(size)
            setPage(1)
          }}
        />
      </section>

      {linkingUser && (
        <div className="modal-backdrop">
          <button
            type="button"
            className="modal-scrim"
            aria-label="Đóng hộp thoại"
            onClick={closeLinkingModal}
          />
          <div
            className="modal modal--narrow"
            role="dialog"
            aria-modal="true"
            aria-labelledby="link-zalo-title"
          >
            <div className="modal-header">
              <div>
                <h3 id="link-zalo-title">Liên kết Zalo</h3>
                <p className="muted">Cập nhật Zalo user_id cho tài khoản.</p>
              </div>
              <button type="button" className="btn btn-ghost" onClick={closeLinkingModal} aria-label="Đóng">
                ✕
              </button>
            </div>
            <div className="modal-body form-stack">
              <div className="user-modal-meta">
                <div className="user-modal-meta__info">
                  <div className="user-modal-meta__name">
                    {linkingUser.fullName ?? linkingUser.username}
                  </div>
                  <div className="user-modal-meta__sub">
                    {linkingUser.email ?? 'Chưa có email'} • {linkingUser.username}
                  </div>
                </div>
                <span className={linkingUser.isActive ? 'pill pill-ok' : 'pill pill-warn'}>
                  {linkingUser.isActive ? 'Đang hoạt động' : 'Ngưng hoạt động'}
                </span>
              </div>
              <label className="field">
                <span>Zalo user_id</span>
                <input
                  ref={zaloInputRef}
                  name="zaloUserId"
                  value={linkingValue}
                  onChange={(event) => setLinkingValue(event.target.value)}
                  placeholder="VD: 1234567890"
                  autoComplete="off"
                />
                <span className="muted">Nếu để trống sẽ hủy liên kết hiện tại.</span>
              </label>
              <div className="field">
                <span>Trạng thái</span>
                <div className="input-row">
                  <span className={linkingUser.zaloUserId ? 'pill pill-ok' : 'pill pill-warn'}>
                    {linkingUser.zaloUserId ? 'Đã liên kết' : 'Chưa liên kết'}
                  </span>
                  {linkingUser.zaloUserId && (
                    <span className="user-modal-hint">ID: {linkingUser.zaloUserId}</span>
                  )}
                  {linkingUser.zaloLinkedAt && (
                    <span className="user-modal-hint">Liên kết lúc {formatDateTime(linkingUser.zaloLinkedAt)}</span>
                  )}
                </div>
              </div>
              {linkingError && (
                <div className="alert alert--error" role="alert" aria-live="assertive">
                  {linkingError}
                </div>
              )}
            </div>
            <div className="modal-footer modal-footer--end">
              <button
                className="btn btn-primary"
                type="button"
                disabled={loadingAction === `zalo-${linkingUser.id}`}
                onClick={handleSaveZalo}
              >
                Lưu liên kết
              </button>
              <button className="btn btn-outline" type="button" onClick={closeLinkingModal}>
                Hủy
              </button>
            </div>
          </div>
        </div>
      )}

      {editingUser && (
        <div className="modal-backdrop">
          <button
            type="button"
            className="modal-scrim"
            aria-label="Đóng hộp thoại"
            onClick={closeRolesModal}
          />
          <div
            className="modal modal--wide"
            role="dialog"
            aria-modal="true"
            aria-labelledby="edit-roles-title"
          >
            <div className="modal-header">
              <div>
                <h3 id="edit-roles-title">Chỉnh sửa vai trò</h3>
                <p className="muted">Chọn quyền truy cập phù hợp cho người dùng.</p>
              </div>
              <button type="button" className="btn btn-ghost" onClick={closeRolesModal} aria-label="Đóng">
                ✕
              </button>
            </div>
            <div className="modal-body form-stack">
              <div className="user-modal-meta">
                <div className="user-modal-meta__info">
                  <div className="user-modal-meta__name">
                    {editingUser.fullName ?? editingUser.username}
                  </div>
                  <div className="user-modal-meta__sub">
                    {editingUser.email ?? 'Chưa có email'} • {editingUser.username}
                  </div>
                </div>
                <span className={editingUser.isActive ? 'pill pill-ok' : 'pill pill-warn'}>
                  {editingUser.isActive ? 'Đang hoạt động' : 'Ngưng hoạt động'}
                </span>
              </div>
              {rolesLoading && <div className="muted">Đang tải vai trò…</div>}
              {rolesError && (
                <div className="alert alert--error" role="alert" aria-live="assertive">
                  {rolesError}
                </div>
              )}
              {!rolesLoading && roles.length === 0 && <div className="empty-state">Không có vai trò.</div>}
              {!rolesLoading && roles.length > 0 && (
                <>
                  <p className="muted">Chọn ít nhất 1 vai trò.</p>
                  <div className="filters-grid">
                    {roles.map((role, index) => (
                      <label className="field field-inline" key={role.code}>
                        <input
                          ref={index === 0 ? rolesFirstInputRef : undefined}
                          type="checkbox"
                          name={`edit-role-${role.code}`}
                          checked={selectedRoles.includes(role.code)}
                          onChange={() => toggleRole(role.code)}
                        />
                        <span>{formatRoleDisplay(role.code, role.name)}</span>
                      </label>
                    ))}
                  </div>
                </>
              )}
            </div>
            <div className="modal-footer modal-footer--end">
              <button
                className="btn btn-primary"
                type="button"
                disabled={loadingAction === `roles-${editingUser.id}` || rolesLoading}
                onClick={handleSaveRoles}
              >
                Lưu vai trò
              </button>
              <button className="btn btn-outline" type="button" onClick={closeRolesModal}>
                Hủy
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
