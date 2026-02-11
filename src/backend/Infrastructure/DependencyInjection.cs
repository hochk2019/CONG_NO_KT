using CongNoGolden.Application.Auth;
using CongNoGolden.Application.Advances;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Customers;
using CongNoGolden.Application.Dashboard;
using CongNoGolden.Application.Imports;
using CongNoGolden.Application.Invoices;
using CongNoGolden.Application.Notifications;
using CongNoGolden.Application.PeriodLocks;
using CongNoGolden.Application.Receipts;
using CongNoGolden.Application.Reports;
using CongNoGolden.Application.Reminders;
using CongNoGolden.Application.Risk;
using CongNoGolden.Application.Backups;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace CongNoGolden.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        DapperTypeHandlers.Register();
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'Default' is not configured.");
        }

        services.AddDbContext<ConGNoDbContext>(options =>
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

        services.AddSingleton<IDbConnectionFactory>(_ => new NpgsqlConnectionFactory(connectionString));
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IImportBatchService, ImportBatchService>();
        services.AddScoped<IImportStagingService, ImportStagingService>();
        services.AddScoped<IImportPreviewService, ImportPreviewService>();
        services.AddScoped<IImportCommitService, ImportCommitService>();
        services.AddScoped<IImportRollbackService, ImportRollbackService>();
        services.AddScoped<IImportCancelService, ImportCancelService>();
        services.AddScoped<IAdvanceService, AdvanceService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IPeriodLockService, PeriodLockService>();
        services.AddScoped<IReceiptService, ReceiptService>();
        services.AddScoped<IReceiptAutomationService, ReceiptAutomationService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IInvoiceCreditReconcileService, InvoiceCreditReconcileService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IReportExportService, ReportExportService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IRiskService, RiskService>();
        services.AddScoped<IReminderService, ReminderService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddSingleton<BackupQueue>();
        services.AddSingleton<BackupProcessRunner>();
        services.AddHttpClient<IZaloClient, ZaloClient>();

        return services;
    }
}
