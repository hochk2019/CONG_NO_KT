namespace CongNoGolden.Application.Reports;

public sealed record ReportDeliveryEmailAttachment(
    string FileName,
    string ContentType,
    byte[] Content);

public sealed record ReportDeliveryEmailMessage(
    IReadOnlyList<string> Recipients,
    string Subject,
    string Body,
    ReportDeliveryEmailAttachment Attachment);

public sealed record ReportDeliveryEmailSendResult(
    bool Sent,
    bool Skipped,
    int RecipientCount,
    string? Detail);

public interface IReportDeliveryEmailSender
{
    Task<ReportDeliveryEmailSendResult> SendAsync(
        ReportDeliveryEmailMessage message,
        CancellationToken ct);
}
