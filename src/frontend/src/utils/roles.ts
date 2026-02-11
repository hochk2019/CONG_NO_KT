export const ROLE_LABELS: Record<string, string> = {
  Admin: 'Quản trị',
  Supervisor: 'Giám sát',
  Accountant: 'Kế toán',
  Viewer: 'Chỉ xem',
}

export const resolveRoleLabel = (code: string, name?: string | null) => {
  return ROLE_LABELS[code] ?? (name ? ROLE_LABELS[name] ?? name : code)
}

export const formatRoleDisplay = (code: string, name?: string | null) => {
  const label = resolveRoleLabel(code, name)
  return label !== code ? `${code} (${label})` : code
}
