# TECH_DECISIONS - Cong No Golden

## Scope
This document records the technical decisions for the Cong No Golden system (LAN, web-only client, PostgreSQL).

## Decisions (with trade-offs)

1) Backend runtime
- Decision: .NET 8 Web API with Clean Architecture, deployed by default via Docker Compose.
- Why: consistent runtime between local/staging/prod, easier rollout/rollback, fewer host-specific drift issues.
- Trade-offs: requires container runtime and compose operational knowledge.

2) Frontend stack
- Decision: React + TypeScript + Vite, served by Nginx in `web` container with `/api` reverse proxy to backend.
- Why: single-origin access, stable cookie behavior, deployment parity with backend.
- Trade-offs: requires Nginx/container config management.

3) Database
- Decision: PostgreSQL 16, UUID primary keys, JSONB for audit/staging, pg_trgm + unaccent.
- Why: strong indexing, JSONB, stable on Windows.
- Trade-offs: must manage backup/restore and tuning.

4) ORM and data access
- Decision: EF Core for CRUD + transactional writes; Dapper for heavy reads and reports; Npgsql BinaryImporter for staging import.
- Why: EF for productivity, Dapper for performance, bulk import for large files.
- Trade-offs: two data access styles to maintain.

5) Auth and session
- Decision: short-lived JWT access token in memory (Authorization header); refresh token in HttpOnly cookie (SameSite=Lax, cookie path configurable by runtime: `/auth` direct API, `/` when behind `/api` proxy in Docker).
- Why: avoids localStorage XSS risk while keeping SPA UX; refresh flow handles reloads/long sessions.
- Trade-offs: refresh cookie requires CORS credentials and rotation logic; refresh endpoint must be protected.

6) Error format and logging
- Decision: RFC7807 ProblemDetails + application error codes. Serilog structured logging, correlation-id middleware.
- Why: consistent errors for FE and easy diagnostics.
- Trade-offs: need standard error code taxonomy.

7) Concurrency
- Decision: optimistic concurrency via `version` column for key aggregates.
- Why: prevents double-approve/void conflicts.
- Trade-offs: client must pass `version` on update.

8) Idempotency
- Decision: import commit requires `idempotency_key` and is idempotent.
- Why: safe retries for flaky network.
- Trade-offs: needs unique index and handling of conflict returns.

9) Cached balances
- Decision: maintain `customers.current_balance` and `outstanding_amount` on invoices/advances in application layer within transaction.
- Why: fast list and report performance.
- Trade-offs: requires strict update rules and test coverage.

10) Delete policy
- Decision: soft delete for invoices/advances/receipts; no physical delete from UI.
- Why: auditability and allocation integrity.
- Trade-offs: must filter deleted records in queries.

## Assumptions
- Concurrency is low (5-10 users) but correctness is critical.
- LAN/private network deployment with Docker Compose as canonical runtime.

## Open questions
- Receipt import template format and mapping (fields, default method).
