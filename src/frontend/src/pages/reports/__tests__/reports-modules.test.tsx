import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { vi } from 'vitest'
import { AuthContext, type AuthContextValue } from '../../../context/AuthStore'
import { ReportsChartsSection } from '../ReportsChartsSection'
import { ReportsFilters } from '../ReportsFilters'
import { ReportsInsightsSection } from '../ReportsInsightsSection'
import { ReportsKpiSection } from '../ReportsKpiSection'
import { ReportsTablesSection } from '../ReportsTablesSection'
import { ReportsPage } from '../../ReportsPage'

const baseAuth: AuthContextValue = {
  state: {
    accessToken: null,
    expiresAt: null,
    username: 'tester',
    roles: [],
  },
  isAuthenticated: false,
  isBootstrapping: false,
  login: async () => undefined,
  logout: () => undefined,
}

describe('reports modules', () => {
  it('renders filters section', () => {
    render(
      <ReportsFilters
        filter={{
          from: '',
          to: '',
          asOfDate: '',
          sellerTaxCode: '',
          customerTaxCode: '',
          ownerId: '',
          groupBy: 'customer',
          filterText: '',
        }}
        useCustomAsOf={false}
        sellerOptions={[]}
        customerOptions={[]}
        ownerOptions={[]}
        presets={[{ id: 'month', label: 'Tháng này' }]}
        filterChips={[]}
        loadingAction=""
        onFromChange={vi.fn()}
        onToChange={vi.fn()}
        onAsOfChange={vi.fn()}
        onToggleCustomAsOf={vi.fn()}
        onSellerChange={vi.fn()}
        onCustomerChange={vi.fn()}
        onOwnerChange={vi.fn()}
        onGroupByChange={vi.fn()}
        onFilterTextChange={vi.fn()}
        onPresetSelect={vi.fn()}
        onResetFilters={vi.fn()}
        onLoadOverview={vi.fn()}
        onLoadSummary={vi.fn()}
        onLoadStatement={vi.fn()}
        onLoadAging={vi.fn()}
        onExport={vi.fn()}
      />,
    )

    expect(screen.getByText('Từ ngày')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Tải tổng quan' })).toBeInTheDocument()
  })

  it('renders KPI section', () => {
    render(
      <ReportsKpiSection
        kpis={{
          totalOutstanding: 1000000,
          outstandingInvoice: 800000,
          outstandingAdvance: 200000,
          unallocatedReceiptsAmount: 50000,
          unallocatedReceiptsCount: 1,
          overdueAmount: 100000,
          overdueCustomers: 2,
          dueSoonAmount: 50000,
          dueSoonCustomers: 1,
          onTimeCustomers: 3,
        }}
        kpiOrder={['totalOutstanding', 'onTimeCustomers']}
        dueSoonDays={7}
        savingPreferences={false}
        onMoveKpi={vi.fn()}
        onResetKpiOrder={vi.fn()}
        onDueSoonDaysChange={vi.fn()}
      />,
    )

    expect(screen.getByText('Tổng quan chỉ số')).toBeInTheDocument()
    expect(screen.getAllByText('KH trả đúng hạn').length).toBeGreaterThan(0)
  })

  it('renders charts section', () => {
    render(
      <ReportsChartsSection
        charts={{
          cashFlow: [
            { date: '01/01', value: 100000 },
            { date: '02/01', value: 200000 },
          ],
          agingDistribution: {
            bucket0To30: 100000,
            bucket31To60: 50000,
            bucket61To90: 20000,
            bucket91To180: 0,
            bucketOver180: 0,
          },
          allocationStatuses: [
            { status: 'ALLOCATED', amount: 100000 },
            { status: 'PARTIAL', amount: 20000 },
          ],
        }}
        loading={false}
      />,
    )

    expect(screen.getByText('Luồng tiền thu theo ngày')).toBeInTheDocument()
    expect(screen.getByText('Tuổi nợ')).toBeInTheDocument()
  })

  it('shows no debt message when aging chart has no data', () => {
    render(<ReportsChartsSection charts={null} loading={false} />)

    expect(
      screen.getByText('Khách hàng không còn khoản nợ nào trên hệ thống'),
    ).toBeInTheDocument()
  })

  it('renders insights section', () => {
    render(
      <ReportsInsightsSection
        insights={{
          topOutstanding: [
            {
              customerTaxCode: '0101',
              customerName: 'Công ty A',
              amount: 1000000,
              daysPastDue: 5,
              ratio: null,
            },
          ],
          topOnTime: [
            {
              customerTaxCode: '0202',
              customerName: 'Công ty B',
              amount: 500000,
              daysPastDue: 0,
              ratio: 0.98,
            },
          ],
          overdueByOwner: [
            {
              groupKey: 'owner-1',
              groupName: 'Nguyễn Văn A',
              totalOutstanding: 1000000,
              overdueAmount: 200000,
              overdueRatio: 0.2,
              overdueCustomers: 2,
            },
          ],
        }}
        loading={false}
        topOutstandingCount={5}
        topOutstandingOptions={[5, 10]}
        onTopOutstandingCountChange={vi.fn()}
      />,
    )

    expect(screen.getByText('Top cần chú ý')).toBeInTheDocument()
    expect(screen.getByText('Top trả đúng hạn nhất')).toBeInTheDocument()
    expect(screen.getByText('Hiển thị')).toBeInTheDocument()
  })

  it('renders tables section', () => {
    render(
      <ReportsTablesSection
        summaryRows={[
          {
            groupKey: 'KH-1',
            groupName: 'Công ty A',
            invoicedTotal: 100000,
            advancedTotal: 0,
            receiptedTotal: 50000,
            outstandingInvoice: 50000,
            outstandingAdvance: 0,
            currentBalance: 50000,
          },
        ]}
        statement={{
          openingBalance: 0,
          closingBalance: 50000,
          lines: [
            {
              documentDate: '2025-01-01',
              appliedPeriodStart: '2025-01-01',
              type: 'INVOICE',
              sellerTaxCode: '0101',
              customerTaxCode: '0101',
              customerName: 'Công ty A',
              documentNo: 'HD001',
              description: 'Test',
              revenue: 100000,
              vat: 0,
              increase: 100000,
              decrease: 0,
              runningBalance: 100000,
            },
          ],
          page: 1,
          pageSize: 20,
          total: 1,
        }}
        agingRows={[
          {
            customerTaxCode: '0101',
            customerName: 'Công ty A',
            sellerTaxCode: '0101',
            bucket0To30: 10000,
            bucket31To60: 0,
            bucket61To90: 0,
            bucket91To180: 0,
            bucketOver180: 0,
            total: 10000,
            overdue: 0,
          },
        ]}
        summaryPagination={{ page: 1, pageSize: 20, total: 1 }}
        statementPagination={{ page: 1, pageSize: 20, total: 1 }}
        agingPagination={{ page: 1, pageSize: 20, total: 1 }}
        summarySortKey=""
        agingSortKey=""
        loadingSummary={false}
        loadingStatement={false}
        loadingAging={false}
        exportingSummary={false}
        exportingStatement={false}
        exportingAging={false}
        onExportSummary={vi.fn()}
        onExportStatement={vi.fn()}
        onExportAging={vi.fn()}
        onSummaryPageChange={vi.fn()}
        onSummaryPageSizeChange={vi.fn()}
        onSummarySortChange={vi.fn()}
        onStatementPageChange={vi.fn()}
        onStatementPageSizeChange={vi.fn()}
        onAgingPageChange={vi.fn()}
        onAgingPageSizeChange={vi.fn()}
        onAgingSortChange={vi.fn()}
      />,
    )

    expect(screen.getByText('Báo cáo tổng hợp')).toBeInTheDocument()
    expect(screen.getByText('Sao kê khách hàng')).toBeInTheDocument()
    expect(screen.getByText('Báo cáo tuổi nợ')).toBeInTheDocument()
    expect(screen.getByText('Phát sinh HĐ')).toBeInTheDocument()
  })

  it('shows no debt message when aging table is empty', () => {
    render(
      <ReportsTablesSection
        summaryRows={[]}
        statement={null}
        agingRows={[]}
        summaryPagination={{ page: 1, pageSize: 20, total: 0 }}
        statementPagination={{ page: 1, pageSize: 20, total: 0 }}
        agingPagination={{ page: 1, pageSize: 20, total: 0 }}
        summarySortKey=""
        agingSortKey=""
        loadingSummary={false}
        loadingStatement={false}
        loadingAging={false}
      />,
    )

    expect(
      screen.getByText('Khách hàng không còn khoản nợ nào trên hệ thống'),
    ).toBeInTheDocument()
  })

  it('renders reports page with auth context', () => {
    render(
      <MemoryRouter>
        <AuthContext.Provider value={baseAuth}>
          <ReportsPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    expect(screen.getByText('Tổng quan công nợ & báo cáo chi tiết')).toBeInTheDocument()
  })
})
