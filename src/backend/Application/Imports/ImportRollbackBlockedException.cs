namespace CongNoGolden.Application.Imports;

public sealed class ImportRollbackBlockedException : Exception
{
    public ImportRollbackBlockedException(
        string reason,
        string detail,
        Guid batchId,
        IReadOnlyDictionary<string, object?>? data = null)
        : base(detail)
    {
        Reason = reason;
        BatchId = batchId;
        ContextData = data ?? new Dictionary<string, object?>();
    }

    public string Reason { get; }

    public Guid BatchId { get; }

    public IReadOnlyDictionary<string, object?> ContextData { get; }
}
