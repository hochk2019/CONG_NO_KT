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
    editingId: null,
    editingDescription: '',
    setEditingDescription: vi.fn(),
    onStartEdit: vi.fn(),
    onSaveEdit: vi.fn(),
    onCancelEdit: vi.fn(),
    onApprove: vi.fn(),
    onVoid: vi.fn(),
    onUnvoid: vi.fn(),
    loadingAction: '',
    ...handlers,
  })

describe('manualAdvancesColumns', () => {
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
})
