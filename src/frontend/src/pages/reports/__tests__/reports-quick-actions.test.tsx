import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ReportsQuickActions } from '../ReportsQuickActions'

describe('ReportsQuickActions', () => {
  it('toggles panel and navigates', async () => {
    const user = userEvent.setup()
    const onToggle = vi.fn()
    const onNavigate = vi.fn()

    render(
      <ReportsQuickActions
        open
        actions={[
          { id: 'filters', label: 'Bộ lọc' },
          { id: 'summary', label: 'Báo cáo tổng hợp' },
        ]}
        onToggle={onToggle}
        onNavigate={onNavigate}
      />,
    )

    expect(screen.getByText('Bộ lọc')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /Ẩn điều hướng/ }))
    expect(onToggle).toHaveBeenCalledTimes(1)

    await user.click(screen.getByRole('button', { name: 'Báo cáo tổng hợp' }))
    expect(onNavigate).toHaveBeenCalledWith('summary')
  })
})
