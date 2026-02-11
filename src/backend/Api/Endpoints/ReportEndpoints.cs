using CongNoGolden.Api;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Application.Reports;

namespace CongNoGolden.Api.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/reports/summary", async (
            DateOnly? from,
            DateOnly? to,
            string? groupBy,
            string? sellerTaxCode,
            string? customerTaxCode,
            Guid? ownerId,
            IReportService reportService,
            CancellationToken ct) =>
        {
            var rangeError = ReportRequestValidator.ValidateDateRange(from, to);
            if (rangeError is not null)
            {
                return ApiErrors.InvalidRequest(rangeError);
            }

            var request = new ReportSummaryRequest(
                from,
                to,
                groupBy,
                sellerTaxCode,
                customerTaxCode,
                ownerId);

            var rows = await reportService.GetSummaryAsync(request, ct);
            return Results.Ok(rows);
        })
        .WithName("ReportSummary")
        .WithTags("Reports")
        .RequireAuthorization("ReportsView");

        app.MapGet("/reports/summary/paged", async (
            DateOnly? from,
            DateOnly? to,
            string? groupBy,
            string? sellerTaxCode,
            string? customerTaxCode,
            Guid? ownerId,
            int? page,
            int? pageSize,
            string? sortKey,
            string? sortDirection,
            IReportService reportService,
            CancellationToken ct) =>
        {
            var rangeError = ReportRequestValidator.ValidateDateRange(from, to);
            if (rangeError is not null)
            {
                return ApiErrors.InvalidRequest(rangeError);
            }

            var request = new ReportSummaryPagedRequest(
                from,
                to,
                groupBy,
                sellerTaxCode,
                customerTaxCode,
                ownerId,
                page ?? 1,
                pageSize ?? 20,
                sortKey,
                sortDirection);

            var result = await reportService.GetSummaryPagedAsync(request, ct);
            return Results.Ok(result);
        })
        .WithName("ReportSummaryPaged")
        .WithTags("Reports")
        .RequireAuthorization("ReportsView");

        app.MapGet("/reports/kpis", async (
            DateOnly? from,
            DateOnly? to,
            DateOnly? asOfDate,
            string? sellerTaxCode,
            string? customerTaxCode,
            Guid? ownerId,
            int? dueSoonDays,
            IReportService reportService,
            CancellationToken ct) =>
        {
            var rangeError = ReportRequestValidator.ValidateDateRange(from, to);
            if (rangeError is not null)
            {
                return ApiErrors.InvalidRequest(rangeError);
            }

            var request = new ReportKpiRequest(
                from,
                to,
                asOfDate,
                sellerTaxCode,
                customerTaxCode,
                ownerId,
                dueSoonDays.GetValueOrDefault(7));

            var result = await reportService.GetKpisAsync(request, ct);
            return Results.Ok(result);
        })
        .WithName("ReportKpis")
        .WithTags("Reports")
        .RequireAuthorization("ReportsView");

        app.MapGet("/reports/charts", async (
            DateOnly? from,
            DateOnly? to,
            DateOnly? asOfDate,
            string? sellerTaxCode,
            string? customerTaxCode,
            Guid? ownerId,
            IReportService reportService,
            CancellationToken ct) =>
        {
            var rangeError = ReportRequestValidator.ValidateDateRange(from, to);
            if (rangeError is not null)
            {
                return ApiErrors.InvalidRequest(rangeError);
            }

            var request = new ReportChartsRequest(
                from,
                to,
                asOfDate,
                sellerTaxCode,
                customerTaxCode,
                ownerId);

            var result = await reportService.GetChartsAsync(request, ct);
            return Results.Ok(result);
        })
        .WithName("ReportCharts")
        .WithTags("Reports")
        .RequireAuthorization("ReportsView");

        app.MapGet("/reports/insights", async (
            DateOnly? from,
            DateOnly? to,
            DateOnly? asOfDate,
            string? sellerTaxCode,
            string? customerTaxCode,
            Guid? ownerId,
            int? top,
            IReportService reportService,
            CancellationToken ct) =>
        {
            var rangeError = ReportRequestValidator.ValidateDateRange(from, to);
            if (rangeError is not null)
            {
                return ApiErrors.InvalidRequest(rangeError);
            }

            var request = new ReportInsightsRequest(
                from,
                to,
                asOfDate,
                sellerTaxCode,
                customerTaxCode,
                ownerId,
                top.GetValueOrDefault(5));

            var result = await reportService.GetInsightsAsync(request, ct);
            return Results.Ok(result);
        })
        .WithName("ReportInsights")
        .WithTags("Reports")
        .RequireAuthorization("ReportsView");

        app.MapGet("/reports/statement", async (
            DateOnly? from,
            DateOnly? to,
            string? sellerTaxCode,
            string? customerTaxCode,
            IReportService reportService,
            CancellationToken ct) =>
        {
            var rangeError = ReportRequestValidator.ValidateDateRange(from, to);
            if (rangeError is not null)
            {
                return ApiErrors.InvalidRequest(rangeError);
            }

            var request = new ReportStatementRequest(
                customerTaxCode,
                from,
                to,
                sellerTaxCode);

            var result = await reportService.GetStatementAsync(request, ct);
            return Results.Ok(result);
        })
        .WithName("ReportStatement")
        .WithTags("Reports")
        .RequireAuthorization("ReportsView");

        app.MapGet("/reports/statement/paged", async (
            DateOnly? from,
            DateOnly? to,
            string? sellerTaxCode,
            string? customerTaxCode,
            int? page,
            int? pageSize,
            IReportService reportService,
            CancellationToken ct) =>
        {
            var rangeError = ReportRequestValidator.ValidateDateRange(from, to);
            if (rangeError is not null)
            {
                return ApiErrors.InvalidRequest(rangeError);
            }

            var request = new ReportStatementPagedRequest(
                from,
                to,
                sellerTaxCode,
                customerTaxCode,
                page ?? 1,
                pageSize ?? 20);

            var result = await reportService.GetStatementPagedAsync(request, ct);
            return Results.Ok(result);
        })
        .WithName("ReportStatementPaged")
        .WithTags("Reports")
        .RequireAuthorization("ReportsView");

        app.MapGet("/reports/aging", async (
            DateOnly? asOfDate,
            string? sellerTaxCode,
            string? customerTaxCode,
            Guid? ownerId,
            IReportService reportService,
            CancellationToken ct) =>
        {
            var asOfError = ReportRequestValidator.ValidateAsOfDate(asOfDate);
            if (asOfError is not null)
            {
                return ApiErrors.InvalidRequest(asOfError);
            }

            var request = new ReportAgingRequest(
                asOfDate,
                sellerTaxCode,
                customerTaxCode,
                ownerId);

            var rows = await reportService.GetAgingAsync(request, ct);
            return Results.Ok(rows);
        })
        .WithName("ReportAging")
        .WithTags("Reports")
        .RequireAuthorization("ReportsView");

        app.MapGet("/reports/aging/paged", async (
            DateOnly? asOfDate,
            string? sellerTaxCode,
            string? customerTaxCode,
            Guid? ownerId,
            int? page,
            int? pageSize,
            string? sortKey,
            string? sortDirection,
            IReportService reportService,
            CancellationToken ct) =>
        {
            var asOfError = ReportRequestValidator.ValidateAsOfDate(asOfDate);
            if (asOfError is not null)
            {
                return ApiErrors.InvalidRequest(asOfError);
            }

            var request = new ReportAgingPagedRequest(
                asOfDate,
                sellerTaxCode,
                customerTaxCode,
                ownerId,
                page ?? 1,
                pageSize ?? 20,
                sortKey,
                sortDirection);

            var result = await reportService.GetAgingPagedAsync(request, ct);
            return Results.Ok(result);
        })
        .WithName("ReportAgingPaged")
        .WithTags("Reports")
        .RequireAuthorization("ReportsView");

        app.MapGet("/reports/preferences", async (
            IReportService reportService,
            ICurrentUser currentUser,
            CancellationToken ct) =>
        {
            if (!currentUser.UserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await reportService.GetPreferencesAsync(currentUser.UserId.Value, ct);
            return Results.Ok(result);
        })
        .WithName("ReportPreferences")
        .WithTags("Reports")
        .RequireAuthorization("ReportsView");

        app.MapPut("/reports/preferences", async (
            UpdateReportPreferencesRequest request,
            IReportService reportService,
            ICurrentUser currentUser,
            CancellationToken ct) =>
        {
            if (!currentUser.UserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await reportService.UpdatePreferencesAsync(currentUser.UserId.Value, request, ct);
            return Results.Ok(result);
        })
        .WithName("ReportPreferencesUpdate")
        .WithTags("Reports")
        .RequireAuthorization("ReportsView");

        app.MapGet("/reports/export", async (
            DateOnly? from,
            DateOnly? to,
            DateOnly? asOfDate,
            string? sellerTaxCode,
            string? customerTaxCode,
            Guid? ownerId,
            string? filterText,
            ReportExportKind? kind,
            IReportExportService exportService,
            CancellationToken ct) =>
        {
            var exportKind = kind ?? ReportExportKind.Full;
            var rangeError = ReportRequestValidator.ValidateDateRange(from, to);
            if (rangeError is not null)
            {
                return ApiErrors.InvalidRequest(rangeError);
            }

            if (exportKind == ReportExportKind.Aging)
            {
                var asOfError = ReportRequestValidator.ValidateAsOfDate(asOfDate);
                if (asOfError is not null)
                {
                    return ApiErrors.InvalidRequest(asOfError);
                }
            }

            var request = new ReportExportRequest(
                from,
                to,
                asOfDate,
                sellerTaxCode,
                customerTaxCode,
                ownerId,
                filterText,
                exportKind);

            var result = await exportService.ExportAsync(request, ct);
            return Results.File(
                result.Content,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                result.FileName);
        })
        .WithName("ReportExport")
        .WithTags("Reports")
        .RequireAuthorization("ReportsView");

        return app;
    }
}
