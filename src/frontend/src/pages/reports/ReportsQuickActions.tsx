type ReportsQuickAction = {
  id: string
  label: string
}

type ReportsQuickActionsProps = {
  open: boolean
  actions: ReportsQuickAction[]
  onToggle: () => void
  onNavigate: (id: string) => void
}

export function ReportsQuickActions({
  open,
  actions,
  onToggle,
  onNavigate,
}: ReportsQuickActionsProps) {
  return (
    <div className="reports-quick-actions" aria-label="Điều hướng nhanh báo cáo">
      <button
        type="button"
        className={`btn btn-ghost reports-quick-actions__toggle${
          open ? ' reports-quick-actions__toggle--open' : ''
        }`}
        onClick={onToggle}
        aria-expanded={open}
        aria-controls="reports-quick-actions-panel"
        aria-label={open ? 'Ẩn điều hướng nhanh' : 'Hiện điều hướng nhanh'}
      >
        {open ? 'Ẩn' : 'Điều hướng'}
      </button>
      {open && (
        <div className="reports-quick-actions__panel" id="reports-quick-actions-panel">
          {actions.map((action) => (
            <button
              key={action.id}
              type="button"
              className="reports-quick-actions__item"
              onClick={() => onNavigate(action.id)}
            >
              {action.label}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
