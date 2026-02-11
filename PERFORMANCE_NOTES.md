# PERFORMANCE_NOTES

## Goal
- All list endpoints are paged.
- Verify index usage with EXPLAIN ANALYZE.
- Dashboard response time target < 2s on large dataset.

## Test Environment
- DB host: localhost
- DB version: PostgreSQL 16.11 (Windows)
- Dataset size (customers/invoices/advances/receipts): 3 / 5 / 2 / 2
- Date: 2026-01-08
- Perf script: `scripts/db/perf_report.ps1` -> `tmp/perf/perf_report.txt`

## Key Queries / Endpoints
1) GET /customers?page=1&pageSize=20&search=...
2) GET /advances?page=1&pageSize=20&status=...
3) GET /imports/batches?page=1&pageSize=20
4) GET /reports/summary?from=...&to=...
5) GET /reports/statement?customerTaxCode=...&from=...&to=...
6) GET /reports/aging?asOfDate=...

## EXPLAIN ANALYZE Notes
Query plans captured via `scripts/db/perf_report.ps1`.

## Results
| Query | Rows | Duration | Index used | Notes |
| --- | --- | --- | --- | --- |
| summary_customer | 3 | 0.224 ms | seq scan (small dataset) | report summary |
| summary_seller | 1 | 0.091 ms | sellers_pkey | report summary |
| summary_period | 3 | 1.269 ms | seq scan (small dataset) | report summary |
| statement_opening | 1 | 0.045 ms | seq scan (small dataset) | report statement |
| statement_lines | 3 | 0.173 ms | users_pkey | report statement |
| aging | 3 | 0.264 ms | seq scan (small dataset) | report aging |

## Observations
- Execution times are < 2 ms on sample data.
- Seq scans are expected with very small row counts; date indexes are in place for larger data.

## Recommendations
- Re-run with production-sized dataset (30/90/365-day ranges) to confirm dashboard target.

## Status
- [x] Performance pass complete (sample dataset; re-run on large dataset)
