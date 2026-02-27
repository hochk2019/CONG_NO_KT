using System.Net;
using System.Net.Mail;
using CongNoGolden.Application.Reports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CongNoGolden.Infrastructure.Services;

public sealed class ReportDeliveryEmailOptions
{
    public bool Enabled { get; set; }
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public bool UseDefaultCredentials { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? FromEmail { get; set; }
    public string? FromName { get; set; }
    public string? SubjectPrefix { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class ReportDeliveryEmailSender : IReportDeliveryEmailSender
{
    private readonly IOptions<ReportDeliveryEmailOptions> _options;
    private readonly ILogger<ReportDeliveryEmailSender> _logger;

    public ReportDeliveryEmailSender(
        IOptions<ReportDeliveryEmailOptions> options,
        ILogger<ReportDeliveryEmailSender> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<ReportDeliveryEmailSendResult> SendAsync(
        ReportDeliveryEmailMessage message,
        CancellationToken ct)
    {
        var recipients = (message.Recipients ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (recipients.Length == 0)
        {
            return new ReportDeliveryEmailSendResult(
                Sent: false,
                Skipped: true,
                RecipientCount: 0,
                Detail: "Không có người nhận trong lịch gửi.");
        }

        var options = _options.Value;
        if (!options.Enabled)
        {
            return new ReportDeliveryEmailSendResult(
                Sent: false,
                Skipped: true,
                RecipientCount: recipients.Length,
                Detail: "Gửi email bị tắt trong cấu hình.");
        }

        if (string.IsNullOrWhiteSpace(options.SmtpHost))
        {
            return new ReportDeliveryEmailSendResult(
                Sent: false,
                Skipped: false,
                RecipientCount: recipients.Length,
                Detail: "Thiếu cấu hình ReportDeliveryEmail:SmtpHost.");
        }

        if (string.IsNullOrWhiteSpace(options.FromEmail))
        {
            return new ReportDeliveryEmailSendResult(
                Sent: false,
                Skipped: false,
                RecipientCount: recipients.Length,
                Detail: "Thiếu cấu hình ReportDeliveryEmail:FromEmail.");
        }

        try
        {
            using var mail = new MailMessage
            {
                From = string.IsNullOrWhiteSpace(options.FromName)
                    ? new MailAddress(options.FromEmail.Trim())
                    : new MailAddress(options.FromEmail.Trim(), options.FromName.Trim()),
                Subject = BuildSubject(message.Subject, options.SubjectPrefix),
                Body = message.Body,
                IsBodyHtml = false
            };

            foreach (var recipient in recipients)
            {
                mail.To.Add(recipient);
            }

            using var attachmentStream = new MemoryStream(message.Attachment.Content, writable: false);
            using var attachment = new Attachment(
                attachmentStream,
                message.Attachment.FileName,
                message.Attachment.ContentType);
            mail.Attachments.Add(attachment);

            using var smtp = new SmtpClient(options.SmtpHost.Trim(), options.SmtpPort)
            {
                EnableSsl = options.UseSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = ResolveTimeoutMilliseconds(options.TimeoutSeconds)
            };

            if (options.UseDefaultCredentials)
            {
                smtp.UseDefaultCredentials = true;
            }
            else if (!string.IsNullOrWhiteSpace(options.Username))
            {
                smtp.UseDefaultCredentials = false;
                smtp.Credentials = new NetworkCredential(options.Username.Trim(), options.Password ?? string.Empty);
            }

            await smtp.SendMailAsync(mail, ct);

            return new ReportDeliveryEmailSendResult(
                Sent: true,
                Skipped: false,
                RecipientCount: recipients.Length,
                Detail: null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Report delivery email send failed.");
            return new ReportDeliveryEmailSendResult(
                Sent: false,
                Skipped: false,
                RecipientCount: recipients.Length,
                Detail: ex.Message);
        }
    }

    private static int ResolveTimeoutMilliseconds(int timeoutSeconds)
    {
        var safeSeconds = timeoutSeconds is < 5 or > 300 ? 30 : timeoutSeconds;
        return checked(safeSeconds * 1000);
    }

    private static string BuildSubject(string subject, string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return subject;
        }

        var normalizedPrefix = prefix.Trim();
        return string.IsNullOrWhiteSpace(subject)
            ? normalizedPrefix
            : $"{normalizedPrefix} {subject}".Trim();
    }
}
