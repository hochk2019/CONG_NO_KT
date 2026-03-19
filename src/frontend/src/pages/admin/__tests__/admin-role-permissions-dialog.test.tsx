import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import AdminRolePermissionsDialog from '../AdminRolePermissionsDialog'

const mocks = vi.hoisted(() => ({
  rolePermissionsManager: vi.fn(),
}))

vi.mock('../RolePermissionsManager', () => ({
  default: (props: { token: string; variant?: 'card' | 'embedded' }) =>
    mocks.rolePermissionsManager(props),
}))

describe('AdminRolePermissionsDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.rolePermissionsManager.mockImplementation(
      ({ token, variant }: { token: string; variant?: 'card' | 'embedded' }) => (
        <div data-testid="role-permissions-manager">{`${token}:${variant ?? 'card'}`}</div>
      ),
    )
  })

  it('does not render anything when closed', () => {
    render(<AdminRolePermissionsDialog isOpen={false} token="token" onClose={vi.fn()} />)

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('renders the modal shell and closes from both controls', async () => {
    const user = userEvent.setup()
    const onClose = vi.fn()

    render(<AdminRolePermissionsDialog isOpen token="token" onClose={onClose} />)

    const dialog = screen.getByRole('dialog', { name: 'Quyền theo vai trò' })
    expect(dialog).toBeInTheDocument()
    expect(screen.getByTestId('role-permissions-manager')).toHaveTextContent('token:embedded')

    await user.click(screen.getByRole('button', { name: 'Đóng' }))
    await user.click(screen.getByRole('button', { name: 'Đóng hộp thoại' }))

    expect(onClose).toHaveBeenCalledTimes(2)
  })
})
