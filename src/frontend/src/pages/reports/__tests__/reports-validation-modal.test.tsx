import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ReportsValidationModal } from '../ReportsValidationModal'

describe('ReportsValidationModal', () => {
  it('renders title/message and closes', async () => {
    const user = userEvent.setup()
    const onClose = vi.fn()

    render(
      <ReportsValidationModal
        open
        title="Thiếu khoảng thời gian"
        message="Vui lòng chọn Từ ngày và Đến ngày."
        onClose={onClose}
      />,
    )

    expect(screen.getByRole('dialog')).toBeInTheDocument()
    expect(screen.getByText('Thiếu khoảng thời gian')).toBeInTheDocument()
    expect(screen.getByText('Vui lòng chọn Từ ngày và Đến ngày.')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Đóng hộp thoại' }))
    expect(onClose).toHaveBeenCalledTimes(1)

    await user.click(screen.getByRole('button', { name: 'Đóng' }))
    expect(onClose).toHaveBeenCalledTimes(2)
  })
})
