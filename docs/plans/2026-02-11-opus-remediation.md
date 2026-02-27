# Opus 4.6 Remediation Plan (2026-02-11)

> [!IMPORTANT]
> **HISTORICAL EXECUTION PLAN**
> Tài liệu này là kế hoạch/thực thi theo thời điểm viết, có thể chứa giả định cũ.
> Nguồn vận hành hiện hành: `DEPLOYMENT_GUIDE_DOCKER.md`, `RUNBOOK.md`, `task.md`.


## 1) Kết quả xác thực nhanh các nhận định CRITICAL/HIGH

| Hạng mục Opus | Trạng thái xác thực | Ghi chú |
|---|---|---|
| Thiếu rate limiting | `ĐÚNG` | Chưa có `AddRateLimiter`/`UseRateLimiter`, auth endpoints chưa giới hạn tần suất |
| JWT secret nằm trong config | `ĐÚNG` | `appsettings*.json` vẫn để placeholder, chưa có guard chặn production misconfig |
| Receipt approve thiếu transaction | `SAI (đã xử lý trước đó)` | `ReceiptService.ApproveAsync` đã có `BeginTransactionAsync` + `CommitAsync` |
| Import commit thiếu transaction | `SAI (đã xử lý trước đó)` | `ImportCommitService.CommitAsync` đã có transaction |
| Frontend chưa code-splitting | `SAI (đã xử lý trước đó)` | `App.tsx` dùng `lazy(...)` + `Suspense` + `pageLoaders` |
| Không responsive | `MỘT PHẦN` | Có breakpoints `1024/720`, nhưng chưa có nav collapse/hamburger rõ ràng |
| Thiếu CI/CD | `ĐÚNG` | Chưa có `.github/workflows/*` |
| Thiếu environment separation | `ĐÚNG` | Mới có base + development, chưa có production file chuẩn |
| current_balance có rủi ro lệch | `ĐÚNG` | Đang dùng cached field, chưa có reconcile job định kỳ |
| Zalo client chưa retry/circuit breaker | `ĐÚNG` | `ZaloClient` hiện gửi 1 lần, chưa có retry policy |

## 2) Kế hoạch triển khai theo pha

### Phase 1 (triển khai ngay)
- Security hardening:
  - Thêm rate limiting cho `/auth/login`, `/auth/refresh`.
  - Thêm guard validate `Jwt__Secret` (độ dài tối thiểu + chặn placeholder ngoài Development).
  - Harden refresh cookie (`SameSite`, `Path`) qua cấu hình.
- DevOps baseline:
  - Thêm GitHub Actions CI (build + test backend, lint/test/build frontend).
  - Bổ sung `appsettings.Production.json`.
  - Cập nhật tài liệu ENV/runbook cho secret management.

### Phase 2 (tiếp theo)
- Data integrity:
  - Thêm job reconcile `customers.current_balance`.
  - Bổ sung endpoint/admin action kiểm tra drift số dư.
- Reliability:
  - Thêm retry/backoff cho Zalo client (ưu tiên transient failures 5xx/network).
- UX:
  - Cải thiện responsive nav (collapse menu cho tablet/mobile).

### Phase 3 (refactor có kiểm soát)
- Giảm duplicate backend helper (`EnsureUser`, `ResolveOwnerFilter`, normalize methods).
- Tách nhỏ các service/page lớn theo bounded responsibilities.
- Giảm số API round-trips ở dashboard/risk/reports (composite endpoints khi phù hợp).

#### Tiến độ Phase 3
- ✅ Part 1 (2026-02-11): tạo helper dùng chung `CurrentUserAccessExtensions` và migrate `RiskService` + `ReminderService` sang helper chung.
- ✅ Part 2 (2026-02-11): migrate `AdvanceService`, `ReceiptService`, `DashboardService`, `PeriodLockService` sang `CurrentUserAccessExtensions` và bỏ helper cục bộ trùng lặp.
- ✅ Part 3 (2026-02-11): giảm round-trips ở dashboard/risk/reports.
  - ✅ Dashboard: giảm round-trips từ 3 call xuống 2 call bằng cách dùng chung response `fetchDashboardOverview` cho KPI + cashflow.
  - ✅ Reports: thêm endpoint composite `/reports/overview` để gom KPI + charts + insights thành 1 call.
  - ✅ Risk: thêm endpoint `/risk/bootstrap` để gom dữ liệu khởi tạo (overview/customers/rules/settings/logs/notifications/zalo).

## 3) Tiêu chí hoàn thành

- Pha 1:
  - Build + tests pass.
  - Có CI workflow chạy được trên PR.
  - Auth endpoints bị throttle khi spam request.
  - Production không khởi động nếu JWT secret sai chuẩn.
- Pha 2:
  - Có thể reconcile số dư an toàn, có log/audit.
  - Zalo gửi nhắc nợ chịu lỗi mạng tốt hơn.
- Pha 3:
  - Không tăng technical debt, có test cho phần refactor.

