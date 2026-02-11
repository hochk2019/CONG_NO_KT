namespace Ops.Shared.Models;

public sealed record BackendJobSettingsDto(bool RemindersEnabled, bool InvoiceReconcileEnabled);

public sealed record BackendJobSettingsUpdateRequest(bool RemindersEnabled, bool InvoiceReconcileEnabled);
