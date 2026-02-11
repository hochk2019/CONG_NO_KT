# MODULE_BOUNDARIES - Cong No Golden

## Modules (Domain)
- Auth: users, roles, login, policies.
- Master: sellers, customers, ownership.
- Import: batches, staging rows, validations, commit/rollback.
- Invoices: invoices, status, adjustments.
- Advances: advance workflow.
- Receipts: receipt workflow.
- Allocation: allocation engine, preview, allocations.
- Reports: summary, statement, aging, export.
- PeriodLocks: lock/unlock policies.
- Audit: audit logs.

## Application services
Each module has:
- Commands (create/approve/void/commit)
- Queries (list/detail/report)
- Validators
- Policies (ownership, period lock)

## Cross-cutting
- Logging, correlation id, error handling
- RBAC/ownership authorization
- Idempotency
- Caching updates (current_balance, outstanding_amount)

## Dependency rules
- Domain depends on nothing else.
- Application depends on Domain only.
- Infrastructure depends on Application + Domain.
- API depends on Application only.

## Forbidden imports (examples)
- Domain -> Infrastructure (not allowed)
- Application -> API (not allowed)
- Infrastructure -> API (not allowed)
