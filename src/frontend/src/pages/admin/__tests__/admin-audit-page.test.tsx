import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { vi } from 'vitest'
import { AuthContext, type AuthContextValue } from '../../../context/AuthStore'
import AdminAuditPage from '../../AdminAuditPage'

const mocks = vi.hoisted(() => ({
  fetchAuditLogs: vi.fn().mockResolvedValue({
    items: [
      {
        id: 'log-1',
        action: 'UPDATE',
        entityType: 'Invoice',
        entityId: 'inv-1',
        userName: 'admin',
        createdAt: new Date().toISOString(),
        beforeData: '{"status":"DRAFT"}',
        afterData: '{"status":"APPROVED"}',
      },
    ],
    total: 1,
    page: 1,
    pageSize: 10,
  }),
}))

vi.mock('../../../api/admin', () => ({
  fetchAuditLogs: mocks.fetchAuditLogs,
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

describe('AdminAuditPage', () => {
  it('opens detail modal when clicking view', async () => {
    const authValue = buildAuthContext()

    render(
      <MemoryRouter>
        <AuthContext.Provider value={authValue}>
          <AdminAuditPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    const viewButton = await screen.findByRole('button', { name: 'Xem' })
    await userEvent.click(viewButton)

    const dialog = await screen.findByRole('dialog')
    expect(dialog).toHaveTextContent('Before')
    expect(dialog).toHaveTextContent('After')
    expect(dialog).toHaveTextContent('"status": "DRAFT"')
    expect(dialog).toHaveTextContent('"status": "APPROVED"')

    const copyButtons = screen.getAllByRole('button', { name: 'Sao chép' })
    expect(copyButtons).toHaveLength(2)
    const expandButtons = screen.getAllByRole('button', { name: 'Mở rộng' })
    expect(expandButtons).toHaveLength(2)

    await waitFor(() => {
      expect(mocks.fetchAuditLogs).toHaveBeenCalled()
    })
  })
})
