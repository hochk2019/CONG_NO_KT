import type { CSSProperties } from 'react'
import type {
  ReportAgingRow,
  ReportStatementPagedResult,
  ReportSummaryRow,
} from '../../api/reports'
import { formatDate, formatMoney } from '../../utils/format'

type PaginationState = {
  page: number
  pageSize: number
  total: number
}

type ReportsTablesSectionProps = {
  summaryRows: ReportSummaryRow[]
  statement: ReportStatementPagedResult | null
  agingRows: ReportAgingRow[]
  summaryPagination: PaginationState
  statementPagination: PaginationState
  agingPagination: PaginationState
  summarySortKey: string
  agingSortKey: string
  loadingSummary: boolean
  loadingStatement: boolean
  loadingAging: boolean
  statementCustomerTaxCode?: string
  statementCustomerName?: string
  exportingSummary?: boolean
  exportingSummaryPdf?: boolean
  exportingStatement?: boolean
  exportingAging?: boolean
  onExportSummary?: () => void
  onExportSummaryPdf?: () => void
  onExportStatement?: () => void
  onExportAging?: () => void
  onSummaryPageChange?: (page: number) => void
  onSummaryPageSizeChange?: (pageSize: number) => void
  onSummarySortChange?: (sortKey: string) => void
  onStatementPageChange?: (page: number) => void
  onStatementPageSizeChange?: (pageSize: number) => void
  onAgingPageChange?: (page: number) => void
  onAgingPageSizeChange?: (pageSize: number) => void
  onAgingSortChange?: (sortKey: string) => void
}

