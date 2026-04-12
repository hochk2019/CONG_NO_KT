import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { describe, expect, it, vi } from 'vitest'
import type { AdvanceListItem } from '../../../api/advances'
import { buildManualAdvanceColumns } from '../manualAdvancesColumns'

const baseRow: AdvanceListItem = {
  id: 'adv-1',
  status: 'DRAFT',
  version: 1,
  advanceNo: 'TH-001',
  advanceDate: '2026-02-12',
  amount: 100000,
  outstandingAmount: 100000,
  sellerTaxCode: '0312345678',
  customerTaxCode: '0101234567',
  description: 'ghi chu',
  customerName: 'ACME',
  ownerName: 'Owner',
  sourceType: 'MANUAL',
  canManage: true,
}

const buildColumns = (handlers?: Partial<Parameters<typeof buildManualAdvanceColumns>[0]>) =>
  buildManualAdvanceColumns({
    onOpenCorrection: vi.fn(),
    onOpenHistory: vi.fn(),
    onApprove: vi.fn(),
    onVoid: vi.fn(),
    onUnvoid: vi.fn(),
    revealedCell: null,
    onToggleReveal: vi.fn(),
    onOpenAdvance: vi.fn(),
    onOpenCustomer: vi.fn(),
    loadingAction: '',
    ...handlers,
  })

describe('manualAdvancesColumns', () => {
  it('opens correction and history actions for editable rows', async () => {
    const user = userEvent.setup()
    const onOpenCorrection = vi.fn()
    const onOpenHistory = vi.fn()
    const columns = buildColumns({ onOpenCorrection, onOpenHistory })
    const actionColumn = columns.find((col) => col.key === 'actions')
    const renderAction = actionColumn?.render as ((row: AdvanceListItem) => ReactNode) | undefined
    expect(renderAction).toBeTypeOf('function')

    const { container } = render(<>{renderAction!(baseRow)}</>)

    expect(container.querySelector('.advances-row-actions')).not.toBeNull()
    expect(container.querySelectorAll('.advances-row-actions__button')).toHaveLength(4)

    await user.click(screen.getByRole('button', { name: 'Sửa' }))
    await user.click(screen.getByRole('button', { name: 'Lịch sử sửa' }))

    expect(onOpenCorrection).toHaveBeenCalledWith(baseRow)
    expect(onOpenHistory).toHaveBeenCalledWith(baseRow)
  })

  it('renders unvoid action for VOID status', async () => {
    const user = userEvent.setup()
    const onUnvoid = vi.fn()
    const columns = buildColumns({ onUnvoid })
    const actionColumn = columns.find((col) => col.key === 'actions')
    const renderAction = actionColumn?.render as ((row: AdvanceListItem) => ReactNode) | undefined
    expect(renderAction).toBeTypeOf('function')

    render(<>{renderAction!({ ...baseRow, status: 'VOID' } as AdvanceListItem)}</>)

    const unvoidButton = screen.getByRole('button', { name: 'Bỏ hủy' })
    expect(unvoidButton).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Sửa' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Lịch sử sửa' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Hủy' })).not.toBeInTheDocument()

    await user.click(unvoidButton)
    expect(onUnvoid).toHaveBeenCalledTimes(1)
  })

  it('renders void action for non-VOID status', () => {
    const columns = buildColumns()
    const actionColumn = columns.find((col) => col.key === 'actions')
    const renderAction = actionColumn?.render as ((row: AdvanceListItem) => ReactNode) | undefined
    expect(renderAction).toBeTypeOf('function')

    render(<>{renderAction!(baseRow)}</>)

    expect(screen.getByRole('button', { name: 'Hủy' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Bỏ hủy' })).not.toBeInTheDocument()
  })

  it('renders muted placeholder when row is not manageable', () => {
    const columns = buildColumns()
    const actionColumn = columns.find((col) => col.key === 'actions')
    const renderAction = actionColumn?.render as ((row: AdvanceListItem) => ReactNode) | undefined
    expect(renderAction).toBeTypeOf('function')

    render(<>{renderAction!({ ...baseRow, canManage: false })}</>)

    expect(screen.getByText('-')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Sửa' })).not.toBeInTheDocument()
  })

  it('reveals and opens advance deeplink from advance number cell', async () => {
    const user = userEvent.setup()
    const onToggleReveal = vi.fn()
    const onOpenAdvance = vi.fn()
    const columns = buildColumns({
      revealedCell: { rowId: 'adv-1', target: 'advance' },
      onToggleReveal,
      onOpenAdvance,
    })
    const column = columns.find((col) => col.key === 'advanceNo')
    const renderCell = column?.render as ((row: AdvanceListItem) => ReactNode) | undefined

    expect(renderCell).toBeTypeOf('function')
    render(<>{renderCell!(baseRow)}</>)

    await user.click(screen.getByRole('button', { name: 'Mở liên kết chứng từ TH-001' }))
    await user.click(screen.getByRole('button', { name: 'Xem chứng từ TH-001' }))

    expect(onToggleReveal).toHaveBeenCalledWith('adv-1', 'advance')
    expect(onOpenAdvance).toHaveBeenCalledWith('0101234567', 'TH-001')
  })

  it('reveals and opens customer deeplink from customer cell', async () => {
    const user = userEvent.setup()
    const onToggleReveal = vi.fn()
    const onOpenCustomer = vi.fn()
    const columns = buildColumns({
      revealedCell: { rowId: 'adv-1', target: 'customer' },
      onToggleReveal,
      onOpenCustomer,
    })
    const column = columns.find((col) => col.key === 'customer')
    const renderCell = column?.render as ((row: AdvanceListItem) => ReactNode) | undefined

    expect(renderCell).toBeTypeOf('function')
    render(<>{renderCell!(baseRow)}</>)

    await user.click(screen.getByRole('button', { name: 'Mở liên kết khách hàng ACME' }))
    await user.click(screen.getByRole('button', { name: 'Xem khách hàng 0101234567' }))

    expect(onToggleReveal).toHaveBeenCalledWith('adv-1', 'customer')
    expect(onOpenCustomer).toHaveBeenCalledWith('0101234567')
  })

  it('keeps plain text fallback when advance deeplink data is incomplete', () => {
    const columns = buildColumns({
      revealedCell: { rowId: 'adv-1', target: 'advance' },
    })
    const advanceColumn = columns.find((col) => col.key === 'advanceNo')
    const customerColumn = columns.find((col) => col.key === 'customer')
    const renderAdvance = advanceColumn?.render as ((row: AdvanceListItem) => ReactNode) | undefined
    const renderCustomer = customerColumn?.render as ((row: AdvanceListItem) => ReactNode) | undefined
    const incompleteRow = {
      ...baseRow,
      customerTaxCode: '',
    }

    expect(renderAdvance).toBeTypeOf('function')
    expect(renderCustomer).toBeTypeOf('function')

    render(
      <>
        {renderAdvance!(incompleteRow)}
        {renderCustomer!(incompleteRow)}
      </>,
    )

    expect(document.body).toHaveTextContent('TH-001')
    expect(document.body).toHaveTextContent('ACME')
    expect(screen.queryByRole('button', { name: 'Mở liên kết chứng từ TH-001' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Mở liên kết khách hàng ACME' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Xem/ })).not.toBeInTheDocument()
  })
})
