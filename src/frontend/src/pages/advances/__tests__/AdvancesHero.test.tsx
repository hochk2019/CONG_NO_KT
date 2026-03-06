import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import AdvancesHero from '../AdvancesHero'

describe('AdvancesHero', () => {
  it('renders compact workspace framing, workflow notes, and primary actions', () => {
    render(<AdvancesHero onImportTemplate={vi.fn()} />)

    expect(screen.getByText('Khoản trả hộ KH')).toBeInTheDocument()
    expect(
      screen.getByRole('heading', {
        level: 2,
        name: 'Workspace nhập liệu và xử lý khoản trả hộ KH',
      }),
    ).toBeInTheDocument()
    expect(screen.getByText('Nhập nhanh')).toBeInTheDocument()
    expect(screen.getByText('Xử lý tại dòng')).toBeInTheDocument()
    expect(screen.getByText('Import đúng chỗ')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Xem danh sách' })).toHaveAttribute(
      'href',
      '#advances-worklist',
    )
  })

  it('calls import handler from the primary CTA', async () => {
    const user = userEvent.setup()
    const onImportTemplate = vi.fn()

    render(<AdvancesHero onImportTemplate={onImportTemplate} />)

    await user.click(screen.getByRole('button', { name: 'Import từ template' }))

    expect(onImportTemplate).toHaveBeenCalledTimes(1)
  })
})
