import { useContext } from 'react'
import { NotificationCenterContext } from './notification-center-context'

export const useNotificationCenter = () => {
  const ctx = useContext(NotificationCenterContext)
  if (!ctx) {
    throw new Error('NotificationCenterContext not available')
  }
  return ctx
}
