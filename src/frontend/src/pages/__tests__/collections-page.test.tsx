import type { ReactNode } from 'react'
import { render, screen, waitFor } from '@testing-library/react'
import { within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { vi } from 'vitest'
import { AuthContext, type AuthContextValue } from '../../context/AuthStore'
import CollectionsPage from '../CollectionsPage'

const mocks = vi.hoisted(() => ({
  listCollectionTasksMock: vi.fn(),
  generateCollectionTasksMock: vi.fn(),
  assignCollectionTaskMock: vi.fn(),
  updateCollectionTaskStatusMock: vi.fn(),
  fetchOwnerLookupMock: vi.fn(),
  fetchUserLookupMock: vi.fn(),
  mapOwnerOptionsMock: vi.fn(),
}))

vi.mock('../../api/collections', () => ({
  listCollectionTasks: (...args: unknown[]) => mocks.listCollectionTasksMock(...args),
  generateCollectionTasks: (...args: unknown[]) => mocks.generateCollectionTasksMock(...args),
  assignCollectionTask: (...args: unknown[]) => mocks.assignCollectionTaskMock(...args),
  updateCollectionTaskStatus: (...args: unknown[]) => mocks.updateCollectionTaskStatusMock(...args),
}))

vi.mock('../../api/lookups', () => ({
  fetchOwnerLookup: (...args: unknown[]) => mocks.fetchOwnerLookupMock(...args),
  fetchUserLookup: (...args: unknown[]) => mocks.fetchUserLookupMock(...args),
  mapOwnerOptions: (...args: unknown[]) => mocks.mapOwnerOptionsMock(...args),
}))

vi.mock('../../components/DataTable', () => ({
  default: ({
    columns,
    rows,
    getRowKey,
  }: {
    columns: Array<{
      key: string
      label: string
      render?: (row: Record<string, unknown>) => ReactNode
    }>
    rows: Record<string, unknown>[]
    getRowKey: (row: Record<string, unknown>, index: number) => string
  }) => (
    <table data-testid="collections-table">
      <thead>
        <tr>
          {columns.map((column) => (
            <th key={column.key}>{column.label}</th>
          ))}
        </tr>
      </thead>
      <tbody>
        {rows.map((row, index) => (
          <tr key={getRowKey(row, index)}>
            {columns.map((column) => (
              <td key={column.key}>
                {column.render ? column.render(row) : (row[column.key] as ReactNode)}
              </td>
            ))}
          </tr>
        ))}
      </tbody>
    </table>
  ),
}))

const buildAuthContext = (roles: string[] = ['Admin']): AuthContextValue => ({
  state: {
    accessToken: 'token',
    expiresAt: new Date(Date.now() + 60_000).toISOString(),
    username: 'tester',
    roles,
  },
  isAuthenticated: true,
  isBootstrapping: false,
  login: vi.fn(),
  logout: vi.fn(),
})

function renderPage(roles: string[] = ['Admin']) {
  const authValue = buildAuthContext(roles)
  return render(
    <MemoryRouter>
      <AuthContext.Provider value={authValue}>
        <CollectionsPage />
      </AuthContext.Provider>
    </MemoryRouter>,
  )
}

describe('CollectionsPage', () => {
  beforeEach(() => {
    window.localStorage.clear()
    mocks.listCollectionTasksMock.mockReset()
    mocks.generateCollectionTasksMock.mockReset()
    mocks.assignCollectionTaskMock.mockReset()
    mocks.updateCollectionTaskStatusMock.mockReset()
    mocks.fetchOwnerLookupMock.mockReset()
    mocks.fetchUserLookupMock.mockReset()
    mocks.mapOwnerOptionsMock.mockReset()

    mocks.listCollectionTasksMock.mockResolvedValue([
      {
        taskId: 'task-001',
        customerTaxCode: '0101234567',
        customerName: 'Cong ty Alpha',
        ownerId: 'owner-1',
        ownerName: 'Owner 1',
        totalOutstanding: 2000000,
        overdueAmount: 1500000,
        maxDaysPastDue: 35,
        predictedOverdueProbability: 0.76,
        riskLevel: 'HIGH',
        aiSignal: 'OVERDUE_RISK',
        priorityScore: 0.88,
        status: 'OPEN',
        assignedTo: null,
        note: null,
        createdAt: '2026-03-01T01:00:00Z',
        updatedAt: '2026-03-01T01:00:00Z',
        completedAt: null,
      },
    ])
    mocks.generateCollectionTasksMock.mockResolvedValue({
      created: 1,
      candidates: 1,
      minPriorityScore: 0.35,
      tasks: [],
    })
    mocks.assignCollectionTaskMock.mockResolvedValue({})
    mocks.updateCollectionTaskStatusMock.mockResolvedValue({})
    mocks.fetchOwnerLookupMock.mockResolvedValue([])
    mocks.fetchUserLookupMock.mockResolvedValue([])
    mocks.mapOwnerOptionsMock.mockReturnValue([])
  })

  it('loads queue and renders collection tasks', async () => {
    renderPage(['Admin'])

    await waitFor(() => {
      expect(mocks.listCollectionTasksMock).toHaveBeenCalledTimes(1)
    })
    expect(screen.getByText('Cong ty Alpha')).toBeInTheDocument()
    expect(screen.getByText('0101234567')).toBeInTheDocument()
    expect(screen.getByText('Workboard thu hồi công nợ')).toBeInTheDocument()
  })

  it('opens assign popup from action column and saves assignee', async () => {
    const user = userEvent.setup()
    mocks.fetchUserLookupMock.mockResolvedValueOnce([
      { id: 'user-1', username: 'collector01', name: 'Nhân sự Thu nợ 1' },
    ])

    renderPage(['Admin'])

    await waitFor(() => {
      expect(mocks.listCollectionTasksMock).toHaveBeenCalled()
    })

    await user.click(screen.getByRole('button', { name: /Mở popup giao việc 0101234567/i }))

    const dialog = screen.getByRole('dialog', { name: 'Giao người phụ trách' })
    await user.selectOptions(within(dialog).getByRole('combobox', { name: 'Người phụ trách' }), 'user-1')
    await user.click(within(dialog).getByRole('button', { name: 'Lưu phân công' }))

    await waitFor(() => {
      expect(mocks.assignCollectionTaskMock).toHaveBeenCalledWith('token', 'task-001', {
        assignedTo: 'user-1',
      })
    })
  })

  it('opens status popup and updates status with note', async () => {
    const user = userEvent.setup()
    renderPage(['Admin'])

    await waitFor(() => {
      expect(mocks.listCollectionTasksMock).toHaveBeenCalled()
    })

    await user.click(screen.getByRole('button', { name: /Mở popup cập nhật 0101234567/i }))

    const dialog = screen.getByRole('dialog', { name: 'Cập nhật trạng thái thu hồi' })
    await user.selectOptions(within(dialog).getByRole('combobox', { name: 'Trạng thái' }), 'DONE')
    const noteInput = within(dialog).getByRole('textbox', { name: 'Ghi chú xử lý' })
    await user.clear(noteInput)
    await user.type(noteInput, 'Khách đã chuyển khoản')
    await user.click(within(dialog).getByRole('button', { name: 'Lưu trạng thái' }))

    await waitFor(() => {
      expect(mocks.updateCollectionTaskStatusMock).toHaveBeenCalledWith('token', 'task-001', {
        status: 'DONE',
        note: 'Khách đã chuyển khoản',
      })
    })
  })

  it('generates queue from risk signals for manager roles', async () => {
    const user = userEvent.setup()
    renderPage(['Supervisor'])

    await waitFor(() => {
      expect(mocks.listCollectionTasksMock).toHaveBeenCalled()
    })

    await user.click(screen.getByRole('button', { name: 'Tạo queue' }))

    await waitFor(() => {
      expect(mocks.generateCollectionTasksMock).toHaveBeenCalledWith(
        'token',
        expect.objectContaining({
          asOfDate: undefined,
          ownerId: undefined,
          take: 30,
          minPriorityScore: 0.35,
        }),
      )
    })
    expect(screen.getByText(/Tạo mới 1 task/)).toBeInTheDocument()
  })

  it('shows explanatory message when generate creates 0 tasks', async () => {
    const user = userEvent.setup()
    mocks.generateCollectionTasksMock.mockResolvedValueOnce({
      created: 0,
      candidates: 23,
      minPriorityScore: 0.35,
      tasks: [],
    })

    renderPage(['Supervisor'])

    await waitFor(() => {
      expect(mocks.listCollectionTasksMock).toHaveBeenCalled()
    })

    await user.click(screen.getByRole('button', { name: 'Tạo queue' }))

    await waitFor(() => {
      expect(mocks.generateCollectionTasksMock).toHaveBeenCalled()
    })

    expect(screen.getByText(/Tạo mới 0 task trên 23 khách hàng rủi ro/)).toBeInTheDocument()
    expect(
      screen.getByText(/Không có task mới được thêm/i),
    ).toBeInTheDocument()
  })

  it('hides generate queue block for read-only users', async () => {
    renderPage(['Viewer'])

    await waitFor(() => {
      expect(mocks.listCollectionTasksMock).toHaveBeenCalled()
    })
    expect(screen.queryByText('Tạo queue từ cảnh báo rủi ro')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Tạo queue' })).not.toBeInTheDocument()
  })
})
