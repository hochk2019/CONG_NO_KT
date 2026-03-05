import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { vi } from 'vitest'
import { AuthContext, type AuthContextValue } from '../../../context/AuthStore'
import AdminPeriodLocksPage from '../../AdminPeriodLocksPage'

const mocks = vi.hoisted(() => ({
  listPeriodLocks: vi.fn(),
  createPeriodLock: vi.fn(),
  unlockPeriodLock: vi.fn(),
}))

vi.mock('../../../api/periodLocks', () => ({
  listPeriodLocks: mocks.listPeriodLocks,
  createPeriodLock: mocks.createPeriodLock,
  unlockPeriodLock: mocks.unlockPeriodLock,
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
  mocks.listPeriodLocks.mockResolvedValue([
    {
      id: 'lock-1',
      periodType: 'MONTH',
      periodKey: '2026-02',
      lockedAt: new Date().toISOString(),
      lockedBy: 'admin',
      note: null,
    },
  ])
  mocks.createPeriodLock.mockResolvedValue(undefined)
  mocks.unlockPeriodLock.mockResolvedValue(undefined)
}

describe('AdminPeriodLocksPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    seedMocks()
  })

  it('opens unlock modal and requires reason', async () => {
    const authValue = buildAuthContext()
    const user = userEvent.setup()

    render(
      <MemoryRouter>
        <AuthContext.Provider value={authValue}>
          <AdminPeriodLocksPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    const unlockButton = await screen.findByRole('button', { name: 'Mở khóa' })
    await user.click(unlockButton)

    const dialog = await screen.findByRole('dialog')
    expect(dialog).toHaveTextContent('Mở khóa kỳ kế toán')

    const confirmButton = screen.getByRole('button', { name: 'Xác nhận mở khóa' })
    expect(confirmButton).toBeDisabled()

    await user.type(screen.getByLabelText('Lý do mở khóa'), 'Điều chỉnh số liệu')
    expect(confirmButton).toBeEnabled()
  })

  it('submits unlock from modal with reason', async () => {
    const authValue = buildAuthContext()
    const user = userEvent.setup()

    render(
      <MemoryRouter>
        <AuthContext.Provider value={authValue}>
          <AdminPeriodLocksPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    const unlockButton = await screen.findByRole('button', { name: 'Mở khóa' })
    await user.click(unlockButton)

    await user.type(screen.getByLabelText('Lý do mở khóa'), 'Điều chỉnh số liệu')
    await user.click(screen.getByRole('button', { name: 'Xác nhận mở khóa' }))

    await waitFor(() => {
      expect(mocks.unlockPeriodLock).toHaveBeenCalledWith('token', 'lock-1', 'Điều chỉnh số liệu')
    })
  })
})
