using System.Text.Json;
using CongNoGolden.Application.Reports;
using CongNoGolden.Application.Common.Interfaces;
using CongNoGolden.Infrastructure.Data;
using CongNoGolden.Infrastructure.Data.Entities;
using CongNoGolden.Infrastructure.Services.Common;
using Cronos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CongNoGolden.Infrastructure.Services;

public sealed class ReportScheduleService : IReportScheduleService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ConGNoDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IReportExportService _reportExportService;
    private readonly IReportDeliveryEmailSender _reportDeliveryEmailSender;
    private readonly ILogger<ReportScheduleService> _logger;

    public ReportScheduleService(
        ConGNoDbContext db,
        ICurrentUser currentUser,
        IReportExportService reportExportService,
        IReportDeliveryEmailSender reportDeliveryEmailSender,
        ILogger<ReportScheduleService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _reportExportService = reportExportService;
        _reportDeliveryEmailSender = reportDeliveryEmailSender;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ReportDeliveryScheduleItem>> ListAsync(CancellationToken ct)
    {
        var userId = _currentUser.EnsureUser();
        var schedules = await _db.ReportDeliverySchedules
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(ct);

        if (schedules.Count == 0)
        {
            return [];
        }

        var latestRuns = await LoadLatestRunsAsync(schedules.Select(x => x.Id).ToArray(), ct);

        return schedules
            .Select(schedule =>
            {
                latestRuns.TryGetValue(schedule.Id, out var latestRun);
                return ToScheduleItem(schedule, latestRun);
            })
            .ToList();
    }

    public async Task<ReportDeliveryScheduleItem> CreateAsync(ReportDeliveryScheduleUpsertRequest request, CancellationToken ct)
    {
        var userId = _currentUser.EnsureUser();
        var normalized = NormalizeRequest(request);
        var now = DateTimeOffset.UtcNow;

        var schedule = new ReportDeliverySchedule
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ReportKind = normalized.ReportKindCode,
            ReportFormat = normalized.ReportFormatCode,
            CronExpression = normalized.CronExpression,
            TimezoneId = normalized.Timezone.Id,
            Recipients = JsonSerializer.Serialize(normalized.Recipients, JsonOptions),
            FilterPayload = JsonSerializer.Serialize(normalized.Filter, JsonOptions),
            Enabled = normalized.Enabled,
            LastRunAt = null,
            NextRunAt = normalized.Enabled
                ? CalculateNextRunUtc(normalized.Cron, normalized.Timezone, now)
                : null,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.ReportDeliverySchedules.Add(schedule);
        await _db.SaveChangesAsync(ct);

        return ToScheduleItem(schedule, latestRun: null);
    }

    public async Task<ReportDeliveryScheduleItem> UpdateAsync(
        Guid id,
        ReportDeliveryScheduleUpsertRequest request,
        CancellationToken ct)
    {
        var userId = _currentUser.EnsureUser();
        var schedule = await GetOwnedScheduleAsync(id, userId, ct);
        var normalized = NormalizeRequest(request);
        var now = DateTimeOffset.UtcNow;

        schedule.ReportKind = normalized.ReportKindCode;
        schedule.ReportFormat = normalized.ReportFormatCode;
        schedule.CronExpression = normalized.CronExpression;
        schedule.TimezoneId = normalized.Timezone.Id;
        schedule.Recipients = JsonSerializer.Serialize(normalized.Recipients, JsonOptions);
        schedule.FilterPayload = JsonSerializer.Serialize(normalized.Filter, JsonOptions);
        schedule.Enabled = normalized.Enabled;
        schedule.NextRunAt = normalized.Enabled
            ? CalculateNextRunUtc(normalized.Cron, normalized.Timezone, now)
            : null;
        schedule.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        var latestRun = await _db.ReportDeliveryRuns
            .AsNoTracking()
            .Where(run => run.ScheduleId == schedule.Id)
            .OrderByDescending(run => run.StartedAt)
            .FirstOrDefaultAsync(ct);

        return ToScheduleItem(schedule, latestRun);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.EnsureUser();
        var schedule = await GetOwnedScheduleAsync(id, userId, ct);
        _db.ReportDeliverySchedules.Remove(schedule);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ReportDeliveryRunItem> RunNowAsync(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.EnsureUser();
        var schedule = await GetOwnedScheduleAsync(id, userId, ct);
        return await ExecuteRunAsync(schedule, trigger: "MANUAL", ct);
    }

    public async Task<PagedResult<ReportDeliveryRunItem>> ListRunsAsync(
        Guid scheduleId,
        ReportDeliveryRunListRequest request,
        CancellationToken ct)
    {
        var userId = _currentUser.EnsureUser();

        var owned = await _db.ReportDeliverySchedules
            .AsNoTracking()
            .AnyAsync(x => x.Id == scheduleId && x.UserId == userId, ct);

        if (!owned)
        {
            throw new KeyNotFoundException("Không tìm thấy lịch gửi báo cáo.");
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 5 or > 100 ? 20 : request.PageSize;
        var offset = (page - 1) * pageSize;

        var query = _db.ReportDeliveryRuns
            .AsNoTracking()
            .Where(run => run.ScheduleId == scheduleId);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(run => run.StartedAt)
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rows.Select(ToRunItem).ToList();
        return new PagedResult<ReportDeliveryRunItem>(items, page, pageSize, total);
    }

    public async Task<int> RunDueSchedulesAsync(int batchSize, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var size = batchSize is < 1 or > 200 ? 20 : batchSize;

        var dueSchedules = await _db.ReportDeliverySchedules
            .Where(schedule => schedule.Enabled
                && schedule.NextRunAt.HasValue
                && schedule.NextRunAt <= now)
            .OrderBy(schedule => schedule.NextRunAt)
            .Take(size)
            .ToListAsync(ct);

        var executed = 0;
        foreach (var schedule in dueSchedules)
        {
            await ExecuteRunAsync(schedule, trigger: "SCHEDULED", ct);
            executed++;
        }

        return executed;
    }

    private async Task<ReportDeliveryRunItem> ExecuteRunAsync(
        ReportDeliverySchedule schedule,
        string trigger,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var run = new ReportDeliveryRun
        {
            Id = Guid.NewGuid(),
            ScheduleId = schedule.Id,
            Status = "RUNNING",
            StartedAt = now,
            CreatedAt = now
        };

        _db.ReportDeliveryRuns.Add(run);
        schedule.LastRunAt = now;
        schedule.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        try
        {
            var exportRequest = BuildExportRequest(schedule);
            var exportResult = await _reportExportService.ExportAsync(exportRequest, ct);
            var emailResult = await _reportDeliveryEmailSender.SendAsync(
                BuildEmailMessage(schedule, exportResult),
                ct);

            if (!emailResult.Sent && !emailResult.Skipped)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(emailResult.Detail)
                        ? "Gửi email báo cáo thất bại."
                        : emailResult.Detail);
            }

            var artifact = new ReportArtifactMeta(
                exportResult.FileName,
                exportResult.ContentType,
                exportResult.Content.Length);

            run.Status = "SUCCEEDED";
            run.FinishedAt = DateTimeOffset.UtcNow;
            run.ArtifactMeta = JsonSerializer.Serialize(artifact, JsonOptions);

            schedule.NextRunAt = schedule.Enabled
                ? CalculateNextRunUtc(ParseCronExpression(schedule.CronExpression), ResolveTimezone(schedule.TimezoneId), run.FinishedAt.Value)
                : null;
            schedule.UpdatedAt = run.FinishedAt.Value;

            AppendRunNotification(schedule, run, trigger, artifact, errorDetail: null, emailResult);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Report schedule run failed. ScheduleId={ScheduleId}", schedule.Id);

            run.Status = "FAILED";
            run.ErrorDetail = TrimError(ex.Message);
            run.FinishedAt = DateTimeOffset.UtcNow;

            schedule.NextRunAt = schedule.Enabled
                ? CalculateNextRunUtc(ParseCronExpression(schedule.CronExpression), ResolveTimezone(schedule.TimezoneId), run.FinishedAt.Value)
                : null;
            schedule.UpdatedAt = run.FinishedAt.Value;

            AppendRunNotification(schedule, run, trigger, artifact: null, errorDetail: run.ErrorDetail, emailResult: null);
            await _db.SaveChangesAsync(ct);
        }

        return ToRunItem(run);
    }

    private static string? TrimError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        return message.Length <= 1500 ? message : message[..1500];
    }

    private void AppendRunNotification(
        ReportDeliverySchedule schedule,
        ReportDeliveryRun run,
        string trigger,
        ReportArtifactMeta? artifact,
        string? errorDetail,
        ReportDeliveryEmailSendResult? emailResult)
    {
        var severity = run.Status == "SUCCEEDED" ? "INFO" : "ALERT";
        var title = run.Status == "SUCCEEDED"
            ? "Lịch gửi báo cáo đã chạy thành công"
            : "Lịch gửi báo cáo chạy thất bại";

        var emailSummary = emailResult is null
            ? null
            : emailResult.Sent
                ? $"Email đã gửi tới {emailResult.RecipientCount} người nhận."
                : emailResult.Skipped
                    ? $"Email không gửi: {emailResult.Detail}"
                    : $"Email thất bại: {emailResult.Detail}";

        var body = run.Status == "SUCCEEDED"
            ? $"Báo cáo {schedule.ReportKind} ({schedule.ReportFormat}) đã được tạo từ lịch {trigger.ToLowerInvariant()}. {emailSummary}".Trim()
            : $"Báo cáo {schedule.ReportKind} ({schedule.ReportFormat}) lỗi khi chạy lịch {trigger.ToLowerInvariant()}: {errorDetail}";

        var metadata = JsonSerializer.Serialize(new
        {
            scheduleId = schedule.Id,
            runId = run.Id,
            run.Status,
            run.StartedAt,
            run.FinishedAt,
            schedule.ReportKind,
            schedule.ReportFormat,
            trigger,
            artifact,
            email = emailResult
        }, JsonOptions);

        _db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = schedule.UserId,
            Title = title,
            Body = body,
            Severity = severity,
            Source = "REPORT",
            Metadata = metadata,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private static ReportDeliveryEmailMessage BuildEmailMessage(
        ReportDeliverySchedule schedule,
        ReportExportResult exportResult)
    {
        var recipients = DeserializeRecipients(schedule.Recipients);
        var attachment = new ReportDeliveryEmailAttachment(
            exportResult.FileName,
            exportResult.ContentType,
            exportResult.Content);

        var subject = $"[CongNo] Bao cao {schedule.ReportKind} ({schedule.ReportFormat})";
        var body =
            $"He thong da tao bao cao tu lich tu dong.\n" +
            $"Loai: {schedule.ReportKind}\n" +
            $"Dinh dang: {schedule.ReportFormat}\n" +
            $"Lich chay: {schedule.CronExpression} ({schedule.TimezoneId})\n" +
            $"Thoi diem UTC: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}\n";

        return new ReportDeliveryEmailMessage(recipients, subject, body, attachment);
    }

    private static ReportExportRequest BuildExportRequest(ReportDeliverySchedule schedule)
    {
        var kind = ParseReportKind(schedule.ReportKind);
        var format = ParseReportFormat(schedule.ReportFormat);
        var filter = DeserializeFilter(schedule.FilterPayload);

        return new ReportExportRequest(
            filter.From,
            filter.To,
            filter.AsOfDate,
            filter.SellerTaxCode,
            filter.CustomerTaxCode,
            filter.OwnerId,
            filter.FilterText,
            kind,
            format);
    }

    private static ReportDeliveryScheduleItem ToScheduleItem(
        ReportDeliverySchedule schedule,
        ReportDeliveryRun? latestRun)
    {
        return new ReportDeliveryScheduleItem(
            schedule.Id,
            schedule.UserId,
            ParseReportKind(schedule.ReportKind),
            ParseReportFormat(schedule.ReportFormat),
            schedule.CronExpression,
            schedule.TimezoneId,
            DeserializeRecipients(schedule.Recipients),
            DeserializeFilter(schedule.FilterPayload),
            schedule.Enabled,
            schedule.LastRunAt,
            schedule.NextRunAt,
            schedule.CreatedAt,
            schedule.UpdatedAt,
            latestRun?.Status,
            latestRun?.FinishedAt);
    }

    private static ReportDeliveryRunItem ToRunItem(ReportDeliveryRun run)
    {
        return new ReportDeliveryRunItem(
            run.Id,
            run.ScheduleId,
            run.Status,
            run.StartedAt,
            run.FinishedAt,
            run.ErrorDetail,
            DeserializeArtifact(run.ArtifactMeta),
            run.CreatedAt);
    }

    private static ReportDeliveryRunArtifact? DeserializeArtifact(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var value = JsonSerializer.Deserialize<ReportArtifactMeta>(json, JsonOptions);
            return value is null
                ? null
                : new ReportDeliveryRunArtifact(value.FileName, value.ContentType, value.SizeBytes);
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<string> DeserializeRecipients(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var values = JsonSerializer.Deserialize<string[]>(json, JsonOptions);
            return values?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static ReportDeliveryFilterDto DeserializeFilter(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ReportDeliveryFilterDto(null, null, null, null, null, null, null);
        }

        try
        {
            return JsonSerializer.Deserialize<ReportDeliveryFilterDto>(json, JsonOptions)
                ?? new ReportDeliveryFilterDto(null, null, null, null, null, null, null);
        }
        catch
        {
            return new ReportDeliveryFilterDto(null, null, null, null, null, null, null);
        }
    }

    private async Task<Dictionary<Guid, ReportDeliveryRun>> LoadLatestRunsAsync(Guid[] scheduleIds, CancellationToken ct)
    {
        if (scheduleIds.Length == 0)
        {
            return new Dictionary<Guid, ReportDeliveryRun>();
        }

        var runs = await _db.ReportDeliveryRuns
            .AsNoTracking()
            .Where(run => scheduleIds.Contains(run.ScheduleId))
            .OrderByDescending(run => run.StartedAt)
            .ToListAsync(ct);

        var result = new Dictionary<Guid, ReportDeliveryRun>();
        foreach (var run in runs)
        {
            if (!result.ContainsKey(run.ScheduleId))
            {
                result[run.ScheduleId] = run;
            }
        }

        return result;
    }

    private async Task<ReportDeliverySchedule> GetOwnedScheduleAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var schedule = await _db.ReportDeliverySchedules
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);

        if (schedule is null)
        {
            throw new KeyNotFoundException("Không tìm thấy lịch gửi báo cáo.");
        }

        return schedule;
    }

    private static NormalizedScheduleRequest NormalizeRequest(ReportDeliveryScheduleUpsertRequest request)
    {
        var cron = ParseCronExpression(request.CronExpression);
        var timezone = ResolveTimezone(request.TimezoneId);
        ValidateFilter(request.Filter);

        if (request.ReportFormat == ReportExportFormat.Pdf && request.ReportKind != ReportExportKind.Summary)
        {
            throw new InvalidOperationException("Định dạng PDF hiện chỉ hỗ trợ báo cáo tổng hợp.");
        }

        var recipients = (request.Recipients ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new NormalizedScheduleRequest(
            request.ReportKind,
            request.ReportKind.ToString().ToUpperInvariant(),
            request.ReportFormat,
            request.ReportFormat.ToString().ToUpperInvariant(),
            request.CronExpression.Trim(),
            cron,
            timezone,
            recipients,
            request.Filter ?? new ReportDeliveryFilterDto(null, null, null, null, null, null, null),
            request.Enabled);
    }

    private static void ValidateFilter(ReportDeliveryFilterDto? filter)
    {
        if (filter is null)
        {
            return;
        }

        if (filter.From.HasValue && filter.To.HasValue && filter.From > filter.To)
        {
            throw new InvalidOperationException("Từ ngày phải nhỏ hơn hoặc bằng đến ngày.");
        }
    }

    internal static CronExpression ParseCronExpression(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            throw new InvalidOperationException("Biểu thức cron không được để trống.");
        }

        try
        {
            var normalized = cronExpression.Trim();
            var tokenCount = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            var format = tokenCount == 6 ? CronFormat.IncludeSeconds : CronFormat.Standard;
            return CronExpression.Parse(normalized, format);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Biểu thức cron không hợp lệ: {ex.Message}");
        }
    }

    internal static TimeZoneInfo ResolveTimezone(string? timezoneId)
    {
        var value = string.IsNullOrWhiteSpace(timezoneId) ? "UTC" : timezoneId.Trim();

        if (TryFindTimezone(value, out var timezone))
        {
            return timezone;
        }

        if (OperatingSystem.IsWindows()
            && TimeZoneInfo.TryConvertIanaIdToWindowsId(value, out var windowsId)
            && TryFindTimezone(windowsId, out timezone))
        {
            return timezone;
        }

        if (!OperatingSystem.IsWindows()
            && TimeZoneInfo.TryConvertWindowsIdToIanaId(value, out var ianaId)
            && TryFindTimezone(ianaId, out timezone))
        {
            return timezone;
        }

        throw new InvalidOperationException("Múi giờ không hợp lệ.");
    }

    private static bool TryFindTimezone(string timezoneId, out TimeZoneInfo timezone)
    {
        try
        {
            timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            return true;
        }
        catch
        {
            timezone = TimeZoneInfo.Utc;
            return false;
        }
    }

    internal static DateTimeOffset? CalculateNextRunUtc(
        CronExpression cron,
        TimeZoneInfo timezone,
        DateTimeOffset afterUtc)
    {
        var next = cron.GetNextOccurrence(afterUtc.UtcDateTime, timezone);
        return next.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(next.Value, DateTimeKind.Utc))
            : null;
    }

    private static ReportExportKind ParseReportKind(string value)
    {
        return Enum.TryParse<ReportExportKind>(value, ignoreCase: true, out var parsed)
            ? parsed
            : ReportExportKind.Full;
    }

    private static ReportExportFormat ParseReportFormat(string value)
    {
        return Enum.TryParse<ReportExportFormat>(value, ignoreCase: true, out var parsed)
            ? parsed
            : ReportExportFormat.Xlsx;
    }

    private sealed record ReportArtifactMeta(
        string? FileName,
        string? ContentType,
        int? SizeBytes);

    private sealed record NormalizedScheduleRequest(
        ReportExportKind ReportKind,
        string ReportKindCode,
        ReportExportFormat ReportFormat,
        string ReportFormatCode,
        string CronExpression,
        CronExpression Cron,
        TimeZoneInfo Timezone,
        IReadOnlyList<string> Recipients,
        ReportDeliveryFilterDto Filter,
        bool Enabled);
}
