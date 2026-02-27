export const dashboardWidgetIds = [
  'executiveSummary',
  'roleCockpit',
  'kpis',
  'cashflow',
  'panels',
] as const

export type DashboardWidgetId = (typeof dashboardWidgetIds)[number]

export const defaultDashboardWidgetOrder: DashboardWidgetId[] = [...dashboardWidgetIds]

export const dashboardWidgetLabels: Record<DashboardWidgetId, { title: string; description: string }> = {
  executiveSummary: {
    title: 'Tóm tắt điều hành',
    description: 'Thông điệp ưu tiên và gợi ý hành động',
  },
  roleCockpit: {
    title: 'Cockpit theo vai trò',
    description: 'Ưu tiên quyết định và luồng thao tác theo vai trò',
  },
  kpis: {
    title: 'Chỉ số KPI',
    description: 'Tổng dư, quá hạn và biến động tháng',
  },
  cashflow: {
    title: 'Dòng tiền',
    description: 'Expected vs Actual và dự báo',
  },
  panels: {
    title: 'Panel phân tích',
    description: 'Top khách hàng và quá hạn theo phụ trách',
  },
}

export const isDashboardWidgetId = (value: string): value is DashboardWidgetId =>
  dashboardWidgetIds.some((widgetId) => widgetId === value)

export const normalizeDashboardWidgetOrder = (items: readonly string[]): DashboardWidgetId[] => {
  const seen = new Set<DashboardWidgetId>()
  const result: DashboardWidgetId[] = []

  for (const item of items) {
    if (!isDashboardWidgetId(item) || seen.has(item)) continue
    seen.add(item)
    result.push(item)
  }

  for (const item of dashboardWidgetIds) {
    if (!seen.has(item)) {
      result.push(item)
    }
  }

  return result
}

export const normalizeDashboardHiddenWidgets = (items: readonly string[]): DashboardWidgetId[] => {
  const seen = new Set<DashboardWidgetId>()
  const result: DashboardWidgetId[] = []

  for (const item of items) {
    if (!isDashboardWidgetId(item) || seen.has(item)) continue
    seen.add(item)
    result.push(item)
  }

  return result
}

export const moveDashboardWidget = (
  order: readonly DashboardWidgetId[],
  widgetId: DashboardWidgetId,
  direction: 'up' | 'down',
) => {
  const index = order.indexOf(widgetId)
  if (index < 0) return [...order]
  const nextIndex = direction === 'up' ? index - 1 : index + 1
  if (nextIndex < 0 || nextIndex >= order.length) return [...order]

  const result = [...order]
  const [item] = result.splice(index, 1)
  result.splice(nextIndex, 0, item)
  return result
}
