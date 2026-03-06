import type { ChangeEvent, MouseEvent } from 'react'
import LookupInput from '../../components/LookupInput'
import type { LookupOption } from '../../api/lookups'

export type ReportPreset = {
  id: string
  label: string
}

export type ReportsFilterState = {
  from: string
  to: string
  asOfDate: string
  sellerTaxCode: string
  customerTaxCode: string
  ownerId: string
  groupBy: string
  filterText: string
}

export type ReportsFiltersProps = {
  filter: ReportsFilterState
  useCustomAsOf: boolean
  sellerOptions: LookupOption[]
  customerOptions: LookupOption[]
  ownerOptions: LookupOption[]
  presets: ReportPreset[]
  filterChips: { label: string; value: string }[]
  loadingAction: string
  onFromChange: (value: string) => void
  onToChange: (value: string) => void
  onAsOfChange: (value: string) => void
  onToggleCustomAsOf: (value: boolean) => void
  onSellerChange: (value: string) => void
  onCustomerChange: (value: string) => void
  onOwnerChange: (value: string) => void
  onGroupByChange: (value: string) => void
  onFilterTextChange: (value: string) => void
  onPresetSelect: (presetId: string) => void
  onResetFilters: () => void
  onLoadOverview: () => void
  onLoadSummary: () => void
  onLoadStatement: () => void
  onLoadAging: () => void
  onExport: () => void
}

