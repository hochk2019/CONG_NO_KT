import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import RolePermissionsManager from '../RolePermissionsManager'

const mocks = vi.hoisted(() => ({
  fetchAdminRoles: vi.fn(),
  fetchAdminPermissions: vi.fn(),
  updateRolePermissions: vi.fn(),
}))

vi.mock('../../../api/admin', () => ({
  fetchAdminRoles: mocks.fetchAdminRoles,
  fetchAdminPermissions: mocks.fetchAdminPermissions,
  updateRolePermissions: mocks.updateRolePermissions,
}))

describe('RolePermissionsManager', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.fetchAdminRoles.mockResolvedValue([
      {
        id: 1,
        code: 'Admin',
        name: 'Quản trị',
        permissions: ['admin.manage', 'audit.view'],
      },
      {
        id: 2,
        code: 'Accountant',
        name: 'Kế toán',
        permissions: ['customer.view'],
      },
    ])
    mocks.fetchAdminPermissions.mockResolvedValue([
      { code: 'admin.manage', name: 'Quản trị hệ thống' },
      { code: 'advance.manage', name: 'Duyệt và quản lý trả hộ' },
      { code: 'audit.view', name: 'Xem nhật ký' },
      { code: 'customer.view', name: 'Xem khách hàng' },
    ])
    mocks.updateRolePermissions.mockResolvedValue(undefined)
  })

  it('loads role permissions, allows editing, and saves the updated assignment', async () => {
    const user = userEvent.setup()

    render(<RolePermissionsManager token="token" />)

    expect(await screen.findByRole('heading', { name: 'Quyền theo vai trò' })).toBeInTheDocument()
    expect(screen.getByText('customer.view (Xem khách hàng)')).toBeInTheDocument()
    expect(screen.getByText('audit.view (Xem nhật ký)')).toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText('Vai trò'), '2')

    const customerView = screen.getByLabelText('customer.view (Xem khách hàng)') as HTMLInputElement
    const auditView = screen.getByLabelText('audit.view (Xem nhật ký)') as HTMLInputElement

    expect(customerView.checked).toBe(true)
    expect(auditView.checked).toBe(false)

    await user.click(auditView)
    await user.click(screen.getByRole('button', { name: 'Lưu quyền' }))

    await waitFor(() => {
      expect(mocks.updateRolePermissions).toHaveBeenCalledWith('token', 2, [
        'audit.view',
        'customer.view',
      ])
    })
  })

  it('groups permissions and allows collapsing each group', async () => {
    const user = userEvent.setup()

    render(<RolePermissionsManager token="token" />)

    const adminGroup = await screen.findByRole('region', { name: 'Nhóm quyền Quản trị' })
    const advanceGroup = screen.getByRole('region', { name: 'Nhóm quyền Trả hộ' })
    const customerGroup = screen.getByRole('region', { name: 'Nhóm quyền Khách hàng' })

    expect(within(adminGroup).getByText('admin.manage (Quản trị hệ thống)')).toBeInTheDocument()
    expect(
      within(advanceGroup).getByText('advance.manage (Duyệt và quản lý trả hộ)'),
    ).toBeInTheDocument()
    expect(within(customerGroup).getByText('customer.view (Xem khách hàng)')).toBeInTheDocument()

    await user.click(within(customerGroup).getByRole('button', { name: 'Thu gọn Khách hàng' }))

    expect(within(customerGroup).getByText('customer.view (Xem khách hàng)')).not.toBeVisible()

    await user.click(within(customerGroup).getByRole('button', { name: 'Mở rộng Khách hàng' }))

    expect(within(customerGroup).getByText('customer.view (Xem khách hàng)')).toBeVisible()
  })

  it('can render in embedded mode without duplicating the outer page card header', async () => {
    render(<RolePermissionsManager token="token" variant="embedded" />)

    expect(await screen.findByLabelText('Vai trò')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Quyền theo vai trò' })).not.toBeInTheDocument()
    expect(
      screen.queryByText('Quản lý tập quyền chi tiết cho từng vai trò hệ thống.'),
    ).not.toBeInTheDocument()
  })
})
