namespace CongNoGolden.Application.Common;

public sealed class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message)
    {
    }
}
