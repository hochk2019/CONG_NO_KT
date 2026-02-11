# QA_REPORT

Date: 2026-01-08
Environment: Local dev (Windows)
Tester: Codex CLI

## Summary
- Total cases: 7 (smoke)
- Pass: 7
- Fail: 0
- Blocked: 0

## Checklist status
- QA_CHECKLIST.md: PARTIAL (smoke only)
- UAT_SCRIPT.md: NOT RUN
- scripts/e2e/smoke.ps1: PASSED
- Performance report: DONE (sample dataset)
- Backup/restore test: PASSED (dump + restore to temp DB)

## Issues
- None

## Evidence
- Backup: `tmp/backup/dumps/congno_golden_20260108_211906.dump`
- Offsite copy: `tmp/backup/offsite/congno_golden_20260108_211906.dump`
- Restore verification: roles=4, users=2
- Perf report: `tmp/perf/perf_report.txt`
- Smoke re-run: 2026-01-08, BaseUrl http://localhost:8080 (PASS)
