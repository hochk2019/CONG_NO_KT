using CongNoGolden.Application.Reports;
using CongNoGolden.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CongNoGolden.Tests.Unit;

public sealed class ReportDeliveryEmailSenderTests
{
    [Fact]
    public async Task SendAsync_WhenRecipientsEmpty_ReturnsSkipped()
    {
        var sender = CreateSender(new ReportDeliveryEmailOptions
        {
            Enabled = true,
            SmtpHost = "smtp.example.local",
            FromEmail = "no-reply@example.local"
        });

        var result = await sender.SendAsync(
            new ReportDeliveryEmailMessage(
                Recipients: [],
                Subject: "Test",
                Body: "Body",
                Attachment: new ReportDeliveryEmailAttachment(
                    "report.xlsx",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    [1, 2, 3])),
            CancellationToken.None);

        Assert.False(result.Sent);
        Assert.True(result.Skipped);
        Assert.Equal(0, result.RecipientCount);
    }

    [Fact]
    public async Task SendAsync_WhenFeatureDisabled_ReturnsSkipped()
    {
        var sender = CreateSender(new ReportDeliveryEmailOptions
        {
            Enabled = false
        });

        var result = await sender.SendAsync(
            BuildMessage("acct@example.local"),
            CancellationToken.None);

        Assert.False(result.Sent);
        Assert.True(result.Skipped);
        Assert.Equal(1, result.RecipientCount);
        Assert.Contains("tắt", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendAsync_WhenHostMissing_ReturnsFailed()
    {
        var sender = CreateSender(new ReportDeliveryEmailOptions
        {
            Enabled = true,
            FromEmail = "no-reply@example.local"
        });

        var result = await sender.SendAsync(
            BuildMessage("acct@example.local"),
            CancellationToken.None);

        Assert.False(result.Sent);
        Assert.False(result.Skipped);
        Assert.Contains("SmtpHost", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendAsync_WhenFromEmailMissing_ReturnsFailed()
    {
        var sender = CreateSender(new ReportDeliveryEmailOptions
        {
            Enabled = true,
            SmtpHost = "smtp.example.local"
        });

        var result = await sender.SendAsync(
            BuildMessage("acct@example.local"),
            CancellationToken.None);

        Assert.False(result.Sent);
        Assert.False(result.Skipped);
        Assert.Contains("FromEmail", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    private static ReportDeliveryEmailSender CreateSender(ReportDeliveryEmailOptions options)
    {
        return new ReportDeliveryEmailSender(
            Options.Create(options),
            NullLogger<ReportDeliveryEmailSender>.Instance);
    }

    private static ReportDeliveryEmailMessage BuildMessage(string recipient)
    {
        return new ReportDeliveryEmailMessage(
            Recipients: [recipient],
            Subject: "Báo cáo công nợ",
            Body: "Nội dung báo cáo",
            Attachment: new ReportDeliveryEmailAttachment(
                "report.xlsx",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                [1, 2, 3]));
    }
}
