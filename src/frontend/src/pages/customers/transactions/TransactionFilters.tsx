import { quickRangeOptions } from './constants'

type StatusOption = { value: string; label: string }

type TransactionFiltersProps = {
  searchLabel: string
  searchValue: string
  searchPlaceholder?: string
  searchTooltip?: string
  onSearchChange: (value: string) => void
  dateFrom: string
  dateTo: string
  onDateFromChange: (value: string) => void
  onDateToChange: (value: string) => void
  quickRange: string
  onQuickRangeChange: (value: string) => void
  statusValue: string
  statusOptions: StatusOption[]
  onStatusChange: (value: string) => void
  hasFilters: boolean
  onClear: () => void
  helperText: string
}

export default function TransactionFilters({
  searchLabel,
  searchValue,
  searchPlaceholder,
  searchTooltip,
  onSearchChange,
  dateFrom,
  dateTo,
  onDateFromChange,
  onDateToChange,
  quickRange,
  onQuickRangeChange,
  statusValue,
  statusOptions,
  onStatusChange,
  hasFilters,
  onClear,
  helperText,
}: TransactionFiltersProps) {
  return (
    <>
      <div className="filters-grid filters-grid--compact filters-grid--transactions">
        <label className="field">
          <span className="field-label">
            {searchLabel}
            {searchTooltip && (
              <span className="info-tip" title={searchTooltip} aria-label={searchTooltip}>
                i
              </span>
            )}
          </span>
          <input
            className="input--search"
            value={searchValue}
            onChange={(event) => onSearchChange(event.target.value)}
            placeholder={searchPlaceholder}
          />
        </label>
        <label className="field field--compact">
          <span>Từ ngày</span>
          <input type="date" value={dateFrom} onChange={(event) => onDateFromChange(event.target.value)} />
        </label>
        <label className="field field--compact">
          <span>Đến ngày</span>
          <input type="date" value={dateTo} onChange={(event) => onDateToChange(event.target.value)} />
        </label>
        <label className="field field--compact">
          <span>Chọn nhanh</span>
          <select value={quickRange} onChange={(event) => onQuickRangeChange(event.target.value)}>
            {quickRangeOptions.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </label>
        <label className="field field--compact">
          <span>Trạng thái</span>
          <select value={statusValue} onChange={(event) => onStatusChange(event.target.value)}>
            <option value="">Tất cả</option>
            {statusOptions.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </label>
      </div>
      <div className="filters-actions">
        {hasFilters ? (
          <button className="btn btn-outline btn-table" type="button" onClick={onClear}>
            Xóa lọc
          </button>
        ) : (
          <span className="muted">{helperText}</span>
        )}
      </div>
    </>
  )
}
