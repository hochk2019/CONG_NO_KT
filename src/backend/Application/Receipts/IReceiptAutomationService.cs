namespace CongNoGolden.Application.Receipts;

public interface IReceiptAutomationService
{
    Task RunAsync(CancellationToken ct);
}
