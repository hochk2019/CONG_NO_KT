using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Reports;

namespace CongNoGolden.Infrastructure.Services;

public sealed partial class ReportService : IReportService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ReportService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }
}
