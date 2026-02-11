import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { RiskAlertsHeader, RiskNotificationsSection } from '../RiskAlertsSections'

describe('risk alert sections', () => {
  it('renders header title', () => {
    render(<RiskAlertsHeader onSetToday={() => undefined} onClearDate={() => undefined} />)

    expect(screen.getByText('Rủi ro công nợ & nhắc kế toán')).toBeInTheDocument()
  })

  it('renders empty notification state', () => {
    render(
      <RiskNotificationsSection
        notificationsLoading={false}
        notifications={[]}
        onMarkRead={() => undefined}
      />,
    )

    expect(screen.getByText('Không có thông báo mới.')).toBeInTheDocument()
  })
})
