# API_CONTRACT_NOTES - Cong No Golden

## General
- Use REST + JSON. All list endpoints support pagination: page, pageSize, sort, order.
- Standard response wrapper:
  - data, meta (pagination), error (ProblemDetails)
- Use ProblemDetails with error codes (ex: PERIOD_LOCKED, OWNERSHIP_DENIED).
- All update endpoints require `version` for optimistic concurrency.

## Import
- POST /imports/upload (multipart) -> batchId
- GET /imports/{batchId}/preview (paged)
- POST /imports/{batchId}/commit (body: idempotency_key, override_period_lock, override_reason)
- POST /imports/{batchId}/rollback (admin, body: override_period_lock, override_reason)

- Import type includes RECEIPT (creates DRAFT receipts on commit).
## Invoices / Advances
- GET list with filters (customer, seller, date range, status)
- POST create draft (if needed)
- POST approve / void with reason
 - POST /advances
 - POST /advances/{id}/approve
 - POST /advances/{id}/void

## Receipts
- POST create draft
- POST preview allocations (for draft)
- POST approve (transaction: allocations + cache updates, override_period_lock, override_reason)
- POST /receipts/{id}/void (reason, override_period_lock, override_reason)
- POST void (reversal policy)

## Period Locks
- GET /period-locks
- POST /period-locks (lock)
- POST /period-locks/{id}/unlock

## Reports
- GET /reports/summary
- GET /reports/statement
- GET /reports/aging
- GET /reports/export (template)

## Auth
- POST /auth/login
- POST /auth/refresh
- POST /auth/logout
- Access token returned in response body; refresh token stored in HttpOnly cookie (path `/auth`, SameSite=Lax).
