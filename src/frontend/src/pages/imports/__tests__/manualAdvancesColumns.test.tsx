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
    expect(container.querySelector('.advances-row-actions__lane--commit')).not.toBeNull()

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
})
