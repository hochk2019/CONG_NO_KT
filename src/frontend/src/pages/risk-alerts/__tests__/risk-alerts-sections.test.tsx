import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { RiskAlertsHeader, RiskNotificationsSection } from '../RiskAlertsSections'

describe('risk alert sections', () => {
  it('renders header title', () => {
    render(
      <RiskAlertsHeader
        onCreateCollectionQueue={() => undefined}
        onSetToday={() => undefined}
        onClearDate={() => undefined}
      />,
    )

    expect(screen.getByText('Rủi ro công nợ & nhắc kế toán')).toBeInTheDocument()
  })

  it('renders empty notification state', () => {
    render(
      <RiskNotificationsSection
        notificationsLoading={false}
        notifications={[]}
        selectedIds={[]}
        bulkLoading={false}
        bulkError={null}
        onSelectedIdsChange={() => undefined}
        onMarkSelectedRead={() => undefined}
        onMarkAllRead={() => undefined}
        onMarkRead={() => undefined}
      />,
    )

    expect(screen.getByText('Không có thông báo mới.')).toBeInTheDocument()
  })

  it('renders bulk actions for notifications', () => {
    const onSelectedIdsChange = vi.fn()
    const onMarkSelectedRead = vi.fn()
    const onMarkAllRead = vi.fn()
    const onMarkRead = vi.fn()

    render(
      <RiskNotificationsSection
        notificationsLoading={false}
        notifications={[
          {
            id: 'n-1',
            title: 'Nhắc kiểm tra rủi ro',
            body: 'Khách hàng ACME có rủi ro cao.',
            severity: 'WARN',
            source: 'RISK',
            createdAt: '2026-03-03T00:00:00.000Z',
            readAt: null,
          },
        ]}
        selectedIds={[]}
        bulkLoading={false}
        bulkError={null}
        onSelectedIdsChange={onSelectedIdsChange}
        onMarkSelectedRead={onMarkSelectedRead}
        onMarkAllRead={onMarkAllRead}
        onMarkRead={onMarkRead}
      />,
    )

    expect(screen.getByText('Đã chọn 0/1 thông báo.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Đã đọc tất cả (1)' })).toBeInTheDocument()
    expect(screen.getByLabelText('Chọn thông báo Nhắc kiểm tra rủi ro')).toBeInTheDocument()
  })
})