export function ReportsFilters({
  filter,
  useCustomAsOf,
  sellerOptions,
  customerOptions,
  ownerOptions,
  presets,
  filterChips,
  loadingAction,
  onFromChange,
  onToChange,
  onAsOfChange,
  onToggleCustomAsOf,
  onSellerChange,
  onCustomerChange,
  onOwnerChange,
  onGroupByChange,
  onFilterTextChange,
  onPresetSelect,
  onResetFilters,
  onLoadOverview,
  onLoadSummary,
  onLoadStatement,
  onLoadAging,
  onExport,
}: ReportsFiltersProps) {
  const handleFromChange = (event: ChangeEvent<HTMLInputElement>) => {
    onFromChange(event.target.value)
  }

  const handleToChange = (event: ChangeEvent<HTMLInputElement>) => {
    onToChange(event.target.value)
  }

  const handleAsOfChange = (event: ChangeEvent<HTMLInputElement>) => {
    onAsOfChange(event.target.value)
  }

  const handleAsOfToggle = (event: ChangeEvent<HTMLInputElement>) => {
    onToggleCustomAsOf(event.target.checked)
  }

  const handleOwnerChange = (event: ChangeEvent<HTMLSelectElement>) => {
    onOwnerChange(event.target.value)
  }

  const handleGroupByChange = (event: ChangeEvent<HTMLSelectElement>) => {
    onGroupByChange(event.target.value)
  }

  const handleFilterTextChange = (event: ChangeEvent<HTMLInputElement>) => {
    onFilterTextChange(event.target.value)
  }

  const handlePresetClick = (event: MouseEvent<HTMLButtonElement>) => {
    const presetId = event.currentTarget.dataset.presetId
    if (presetId) {
      onPresetSelect(presetId)
    }
  }

  const handleResetClick = () => {
    onResetFilters()
  }

  const handleLoadOverviewClick = () => {
    onLoadOverview()
  }

  const handleLoadSummaryClick = () => {
    onLoadSummary()
  }

  const handleLoadStatementClick = () => {
    onLoadStatement()
  }

  const handleLoadAgingClick = () => {
    onLoadAging()
  }

  const handleExportClick = () => {
    onExport()
  }

  const isExporting = loadingAction.startsWith('export')
  const isLoadingData = ['overview', 'summary', 'statement', 'aging'].includes(loadingAction)
  const statusLabel = isExporting
    ? 'Đang xuất báo cáo'
    : isLoadingData
    ? 'Đang tải dữ liệu'
    : 'Sẵn sàng'

  return (
    <section className="card reports-filters" id="filters" aria-labelledby="reports-filters-title">
      <div className="reports-filters__header">
        <div>
          <h3 id="reports-filters-title">Bộ lọc báo cáo</h3>
          <p className="muted">Chọn kỳ thời gian, phạm vi dữ liệu và tải từng nhóm báo cáo.</p>
        </div>
        <span
          className={`reports-filters__status-pill${
            loadingAction ? ' reports-filters__status-pill--active' : ''
          }`}
        >
          {statusLabel}
        </span>
      </div>

      <div className="reports-filters__date-grid">
        <div className="filters-block reports-filters__block">
          <div className="filters-block__title">Kỳ báo cáo</div>
          <div className="filters-row">
            <label className="field">
              <span>Từ ngày</span>
              <input
                type="date"
                value={filter.from}
                onChange={handleFromChange}
                placeholder="DD/MM/YYYY"
              />
            </label>
            <label className="field">
              <span>Đến ngày</span>
              <input
                type="date"
                value={filter.to}
                onChange={handleToChange}
                placeholder="DD/MM/YYYY"
              />
            </label>
          </div>
        </div>

        <div className="filters-block reports-filters__block">
          <div className="filters-block__title">Ngày chốt (Tổng quan &amp; Tuổi nợ)</div>
          <label className="field field--checkbox">
            <input type="checkbox" checked={useCustomAsOf} onChange={handleAsOfToggle} />
            <span>Dùng ngày chốt khác</span>
          </label>
          {useCustomAsOf && (
            <label className="field">
              <span>Tính đến ngày</span>
              <input
                type="date"
                value={filter.asOfDate}
                onChange={handleAsOfChange}
                placeholder="DD/MM/YYYY"
              />
            </label>
          )}
          <span className="muted">Mặc định lấy theo Đến ngày nếu không chọn riêng.</span>
        </div>
      </div>

      <div className="filters-grid reports-filters__grid">
        <LookupInput
          label="MST bên bán"
          value={filter.sellerTaxCode}
          placeholder="MST bên bán"
          options={sellerOptions}
          onChange={onSellerChange}
        />
        <LookupInput
          label="MST bên mua"
          value={filter.customerTaxCode}
          placeholder="MST bên mua"
          options={customerOptions}
          onChange={onCustomerChange}
        />
        <label className="field">
          <span>Phụ trách</span>
          <select value={filter.ownerId} onChange={handleOwnerChange}>
            <option value="">Tất cả</option>
            {ownerOptions.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
          <span className="muted">Chọn người phụ trách để lọc báo cáo.</span>
        </label>
        <label className="field">
          <span>Nhóm theo</span>
          <select value={filter.groupBy} onChange={handleGroupByChange}>
            <option value="customer">Khách hàng</option>
            <option value="seller">Bên bán</option>
            <option value="owner">Phụ trách</option>
            <option value="period">Kỳ</option>
          </select>
        </label>
        <label className="field">
          <span>Từ khóa</span>
          <input
            value={filter.filterText}
            onChange={handleFilterTextChange}
            placeholder="lọc theo từ khóa"
          />
        </label>
      </div>

      <div className="inline-actions reports-filters__presets">
        <span className="muted">Preset nhanh:</span>
        {presets.map((preset) => (
          <button
            key={preset.id}
            className="btn btn-ghost"
            type="button"
            data-preset-id={preset.id}
            onClick={handlePresetClick}
          >
            {preset.label}
          </button>
        ))}
        <button className="btn btn-ghost" type="button" onClick={handleResetClick}>
          Xóa lọc
        </button>
      </div>

      <div className="filter-chips reports-filters__chips">
        {filterChips.length > 0 ? (
          filterChips.map((chip) => (
            <span className="filter-chip" key={`${chip.label}-${chip.value}`}>
              <strong>{chip.label}:</strong> {chip.value}
            </span>
          ))
        ) : (
          <span className="filter-chip">Chưa có bộ lọc.</span>
        )}
      </div>

      <div className="reports-filters__action-bar">
        <div className="reports-filters__actions-primary">
          <button
            className="btn btn-primary"
            type="button"
            onClick={handleLoadOverviewClick}
            disabled={loadingAction === 'overview'}
          >
            {loadingAction === 'overview' ? 'Đang tải...' : 'Tải tổng quan'}
          </button>
          <button
            className="btn btn-outline"
            type="button"
            onClick={handleLoadSummaryClick}
            disabled={loadingAction === 'summary'}
          >
            {loadingAction === 'summary' ? 'Đang tải...' : 'Tải tổng hợp'}
          </button>
          <button
            className="btn btn-outline"
            type="button"
            onClick={handleLoadStatementClick}
            disabled={loadingAction === 'statement'}
          >
            {loadingAction === 'statement' ? 'Đang tải...' : 'Tải sao kê'}
          </button>
          <button
            className="btn btn-outline"
            type="button"
            onClick={handleLoadAgingClick}
            disabled={loadingAction === 'aging'}
          >
            {loadingAction === 'aging' ? 'Đang tải...' : 'Tải tuổi nợ'}
          </button>
        </div>
        <div className="reports-filters__actions-secondary">
          <button
            className="btn btn-ghost"
            type="button"
            onClick={handleExportClick}
            disabled={isExporting}
          >
            {isExporting ? 'Đang xuất...' : 'Xuất Excel'}
          </button>
        </div>
      </div>
    </section>
  )
}
