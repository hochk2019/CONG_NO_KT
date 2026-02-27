namespace CongNoGolden.Application.Customers;

public interface ICustomerBalanceReconcileService
{
    Task<CustomerBalanceReconcileResult> ReconcileAsync(CustomerBalanceReconcileRequest request, CancellationToken ct);
}