export function ReportsTablesSection({
  summaryRows,
  statement,
  agingRows,
  summaryPagination,
  statementPagination,
  agingPagination,
  summarySortKey,
  agingSortKey,
  loadingSummary,
  loadingStatement,
  loadingAging,
  statementCustomerTaxCode,
  statementCustomerName,
  exportingSummary = false,
  exportingSummaryPdf = false,
  exportingStatement = false,
  exportingAging = false,
  onExportSummary,
  onExportSummaryPdf,
  onExportStatement,
  onExportAging,
  onSummaryPageChange,
  onSummaryPageSizeChange,
  onSummarySortChange,
  onStatementPageChange,
  onStatementPageSizeChange,
  onAgingPageChange,
  onAgingPageSizeChange,
  onAgingSortChange,
}: ReportsTablesSectionProps) {
  const shouldShowStatementCustomer =
    Boolean(statementCustomerTaxCode) || Boolean(statementCustomerName)
  const pageSizes = [10, 20, 50, 100]
  const summarySortOptions = [
    { value: '', label: 'Mặc định' },
    { value: 'currentBalance', label: 'Tổng nợ còn lại giảm dần' },
  ]
  const agingSortOptions = [
    { value: '', label: 'Mặc định' },
    { value: 'overdue', label: 'Quá hạn' },
    { value: 'bucket0To30', label: '0-30' },
    { value: 'bucket31To60', label: '31-60' },
    { value: 'bucket61To90', label: '61-90' },
    { value: 'bucket91To180', label: '91-180' },
    { value: 'bucketOver180', label: '>180' },
  ]

  const renderPagination = (
    pagination: PaginationState,
    loading: boolean,
    onPageChange?: (page: number) => void,
    onPageSizeChange?: (pageSize: number) => void,
  ) => {
    if (!onPageChange) return null
    const totalPages = Math.max(1, Math.ceil(pagination.total / pagination.pageSize))
    return (
      <div className="table-controls">
        <div className="table-page-info">
          Trang {pagination.page} / {totalPages} (Tổng {pagination.total})
        </div>
        <div className="table-page-actions">
          <button
            className="btn btn-ghost"
            type="button"
            onClick={() => onPageChange(Math.max(1, pagination.page - 1))}
            disabled={loading || pagination.page <= 1}
          >
            Trước
          </button>
          <button
            className="btn btn-ghost"
            type="button"
            onClick={() => onPageChange(Math.min(totalPages, pagination.page + 1))}
            disabled={loading || pagination.page >= totalPages}
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
              disabled={loading}
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
    )
  }

  return (
    <section className="reports-tables">
      <section className="card" id="summary">
        <div className="card-row">
          <h3>Báo cáo tổng hợp</h3>
          <div className="table-actions">
            <label className="table-page-size">
              <span>Sắp xếp</span>
              <select
                value={summarySortKey}
                onChange={(event) => onSummarySortChange?.(event.target.value)}
              >
                {summarySortOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>
            {onExportSummary && (
              <button
                type="button"
                className="btn btn-ghost btn-table"
                onClick={onExportSummary}
                disabled={exportingSummary}
                aria-label="Tải báo cáo tổng hợp"
              >
                {exportingSummary ? 'Đang tải...' : 'Tải Excel tổng hợp'}
              </button>
            )}
            {onExportSummaryPdf && (
              <button
                type="button"
                className="btn btn-ghost btn-table"
                onClick={onExportSummaryPdf}
                disabled={exportingSummaryPdf}
                aria-label="Tải PDF tổng hợp"
              >
                {exportingSummaryPdf ? 'Đang tải...' : 'Tải PDF tổng hợp'}
              </button>
            )}
          </div>
        </div>
        {loadingSummary ? (
          <div className="empty-state">Đang tải báo cáo tổng hợp...</div>
        ) : summaryRows.length > 0 ? (
          <>
            <div className="table-scroll">
              <table
                className="table table--mobile-cards"
                style={{ '--table-columns': 8, '--table-min-width': '980px' } as CSSProperties}
              >
                <thead className="table-head">
                  <tr className="table-row">
                    <th scope="col">Nhóm</th>
                    <th scope="col">Tên</th>
                    <th scope="col">Phát sinh HĐ</th>
                    <th scope="col">Phát sinh trả hộ</th>
                    <th scope="col">Tiền đã thu</th>
                    <th scope="col">Dư HĐ còn lại</th>
                    <th scope="col">Dư trả hộ còn lại</th>
                    <th scope="col">Tổng nợ còn lại</th>
                  </tr>
                </thead>
                <tbody>
                  {summaryRows.map((row) => (
                    <tr className="table-row" key={row.groupKey}>
                      <td data-label="Nhóm">{row.groupKey}</td>
                      <td data-label="Tên">{row.groupName ?? '-'}</td>
                      <td data-label="Phát sinh HĐ">{formatMoney(row.invoicedTotal)}</td>
                      <td data-label="Phát sinh trả hộ">{formatMoney(row.advancedTotal)}</td>
                      <td data-label="Tiền đã thu">{formatMoney(row.receiptedTotal)}</td>
                      <td data-label="Dư HĐ còn lại">{formatMoney(row.outstandingInvoice)}</td>
                      <td data-label="Dư trả hộ còn lại">{formatMoney(row.outstandingAdvance)}</td>
                      <td data-label="Tổng nợ còn lại">{formatMoney(row.currentBalance)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {renderPagination(
              summaryPagination,
              loadingSummary,
              onSummaryPageChange,
              onSummaryPageSizeChange,
            )}
          </>
        ) : (
          <div className="empty-state">Không có dữ liệu trong kỳ đã chọn.</div>
        )}
      </section>

      <section className="card" id="statement">
        <div className="card-row">
          <h3>Sao kê khách hàng</h3>
          {onExportStatement && (
            <button
              type="button"
              className="btn btn-ghost btn-table"
              onClick={onExportStatement}
              disabled={exportingStatement}
              aria-label="Tải sao kê"
            >
              {exportingStatement ? 'Đang tải...' : 'Tải sao kê'}
            </button>
          )}
        </div>
        {shouldShowStatementCustomer && (
          <div className="chip-row">
            {statementCustomerTaxCode && (
              <span className="customer-chip">MST: {statementCustomerTaxCode}</span>
            )}
            {statementCustomerName && <span className="customer-name">{statementCustomerName}</span>}
          </div>
        )}
        {loadingStatement ? (
          <div className="empty-state">Đang tải báo cáo sao kê...</div>
        ) : statement ? (
          <>
            {statementCustomerTaxCode && (
              <div className="summary-grid">
                <div>
                  <strong>{formatMoney(statement.openingBalance)}</strong>
                  <span>Số dư đầu kỳ</span>
                </div>
                <div>
                  <strong>{formatMoney(statement.closingBalance)}</strong>
                  <span>Số dư cuối kỳ</span>
                </div>
              </div>
            )}
            <div className="table-scroll">
              <table
                className="table table--mobile-cards"
                style={{ '--table-columns': 8, '--table-min-width': '1020px' } as CSSProperties}
              >
                <thead className="table-head">
                  <tr className="table-row">
                    <th scope="col">Ngày</th>
                    <th scope="col">Kỳ áp dụng</th>
                    <th scope="col">Loại</th>
                    <th scope="col">Số CT</th>
                    <th scope="col">Diễn giải</th>
                    <th scope="col">Tăng</th>
                    <th scope="col">Giảm</th>
                    <th scope="col">Dư cuối</th>
                  </tr>
                </thead>
                <tbody>
                  {statement.lines.map((line, index) => (
                    <tr className="table-row" key={`${line.documentDate}-${index}`}>
                      <td data-label="Ngày">{formatDate(line.documentDate)}</td>
                      <td data-label="Kỳ áp dụng">{formatDate(line.appliedPeriodStart)}</td>
                      <td data-label="Loại">{line.type}</td>
                      <td data-label="Số CT">{line.documentNo ?? '-'}</td>
                      <td data-label="Diễn giải">{line.description ?? '-'}</td>
                      <td data-label="Tăng">{formatMoney(line.increase)}</td>
                      <td data-label="Giảm">{formatMoney(line.decrease)}</td>
                      <td data-label="Dư cuối">{formatMoney(line.runningBalance)}</td>
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
          {renderPagination(
            statementPagination,
            loadingStatement,
            onStatementPageChange,
            onStatementPageSizeChange,
          )}
        </>
        ) : (
          <div className="empty-state">Không có dữ liệu trong kỳ đã chọn.</div>
        )}
      </section>

      <section className="card" id="aging">
        <div className="card-row">
          <h3>Báo cáo tuổi nợ</h3>
          <div className="table-actions">
            <label className="table-page-size">
              <span>Sắp xếp</span>
              <select
                value={agingSortKey}
                onChange={(event) => onAgingSortChange?.(event.target.value)}
              >
                {agingSortOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>
            {onExportAging && (
              <button
                type="button"
                className="btn btn-ghost btn-table"
                onClick={onExportAging}
                disabled={exportingAging}
                aria-label="Tải tuổi nợ"
              >
                {exportingAging ? 'Đang tải...' : 'Tải tuổi nợ'}
              </button>
            )}
          </div>
        </div>
        {loadingAging ? (
          <div className="empty-state">Đang tải báo cáo tuổi nợ...</div>
        ) : agingRows.length > 0 ? (
          <>
            <div className="table-scroll">
              <table
                className="table table--mobile-cards"
                style={{ '--table-columns': 10, '--table-min-width': '1200px' } as CSSProperties}
              >
                <thead className="table-head">
                  <tr className="table-row">
                    <th scope="col">MST KH</th>
                    <th scope="col">Khách hàng</th>
                    <th scope="col">MST bán</th>
                    <th scope="col">0-30</th>
                    <th scope="col">31-60</th>
                    <th scope="col">61-90</th>
                    <th scope="col">91-180</th>
                    <th scope="col">&gt;180</th>
                    <th scope="col">Tổng</th>
                    <th scope="col">Quá hạn</th>
                  </tr>
                </thead>
                <tbody>
                  {agingRows.map((row) => (
                    <tr className="table-row" key={`${row.customerTaxCode}-${row.sellerTaxCode}`}>
                      <td data-label="MST KH">{row.customerTaxCode}</td>
                      <td data-label="Khách hàng">{row.customerName}</td>
                      <td data-label="MST bán">{row.sellerTaxCode}</td>
                      <td data-label="0-30">{formatMoney(row.bucket0To30)}</td>
                      <td data-label="31-60">{formatMoney(row.bucket31To60)}</td>
                      <td data-label="61-90">{formatMoney(row.bucket61To90)}</td>
                      <td data-label="91-180">{formatMoney(row.bucket91To180)}</td>
                      <td data-label=">180">{formatMoney(row.bucketOver180)}</td>
                      <td data-label="Tổng">{formatMoney(row.total)}</td>
                      <td data-label="Quá hạn">{formatMoney(row.overdue)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {renderPagination(
              agingPagination,
              loadingAging,
              onAgingPageChange,
              onAgingPageSizeChange,
            )}
          </>
        ) : (
          <div className="empty-state">Khách hàng không còn khoản nợ nào trên hệ thống</div>
        )}
      </section>
    </section>
  )
}
