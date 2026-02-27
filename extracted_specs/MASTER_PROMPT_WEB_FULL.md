> [!IMPORTANT]
> **HISTORICAL DOCUMENT**
> Tài liệu này là snapshot/lịch sử để tham khảo, **không phải nguồn vận hành chuẩn hiện tại**.
> Nguồn chuẩn hiện tại:
> - Deploy: DEPLOYMENT_GUIDE_DOCKER.md
> - Runbook: RUNBOOK.md
> - Ops runtime: docs/OPS_ADMIN_CONSOLE.md
# MASTER_PROMPT_WEB_FULL — Công nợ Golden (WEB + PostgreSQL + Windows LAN)

Bạn là Senior Solution Architect + Tech Lead + Full-stack Engineer.  
Triển khai hệ thống Công nợ Golden chạy LAN, **Client = WEB (không cài đặt)**.

## Input bắt buộc (đọc trước)
- app_cong_no.md
- db_schema_postgresql.sql
- openapi_golden_congno.yaml
- Mau_DoiSoat_CongNo_Golden.xlsx
- ReportDetail.xlsx
- Tính năng APP sẽ là.txt
- ACCEPTANCE_CRITERIA_BY_PHASE.md
- DEPLOYMENT_GUIDE_WEB_WINDOWS.md
- UI_PROTOTYPE_CongNoGolden_WEB.html

## Non‑negotiables
1) Import: staging -> preview (OK/WARN/ERROR) -> commit; file_hash; supports INVOICE/ADVANCE/RECEIPT.
2) Approve workflow Trả hộ + Thu tiền theo ownership.
3) AllocationEngine 3 mode: BY_INVOICE / BY_PERIOD / FIFO.
4) Thu tiền có receipt_date + applied_period_start.
5) Multi-seller, period lock, audit before/after JSONB.
6) WEB-only: server-side pagination/filter/sort; PWA minimal.
7) Không nhồi code: file<=300 LOC; function<=50 LOC; controller mỏng.

8) Import commit idempotent (idempotency_key) + cached balances (current_balance/outstanding_amount).
9) Không import receipts ở phase hiện tại (receipts nhập tay).

## Output format
Mỗi phase: ticket list + artefacts + tick acceptance + assumptions + risks.
Nếu thiếu thông tin: hỏi tối đa 5 câu.

Bắt đầu PHASE 1 theo `prompt_antigravity_phases.md`.


