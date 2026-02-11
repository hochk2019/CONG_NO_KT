import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { vi } from 'vitest'
import { AuthContext, type AuthContextValue } from '../../../context/AuthStore'
import AdminUsersPage from '../../AdminUsersPage'

const mocks = vi.hoisted(() => ({
  fetchAdminUsers: vi.fn(),
  fetchAdminRoles: vi.fn(),
  createAdminUser: vi.fn(),
  updateUserRoles: vi.fn(),
  updateUserStatus: vi.fn(),
  updateUserZalo: vi.fn(),
}))

vi.mock('../../../api/admin', () => ({
  fetchAdminUsers: mocks.fetchAdminUsers,
  fetchAdminRoles: mocks.fetchAdminRoles,
  createAdminUser: mocks.createAdminUser,
  updateUserRoles: mocks.updateUserRoles,
  updateUserStatus: mocks.updateUserStatus,
  updateUserZalo: mocks.updateUserZalo,
}))

const buildAuthContext = (): AuthContextValue => ({
  state: {
    accessToken: 'token',
    expiresAt: new Date(Date.now() + 60_000).toISOString(),
    username: 'tester',
    roles: ['Admin'],
  },
  isAuthenticated: true,
  isBootstrapping: false,
  login: vi.fn(),
  logout: vi.fn(),
})

const seedMocks = () => {
  mocks.fetchAdminUsers.mockResolvedValue({
    items: [
      {
        id: 'user-1',
        username: 'admin',
        fullName: 'Admin User',
        email: 'admin@example.com',
        phone: '0909000000',
        isActive: true,
        roles: ['Admin'],
        zaloUserId: null,
        zaloLinkedAt: null,
      },
    ],
    total: 1,
    page: 1,
    pageSize: 10,
  })
  mocks.fetchAdminRoles.mockResolvedValue([
    { id: 1, code: 'Admin', name: 'Quản trị' },
    { id: 2, code: 'Accountant', name: 'Kế toán' },
  ])
}

describe('AdminUsersPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    seedMocks()
  })

  it('opens role editor in a modal', async () => {
    const authValue = buildAuthContext()

    render(
      <MemoryRouter>
        <AuthContext.Provider value={authValue}>
          <AdminUsersPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    const editButton = await screen.findByRole('button', { name: 'Sửa vai trò' })
    await userEvent.click(editButton)

    const dialog = await screen.findByRole('dialog')
    expect(dialog).toHaveTextContent('Chỉnh sửa vai trò')
    expect(dialog).toHaveTextContent('admin')
    expect(dialog).toHaveTextContent('Admin (Quản trị)')
    expect(screen.getByRole('button', { name: 'Lưu vai trò' })).toBeInTheDocument()

    await userEvent.keyboard('{Escape}')
    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    })

    await waitFor(() => {
      expect(mocks.fetchAdminUsers).toHaveBeenCalled()
    })
  })

  it('opens Zalo linking in a modal', async () => {
    const authValue = buildAuthContext()

    render(
      <MemoryRouter>
        <AuthContext.Provider value={authValue}>
          <AdminUsersPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    const linkButton = await screen.findByRole('button', { name: 'Liên kết Zalo' })
    await userEvent.click(linkButton)

    const dialog = await screen.findByRole('dialog')
    expect(dialog).toHaveTextContent('Liên kết Zalo')
    expect(screen.getByLabelText(/Zalo user_id/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Lưu liên kết' })).toBeInTheDocument()

    await userEvent.keyboard('{Escape}')
    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    })
  })
})
