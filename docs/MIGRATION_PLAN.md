# MIGRATION_PLAN - Cong No Golden

## Tooling
- Use DbUp (SQL migration scripts) for full control of PostgreSQL schema.
- Keep schema in `/scripts/db/migrations/` with incremental numbering.

## Baseline
- 001_init.sql: derived from `db_schema_postgresql.sql`.
- 002_seed_roles.sql: insert roles.
- 003_seed_sellers.sql: insert default sellers.
- 004_refresh_tokens.sql: add refresh token storage.
- 005_customers_name_search.sql: add name_search + trigram index for unaccent search.
- 006_report_indexes.sql: add reporting indexes for date/status filters.
- 007_risk_reminders.sql: add risk rules, reminder settings/logs, notifications.
- 008_zalo_link.sql: add Zalo user_id + link tokens.
- 009_import_cancel.sql: add cancel metadata for import batches.
- 010_import_status_cancelled.sql: allow CANCELLED in import batch status check.
- 011_perf_indexes.sql: add performance indexes for import/reminder staging.

## Process
1) Create new migration script for every schema change.
2) Apply migrations on startup (or via CI step) with locking.
3) Use an admin connection string for migrations; keep app role least-privilege.
4) Always take backup before applying migrations in production.

## Rollback strategy
- Prefer forward-only migrations.
- For destructive changes, provide paired rollback script and require manual approval.
- Use pg_dump backup for full restore.
