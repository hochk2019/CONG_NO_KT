import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import ActionConfirmModal from '../ActionConfirmModal'

describe('ActionConfirmModal', () => {
  it('does not render when closed', () => {
    render(
      <ActionConfirmModal
        isOpen={false}
        title="Xác nhận"
        onClose={vi.fn()}
        onConfirm={vi.fn()}
      />,
    )

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('requires reason when reasonRequired=true and submits trimmed payload', async () => {
    const user = userEvent.setup()
    const onConfirm = vi.fn()

    render(
      <ActionConfirmModal
        isOpen
        title="Hủy phiếu thu"
        reasonRequired
        onClose={vi.fn()}
        onConfirm={onConfirm}
      />,
    )

    const submitButton = screen.getByRole('button', { name: 'Xác nhận' })
    expect(submitButton).toBeDisabled()

    await user.type(screen.getByPlaceholderText('Nhập lý do'), '  Hủy nhầm chứng từ  ')
    expect(submitButton).toBeEnabled()

    await user.click(submitButton)

    expect(onConfirm).toHaveBeenCalledWith({
      reason: 'Hủy nhầm chứng từ',
      overridePeriodLock: false,
      overrideReason: undefined,
    })
  })

  it('requires override reason when override lock is enabled', async () => {
    const user = userEvent.setup()
    const onConfirm = vi.fn()

    render(
      <ActionConfirmModal
        isOpen
        title="Bỏ hủy phiếu thu"
        showOverrideOption
        onClose={vi.fn()}
        onConfirm={onConfirm}
      />,
    )

    await user.click(screen.getByText('Tùy chọn nâng cao'))
    await user.click(screen.getByRole('checkbox', { name: 'Vượt khóa kỳ' }))

    const submitButton = screen.getByRole('button', { name: 'Xác nhận' })
    expect(submitButton).toBeDisabled()

    await user.type(screen.getByPlaceholderText('Nhập lý do vượt khóa kỳ'), 'Mở khóa để xử lý')
    expect(submitButton).toBeEnabled()

    await user.click(submitButton)

    expect(onConfirm).toHaveBeenCalledWith({
      reason: '',
      overridePeriodLock: true,
      overrideReason: 'Mở khóa để xử lý',
    })
  })
})
