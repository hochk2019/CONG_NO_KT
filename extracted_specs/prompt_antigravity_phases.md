> [!IMPORTANT]
> **HISTORICAL DOCUMENT**
> Tài liệu này là snapshot/lịch sử để tham khảo, **không phải nguồn vận hành chuẩn hiện tại**.
> Nguồn chuẩn hiện tại:
> - Deploy: DEPLOYMENT_GUIDE_DOCKER.md
> - Runbook: RUNBOOK.md
> - Ops runtime: docs/OPS_ADMIN_CONSOLE.md
# Prompt cho Agent Antigravity — Công nợ Golden (WEB + PostgreSQL + Windows LAN)

Bạn là Senior Solution Architect + Tech Lead + Full-stack Engineer.  
Dự án chạy LAN nội bộ: **Client = WEB (không cài trên từng máy)**, DB PostgreSQL trên Server1 (Windows).

## Input (đọc trước trong ZIP)
- app_cong_no.md
- db_schema_postgresql.sql
- openapi_golden_congno.yaml
- Mau_DoiSoat_CongNo_Golden.xlsx
- ReportDetail.xlsx
- Tính năng APP sẽ là.txt
- ACCEPTANCE_CRITERIA_BY_PHASE.md
- DEPLOYMENT_GUIDE_WEB_WINDOWS.md
- UI_PROTOTYPE_CongNoGolden_WEB.html

## Luật bắt buộc
1) Pass checklist `ACCEPTANCE_CRITERIA_BY_PHASE.md`.
2) Core bắt buộc: staging import, approve workflow, allocation engine (BY_INVOICE/BY_PERIOD/FIFO), receipt_date+applied_period_start, multi-seller, period lock, audit.
3) WEB-only: server-side pagination/filter/sort mọi list; PWA minimal; không làm WPF/Electron.
4) Không nhồi code: file<=300 LOC; function<=50 LOC; tách module rõ.
5) Hỏi tối đa 5 câu nếu thiếu; còn lại tự default và ghi assumptions.

---

# PHASE 1 — Architecture & Plan (Opus)
- TECH_DECISIONS.md, ARCHITECTURE.md, REPO_STRUCTURE.md, MODULE_BOUNDARIES.md, CODING_STANDARDS.md
- DB_REVIEW.md + INDEX_STRATEGY.md + MIGRATION_PLAN.md
- UX_NAVIGATION_MAP.md + WIREFRAMES.md + ROLE_MATRIX_UI.md
- API_CONTRACT_NOTES.md (bám openapi)

# PHASE 2 — Backend Core (Sonnet 4.5)
- Auth/RBAC/Ownership + Audit
- Import staging/preview/commit/rollback (Invoices + Advances)
- Receipts + AllocationEngine + unit tests
- Period locks + Reports endpoints + Export Excel

# PHASE 3 — Frontend WEB (Sonnet 4.5)
- React app shell + auth guard
- Shared DataTable (server paging/filter/sort)
- Import Wizard UI
- Advances UI
- Receipts UI + allocation preview (3 mode)
- Reports UI + export download
- PWA minimal

# PHASE 4 — Deploy & QA (Opus)
- PERFORMANCE_NOTES.md
- QA_REPORT.md (tick checklist)
- Deploy theo DEPLOYMENT_GUIDE_WEB_WINDOWS.md


