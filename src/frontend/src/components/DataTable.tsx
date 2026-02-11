import type { CSSProperties, ReactNode } from 'react'

type Column<T> = {
  key: string
  label: string
  render?: (row: T) => ReactNode
  align?: 'left' | 'center' | 'right'
  sortable?: boolean
  width?: string
}

type SortState = {
  key: string
  direction: 'asc' | 'desc'
}

type Pagination = {
  page: number
  pageSize: number
  total: number
}

type DataTableProps<T> = {
  columns: Column<T>[]
  rows: T[]
  getRowKey: (row: T, index: number) => string
  getRowClassName?: (row: T, index: number) => string | undefined
  emptyMessage?: string
  minWidth?: string
  sort?: SortState
  onSort?: (next: SortState) => void
  pagination?: Pagination
  onPageChange?: (page: number) => void
  onPageSizeChange?: (pageSize: number) => void
}

const pageSizes = [10, 20, 50, 100]

export default function DataTable<T>({
  columns,
  rows,
  getRowKey,
  getRowClassName,
  emptyMessage = 'Không có dữ liệu.',
  minWidth,
  sort,
  onSort,
  pagination,
  onPageChange,
  onPageSizeChange,
}: DataTableProps<T>) {
  const handleSort = (key: string, sortable?: boolean) => {
    if (!sortable || !onSort) {
      return
    }
    if (!sort || sort.key !== key) {
      onSort({ key, direction: 'asc' })
      return
    }
    onSort({ key, direction: sort.direction === 'asc' ? 'desc' : 'asc' })
  }

  const tableStyle: CSSProperties = {
    '--table-columns': columns.length,
    ...(minWidth ? { '--table-min-width': minWidth } : null),
  } as CSSProperties

  const totalPages = pagination
    ? Math.max(1, Math.ceil(pagination.total / pagination.pageSize))
    : 1

  return (
    <div>
      <div className="table-scroll">
        <table className="table" style={tableStyle}>
          <thead className="table-head">
            <tr className="table-row">
              {columns.map((column) => {
                const isActive = sort?.key === column.key
                const indicator = isActive ? (sort?.direction === 'asc' ? '^' : 'v') : ''
                const sortState = isActive
                  ? sort?.direction === 'asc'
                    ? 'ascending'
                    : 'descending'
                  : 'none'
                const ariaSort = column.sortable ? sortState : 'none'
                const ariaLabel = column.sortable ? `Sắp xếp theo ${column.label}` : column.label
                const align = column.align ?? 'left'
                const justifyContent =
                  align === 'right' ? 'flex-end' : align === 'center' ? 'center' : 'flex-start'
                const widthStyle = column.width
                  ? ({ width: column.width, minWidth: column.width } as CSSProperties)
                  : undefined
                const headerStyle = {
                  textAlign: align,
                  ...widthStyle,
                } as CSSProperties
                return (
                  <th key={column.key} scope="col" aria-sort={ariaSort} style={headerStyle}>
                    <button
                      className="table-sort"
                      type="button"
                      aria-label={ariaLabel}
                      disabled={!column.sortable || !onSort}
                      onClick={() => handleSort(column.key, column.sortable)}
                      style={{ textAlign: align, justifyContent }}
                    >
                      <span>{column.label}</span>
                      {indicator && <span className="table-sort__indicator">{indicator}</span>}
                    </button>
                  </th>
                )
              })}
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 ? (
              <tr className="table-row table-empty">
                <td colSpan={columns.length}>
                  <div className="empty-state">{emptyMessage}</div>
                </td>
              </tr>
            ) : (
              rows.map((row, index) => (
                <tr
                  className={`table-row${getRowClassName ? ` ${getRowClassName(row, index) ?? ''}` : ''}`}
                  key={getRowKey(row, index)}
                >
                  {columns.map((column) => {
                    const widthStyle = column.width
                      ? ({ width: column.width, minWidth: column.width } as CSSProperties)
                      : undefined
                    return (
                    <td
                      key={column.key}
                      style={{ textAlign: column.align ?? 'left', ...widthStyle }}
                    >
                      {column.render
                        ? column.render(row)
                        : (row as Record<string, ReactNode>)[column.key]}
                    </td>
                    )
                  })}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {pagination && onPageChange && (
        <div className="table-controls">
          <div className="table-page-info">
            Trang {pagination.page} / {totalPages} (Tổng {pagination.total})
          </div>
          <div className="table-page-actions">
            <button
              className="btn btn-ghost"
              type="button"
              onClick={() => onPageChange(Math.max(1, pagination.page - 1))}
              disabled={pagination.page <= 1}
            >
              Trước
            </button>
            <button
              className="btn btn-ghost"
              type="button"
              onClick={() => onPageChange(Math.min(totalPages, pagination.page + 1))}
              disabled={pagination.page >= totalPages}
            >
              Sau
            </button>
          </div>
          {onPageSizeChange && (
            <label className="table-page-size">
              <span>Kích thước trang</span>
              <select
                value={pagination.pageSize}
                onChange={(event) => onPageSizeChange(Number(event.target.value))}
              >
                {pageSizes.map((size) => (
                  <option key={size} value={size}>
                    {size}
                  </option>
                ))}
              </select>
            </label>
          )}
        </div>
      )}
    </div>
  )
}
