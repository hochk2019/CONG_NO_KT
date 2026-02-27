> [!IMPORTANT]
> **HISTORICAL DOCUMENT**
> Tài liệu này là snapshot/lịch sử để tham khảo, **không phải nguồn vận hành chuẩn hiện tại**.
> Nguồn chuẩn hiện tại:
> - Deploy: DEPLOYMENT_GUIDE_DOCKER.md
> - Runbook: RUNBOOK.md
> - Ops runtime: docs/OPS_ADMIN_CONSOLE.md
# ĐÁNH GIÁ TOÀN DIỆN DỰ ÁN CONG NO GOLDEN
## Hệ thống Theo dõi Thu hồi Công nợ & Cảnh báo Công nợ Đến hạn

> **Vai trò đánh giá**: Senior Solution Architect + Tech Lead + DevOps Reviewer  
> **Ngày đánh giá**: 2026-02-11  
> **Phiên bản**: Opus 4.6 Review  
> **Mục đích**: Đánh giá toàn diện để agent Codex xác nhận và triển khai sửa chữa

---

## 🔄 Cập nhật trạng thái remediation (Codex, 2026-02-12)

| Hạng mục | Trạng thái | Chi tiết triển khai |
|---|---|---|
| Responsive layout (sidebar collapsible/hamburger) | ✅ **Đã xử lý** | Cập nhật `src/frontend/src/layouts/AppShell.tsx`, `src/frontend/src/index.css`; thêm test `src/frontend/src/layouts/__tests__/app-shell.test.tsx` |
| `current_balance` reconciliation | ✅ **Đã xử lý** | Thêm reconcile service + hosted job + admin endpoint/manual trigger: `src/backend/Infrastructure/Services/CustomerBalanceReconcileService.cs`, `src/backend/Api/Services/CustomerBalanceReconcileHostedService.cs`, `src/backend/Api/Endpoints/AdminEndpoints.cs` |
| Retry/backoff cho Zalo API | ✅ **Đã xử lý** | Bổ sung retry/backoff có cấu hình trong `src/backend/Infrastructure/Services/ZaloClient.cs` + test `src/backend/Tests.Unit/ZaloClientRetryTests.cs` |
| Rate limiting cho auth endpoints | ✅ **Đã xử lý** | Đã thêm rate limiting cho `/auth/login`, `/auth/refresh` trong backend |
| CI + environment separation | ✅ **Đã xử lý** | Đã có `.github/workflows/ci.yml` và bổ sung config production (`appsettings.Production.json`, docs env/run) |
| Shared backend helpers (`EnsureUser`, `ResolveOwnerFilter`) | ✅ **Đã xử lý (xác nhận 2026-02-11)** | Đã tách helper dùng chung và audit service phụ trợ; không còn helper cục bộ trùng lặp |

## 🧭 Kế hoạch thực hiện tiếp (Codex, 2026-02-11)

> Nguồn theo dõi chuẩn cho vòng tiếp theo: `docs/plans/2026-02-11-opus-follow-up-hardening-plan.md` + `task.md` (Phase 18) + beads.
>  
> Ghi chú: một số nhận định ở các section sâu phía dưới là snapshot ban đầu; bảng dưới là trạng thái vận hành hiện tại (override).

| Hạng mục còn lại | Quyết định | Trạng thái hiện tại | Lý do / ghi chú |
|---|---|---|---|
| JWT secret externalization + production fail-fast | Làm ngay (P1) | ✅ Đã xử lý (2026-02-11) | Đã bỏ `Jwt:Secret` khỏi `appsettings*.json` tracked, chỉ nhận từ config ngoài + fail-fast khi secret yếu/placeholder |
| Password complexity + refresh token absolute expiry | Làm ngay (P1) | ✅ Đã xử lý (2026-02-11) | Thêm password complexity cho `/admin/users`; refresh token rotation có absolute expiry cap |
| Explicit transaction cho `ReceiptService.ApproveAsync` | Làm ngay (P1) | ✅ Đã xác nhận (2026-02-11) | `ApproveAsync` đã dùng `BeginTransactionAsync` + `CommitAsync`; không cần patch bổ sung |
| Frontend route-level code splitting | Làm ngay (P1) | ✅ Đã xác nhận (2026-02-11) | `src/frontend/src/App.tsx` đã dùng `React.lazy` + `Suspense` qua `pageLoaders` |
| Shared helper audit cho service phụ trợ còn lại | Làm trong đợt này (P2) | ✅ Đã xác nhận (2026-02-11) | Đã audit, không còn helper cục bộ `EnsureUser/ResolveOwnerFilter` trùng lặp ngoài extension chung |
| Baseline observability (metrics/tracing/readiness detail) | Làm trong đợt này (P2) | ✅ Đã xử lý (2026-02-11) | Thêm OpenTelemetry (metrics/tracing) theo config + mở rộng `/health/ready` trả `checks` |
| Zalo circuit breaker nâng cao | Làm trong đợt này (P2) | ✅ Đã xử lý (2026-02-11) | Thêm circuit breaker có ngưỡng/cooldown cấu hình được, giữ retry/backoff hiện có |
| IP/device binding cho refresh token | Làm trong đợt này (P2) | ✅ Đã xử lý (2026-02-11) | Thêm dual-signal binding (device fingerprint + IP prefix), chỉ chặn khi lệch đồng thời cả 2 tín hiệu để giảm false-positive |
| DB partition + retention automation | Thực hiện trong vòng Opus execution | ✅ Done (2026-02-12) | Hoàn tất retention automation + migration `022_audit_logs_partition.sql` để partition `congno.audit_logs` theo tháng và tự tạo partition kế tiếp |
| Containerization (Docker) | Thực hiện | ✅ Done (2026-02-12) | Đã thêm Dockerfile backend/frontend, `.dockerignore`, `docker-compose.yml`, `.env.docker.example`, `DEPLOYMENT_GUIDE_DOCKER.md` |
| AI risk scoring / dự báo trễ hạn | Hoàn tất baseline + ML training pipeline | ✅ Done (2026-02-12) | Đã triển khai model registry + training runs + scheduler retrain + admin endpoints train/activate/list (`risk_ml_models`, `risk_ml_training_runs`, `IRiskAiModelService`) và tích hợp inference có fallback an toàn về `RiskAiScorer` |

## 🧪 Cập nhật vận hành staging + rollout Docker (Codex, 2026-02-12)

- ✅ **Migration staging đã chạy xong**: tạo backup trước migrate (`C:\apps\congno\backup\ops\congno_20260212_085421.dump`), sau đó áp đủ script `019`→`023` và verify schema (`absolute_expires_at`, binding context, `risk_ml_models`, `risk_ml_training_runs`).
- ✅ **Smoke test `/admin/risk-ml/*` đã pass**: chạy list/train/activate trên staging; training run `SUCCEEDED` với dữ liệu thực (287 samples) và active model `v1`.
- ✅ **Rollout Docker đã pass**: build + `docker compose up -d`, fix mount scripts migrations vào API container, health check đều xanh (`/health`, `/health/ready`, frontend web).
- ⚠️ **Ghi chú vận hành Ops Agent**: bản agent đang cài tại staging có dấu hiệu build cũ (`/runtime/info` trả `404`) và tác vụ service control cần quyền cao hơn. Hệ thống vẫn chạy được theo Docker compose, nhưng cần nâng cấp/restart service agent bằng tài khoản có quyền để full-control từ Ops Console.

## 🛠️ Hotfix backup/restore compatibility (Codex, 2026-02-12)

- ✅ **Đã tái hiện lỗi thực tế** khi restore dump cũ trên Docker: conflict `constraint/table/schema` + role owner + API lỗi schema sau restore.
- ✅ **Đã fix backend restore flow**:
  - reset schema trước restore (`DROP SCHEMA IF EXISTS congno CASCADE`);
  - `pg_restore` dùng cờ portable: `--clean --if-exists --no-owner --no-privileges --exit-on-error`;
  - `pg_dump` dùng `-O -x` để backup mới không phụ thuộc owner/privileges.
- ✅ **Đã thêm bước auto-migrate sau restore** để nâng schema dump cũ lên version runtime hiện tại.
- ✅ **Đã verify end-to-end**: `upload -> restore -> login` chạy thành công, `maintenance=false` sau restore.

---

## Mục lục

1. [Phân tích tổng quan hệ thống](#1️⃣-phân-tích-tổng-quan-hệ-thống)
2. [Review UI/UX](#2ï¸âƒ£-review-uiux)
3. [Review Frontend](#3ï¸âƒ£-review-frontend)
4. [Review Backend](#4ï¸âƒ£-review-backend)
5. [Review Database](#5ï¸âƒ£-review-database)
6. [Review DevOps & Vận hành](#6️⃣-review-devops--vận-hành)
7. [Review Báº£o máº­t](#7ï¸âƒ£-review-báº£o-máº­t)
8. [Tính đồng bộ Frontend–Backend–Database](#8️⃣-tính-đồng-bộ-frontend--backend--database)
9. [Code thừa & Technical Debt](#9️⃣-code-thừa--technical-debt)
10. [Đề xuất cải tiến](#🔟-đề-xuất-cải-tiến)

---

## 1️⃣ Phân tích tổng quan hệ thống

### 1.1 Mục tiêu dự án

Hệ thống quản lý, theo dõi và thu hồi công nợ khách hàng cho doanh nghiệp thương mại. Giúp kế toán, ban giám đốc có cái nhìn tổng quan về tình trạng công nợ, phát hiện sớm rủi ro trễ hạn, tự động nhắc nợ và hỗ trợ đối soát chi tiết.

### 1.2 Đối tượng sử dụng

| Vai trò | Quyền hạn chính |
|---------|-----------------|
| **Admin** | Toàn quyền: quản lý user, backup/restore, import commit, khóa kỳ, cấu hình hệ thống |
| **Supervisor** | Import, phê duyệt receipt, quản lý khách hàng, xem báo cáo, cấu hình risk/reminder |
| **Accountant** | Upload import, tạo receipt/advance, xem báo cáo, xem risk |
| **Viewer** | Chỉ xem: báo cáo, dashboard, danh sách khách hàng, risk overview |

### 1.3 Các tính năng chính

- **Import dữ liệu**: Upload Excel hóa đơn/tạm ứng/thanh toán → staging → preview → commit. Hỗ trợ idempotency key, dedup, rollback
- **Quản lý hóa đơn**: NORMAL/REPLACE/ADJUST, soft delete, credit note reconciliation tự động
- **Thu tiền (Receipt)**: Tạo → preview allocation → approve. Hỗ trợ 4 chế độ phân bổ: FIFO, By Invoice, By Period, Manual
- **Tạm ứng (Advance)**: CRUD với workflow DRAFT → APPROVED → PAID → VOID
- **Khóa kỳ (Period Lock)**: MONTH/QUARTER/YEAR, chặn thao tác lên dữ liệu đã khóa
- **Dashboard**: KPI tổng hợp, biểu đồ xu hướng dòng tiền, top khách nợ lâu, phân nhóm quá hạn
- **Báo cáo**: Tổng hợp, chi tiết, aging analysis, biểu đồ, insights, xuất Excel (5 template)
- **Risk Alerts**: Phân loại rủi ro 4 cấp (VERY_HIGH/HIGH/MEDIUM/LOW), rules engine cấu hình được
- **Nhắc nợ (Reminder)**: Tự động theo lịch, qua IN_APP + Zalo, hỗ trợ cảnh báo nợ sắp đến hạn
- **Notification Center**: In-app notifications, đánh dấu đã đọc, cấu hình severity/source preferences
- **Quản trị**: CRUD users, audit log, health check, backup/restore với pg_dump/pg_restore
- **Tích hợp Zalo**: Gửi thông báo qua Zalo OA, webhook nhận reply

### 1.4 Luồng nghiệp vụ chính

```
Excel Upload → Staging + Validation → Preview → Commit (tạo Invoice/Advance/Receipt)
                                                    ↓
                                        Tự động tạo Customer nếu chưa có
                                        Cáº­p nháº­t current_balance
                                                    ↓
Receipt → Preview Allocation → Approve → Áp dụng vào Invoice/Advance
                                                    ↓
                                        Cáº­p nháº­t outstanding_amount + status
                                                    ↓
Hệ thống tự động: RiskClassifier → ReminderService → Gửi IN_APP / Zalo
                                                    ↓
Dashboard + Reports ← Query realtime từ database
```

### 1.5 Kiến trúc tổng thể

**Kiến trúc**: Clean Architecture (3 tầng riêng biệt + Domain layer thuần)

```
┌─────────────────────────────────────────────────────┐
│                    Api Layer                         │
│  (Endpoints, Middleware, Program.cs, HostedServices) │
├─────────────────────────────────────────────────────┤
│               Application Layer                      │
│  (Interfaces, DTOs, Request/Response models)         │
├─────────────────────────────────────────────────────┤
│                 Domain Layer                         │
│  (AllocationEngine, RiskClassifier, Value Objects)   │
├─────────────────────────────────────────────────────┤
│              Infrastructure Layer                    │
│  (EF Core, Dapper, Services, Data Entities)          │
└─────────────────────────────────────────────────────┘
```

**Công nghệ**:

| Layer | Công nghệ |
|-------|-----------|
| Frontend | React 18 + TypeScript + Vite |
| Backend | .NET 8 Minimal API |
| Database | PostgreSQL 16 |
| ORM | EF Core (CRUD) + Dapper (queries phức tạp) |
| Auth | JWT Bearer + BCrypt + Refresh Token Rotation |
| Logging | Serilog (file-based) |
| Hosting | Windows Service **hoặc** Docker Compose (đã có cả 2 runtime) |
| Messaging | Zalo OA API |

### 1.6 Đánh giá tổng quan

| Tiêu chí | Đánh giá | Ghi chú |
|-----------|----------|---------|
| Phù hợp quy mô | ✅ **Tốt** | Phù hợp SME 10-50 users, đơn vị kế toán nhỏ-vừa |
| Khả năng mở rộng | ⚠️ **Trung bình** | Monolith single-server, chưa có caching, chưa hỗ trợ multi-tenant |
| Dễ bảo trì | ✅ **Tốt** | Clean Architecture rõ ràng, tách biệt concerns tốt |

---

## 2ï¸âƒ£ Review UI/UX

### 2.1 Phân tích chi tiết

| Tiêu chí | Đánh giá | Chi tiết |
|-----------|----------|----------|
| Tính trực quan | ✅ **Tốt** | Sidebar navigation rõ ràng, breadcrumbs đầy đủ, KPI cards nổi bật |
| Phù hợp người dùng phổ thông | ⚠️ **Trung bình** | Nhiều trang phức tạp (ReportsPage 1018 dòng), có thể gây choáng ngợp |
| Nhất quán giao diện | ✅ **Tốt** | Design system 2732 dòng CSS với CSS variables thống nhất, fonts (IBM Plex Sans, Space Grotesk) |
| Responsive | ✅ **Đã cải thiện (2026-02-11)** | Đã bổ sung responsive layout cho AppShell (mobile nav toggle, overlay, collapse sidebar) |
| Cảnh báo đến hạn | ✅ **Tốt** | Risk pills 4 màu (VERY_HIGH/HIGH/MEDIUM/LOW), notification bell + badge |
| Dashboard | ✅ **Tốt** | KPI cards, trend chart granularity (week/month), top overdue, overdue groups |
| Luồng thao tác | ⚠️ **Trung bình** | Import flow có nhiều bước (upload → staging → preview → commit), nhưng mỗi bước rõ ý nghĩa |

### 2.2 Điểm chưa hợp lý

1. ~~**`CRITICAL`** — Không có responsive layout cho mobile/tablet. `app-shell` fix cứng `grid-template-columns: 260px 1fr`, sidebar không collapse được~~  
   ✅ **Đã xử lý (2026-02-11)** — AppShell có chế độ sidebar collapse/hamburger cho mobile/tablet
2. Trang ReportsPage quá nặng (1018 dòng), quá nhiều state variables → khó navigate cho người dùng
3. Dashboard thiếu hỗ trợ dark mode
4. Luồng tạo Receipt phải chọn allocation mode trước khi biết preview → có thể gây confuse
5. Notification panel max-height 320px, khi nhiều notification sẽ khó quản lý
6. Không có onboarding/hướng dẫn cho người dùng mới

### 2.3 Đề xuất cải tiến

| # | Đề xuất | Ưu tiên |
|---|---------|---------|
| 1 | Thêm responsive layout: sidebar collapsible, mobile-first grid | **High** |
| 2 | Tách ReportsPage thành sub-routes (Summary, Statement, Aging riêng) | **High** |
| 3 | Thêm dark mode toggle | **Medium** |
| 4 | Thêm tooltip/hướng dẫn cho các thuật ngữ chuyên ngành | **Medium** |
| 5 | Thêm skeleton loaders thay vì chỉ hiện "Đang tải..." | **Low** |

---

## 3ï¸âƒ£ Review Frontend

### 3.1 Cấu trúc project

```
src/
├── api/          (23 files) — API modules, typed OpenAPI client
├── assets/       (1 file)
├── components/   (6 files) — DataTable, LookupInput, notifications/
├── context/      (6 files) — AuthContext, NotificationCenterContext
├── hooks/        (1 file)
├── layouts/      (2 files) — AppShell
├── pages/        (65 files) — Page components + sub-directories
├── utils/        (3 files)
├── App.tsx       — Routing + role guards
├── main.tsx      — Entry point
└── index.css     — Design system (2732 lines)
```

### 3.2 Phân tích

| Tiêu chí | Đánh giá | Chi tiết |
|-----------|----------|----------|
| Tách component | ⚠️ **Trung bình** | Chỉ 6 shared components. Nhiều logic render lặp lại trong pages |
| State management | ⚠️ **Trung bình** | Dùng `useState` + `useEffect` thuần, không có global state manager. Mỗi page tự quản lý fetch + state |
| Error handling | ✅ **Tốt** | `ApiError` class + `formatApiErrorMessage` + try/catch nhất quán |
| Form validation | ⚠️ **Trung bình** | Client-side validation cơ bản, không dùng validation library (zod, yup) |
| API calls | ✅ **Tốt** | Centralized `apiFetch` + `apiFetchBlob`, OpenAPI types (`openapi.d.ts` - 67KB) |
| Loading/retry/timeout | ⚠️ **Trung bình** | Có loading state, có AbortSignal support, có retry trong AuthContext bootstrap. Nhưng không có global retry/timeout |
| Performance | ⚠️ **Trung bình** | Đã có `React.lazy`/`Suspense` + prefetch theo route; vẫn còn dư địa tối ưu memoization/table render |
| Code duplicate | ⚠️ **Trung bình** | Pattern fetch-in-useEffect lặp lại ở mọi page, page size storage logic trùng |

### 3.3 Điểm cần lưu ý

~~**`CRITICAL`** — Thiếu code-splitting~~  
✅ **Đã xử lý (đợt trước, xác nhận lại 2026-02-11)**:
- `App.tsx` dùng `React.lazy` + `Suspense`.
- Route loaders + prefetch đã được triển khai (`src/frontend/src/pages/pageLoaders.ts`).

**Cần cải thiện** — Không có custom hooks cho fetching patterns:
```typescript
// Pattern lặp lại ở >15 pages:
const [data, setData] = useState(null)
const [loading, setLoading] = useState(true)
const [error, setError] = useState(null)
useEffect(() => { /* fetch -> setData -> setLoading(false) */ }, [deps])
// → Nên extract thành useQuery hook
```

**Cần cải thiện** — State management:
- `DashboardPage.tsx`: 774 dòng, ~15 useState hooks
- `ReportsPage.tsx`: 1018 dòng, ~30 useState hooks
- `RiskAlertsPage.tsx`: 784 dòng, ~20 useState hooks
- → Nên dùng `useReducer` hoặc state management library

### 3.4 Đánh giá

| Tiêu chí | Đánh giá |
|-----------|----------|
| Dễ mở rộng | ⚠️ **Trung bình** — Shared components quá ít, mỗi page tự xây dựng logic riêng |
| Clean code | ⚠️ **Trung bình** — Các page file quá lớn, nên tách thành smaller modules |

---

## 4ï¸âƒ£ Review Backend

### 4.1 Kiến trúc

**Đánh giá: ✅ Tốt**

Clean Architecture tuân thủ nghiêm ngặt:
- **Api**: 16 endpoint files sử dụng Minimal API, `ApiErrors` centralized
- **Application**: 15 modules, mỗi module có interface + DTOs
- **Domain**: Pure business logic (AllocationEngine, RiskClassifier) — không dependency
- **Infrastructure**: 60 service files, EF Core + Dapper hybrid

DI registration (`DependencyInjection.cs`): 22+ scoped services, clean và tường minh.

### 4.2 Phân tích chi tiết

| Tiêu chí | Đánh giá | Chi tiết |
|-----------|----------|----------|
| Controller-Service-Repository | ✅ **Tốt** | Endpoint → Service (via interface) → EF Core/Dapper. Không có Repository pattern riêng nhưng đủ cho quy mô |
| Logic công nợ | ✅ **Tốt** | AllocationEngine with FIFO/ByInvoice/ByPeriod/Manual modes, invoice credit reconciliation tự động |
| Logic tính ngày đến hạn | ✅ **Tốt** | `payment_terms_days` per customer, RiskClassifier tính `MaxDaysPastDue`, `OverdueRatio`, `LateCount` |
| Transaction control | ⚠️ **Trung bình** | Dùng EF Core SaveChangesAsync (implicit transaction). Import commit dùng single SaveChanges cho toàn batch — tốt. Nhưng Receipt approve + allocation không wrapped explicit transaction |
| Server-side validation | ✅ **Tốt** | Validate trong service layer, import staging có validation pipeline (OK/WARN/ERROR) |
| Logging | ✅ **Tốt** | Serilog với file rotation (14 ngày, 10MB/file), enrichment từ LogContext |
| Exception handling | ✅ **Tốt** | `ApiErrors.FromException()` pattern-matching centralized, ProblemDetails format |
| API security | ✅ **Tốt** | 17 authorization policies, role-based trên từng endpoint group |

### 4.3 Ưu điểm nổi bật

1. **EF Core + Dapper hybrid**: EF Core cho CRUD, Dapper cho complex reporting SQL → optimal performance
2. **AllocationEngine**: Pure domain logic, testable, hỗ trợ 4 modes phân bổ thanh toán
3. **RiskClassifier**: Configurable rules, level-based matching, database-driven rules
4. **Hosted Services**: 4 background services (Reminder scheduler, Invoice reconciliation, Backup scheduler, Backup worker)
5. **MaintenanceMiddleware**: Chặn requests during restore, cho phép health + auth + backup qua
6. **Import pipeline**: Idempotency key, file hash dedup, staging → preview → commit → rollback workflow

### 4.4 Điểm cần cải thiện

~~**`CRITICAL`** — Receipt approve transaction safety~~  
✅ **Đã xử lý (xác nhận 2026-02-11)**:
- `ReceiptService.ApproveAsync` đã có `BeginTransactionAsync` + `CommitAsync` cho luồng approve cốt lõi.
- Không cần bổ sung patch transaction ở vòng follow-up này.

**Cần cải thiện** — Service files quá lớn:
- `ReceiptService.cs`: 809 dòng (đã tách thành 6 partial files, tốt)
- `ReportService`: 7 partial files — tốt nhưng mỗi file vẫn lớn
- `BackupService.cs`: 838 dòng — nên tách thêm
- `ReminderService.cs`: 688 dòng — nên tách

**Cần cải thiện** — `RiskClassifier.Matches()` dùng OR logic:
```csharp
private static bool Matches(RiskMetrics metrics, RiskRule rule)
{
    return metrics.MaxDaysPastDue >= rule.MinOverdueDays
        || metrics.OverdueRatio >= rule.MinOverdueRatio  // OR logic
        || metrics.LateCount >= rule.MinLateCount;
}
// Vấn đề: Customer chỉ cần 1 điều kiện match là được classify
// Nên hỗ trợ AND/OR configurable per rule
```

~~**Cần cải thiện** — Không có retry/circuit breaker cho Zalo API calls~~  
✅ **Đã xử lý đầy đủ (2026-02-11)** — Zalo client đã có retry/backoff + circuit breaker có ngưỡng/cooldown cấu hình được

**Code thừa** — `ImportCommitService` có 2 method `TryBuildInvoiceKey` trùng tên (dòng 288 và 432), overload nhưng gây confuse

### 4.5 Đánh giá

| Tiêu chí | Đánh giá |
|-----------|----------|
| Code thừa | 🟡 **Đang giảm dần** — Đã tách helper chung cho `EnsureUser/ResolveOwnerFilter` và áp dụng cho nhóm service chính (Risk/Reminder/Advance/Receipt/Dashboard/PeriodLock) |
| Logic trùng lặp | 🟡 **Đang giảm dần** — Trùng lặp trọng yếu đã giảm; còn một số helper cục bộ ở service phụ trợ cần đánh giá tiếp |
| Technical debt | ⚠️ **Trung bình** — Service files lớn, thiếu explicit transactions ở một số critical flows |

---

## 5ï¸âƒ£ Review Database

### 5.1 Thiáº¿t káº¿ báº£ng

**Đánh giá: ✅ Tốt**

18 migration scripts, schema `congno`, thiết kế hợp lý:

| Bảng | PK | Mô tả |
|------|-----|-------|
| `users` | UUID | Quản lý tài khoản, BCrypt password hash |
| `roles` | Serial | 4 roles: Admin, Supervisor, Accountant, Viewer |
| `user_roles` | Composite (user_id, role_id) | N-N relationship |
| `sellers` | Tax code (varchar) | Đơn vị bán (natural key) |
| `customers` | Tax code (varchar) | Khách hàng, có `current_balance` cached |
| `import_batches` | UUID | Upload tracking, JSONB `summary_data` |
| `import_staging_rows` | UUID | Preview rows, JSONB `raw_data` + `validation_messages` |
| `invoices` | UUID | Hóa đơn, soft delete, NORMAL/REPLACE/ADJUST types |
| `advances` | UUID | Tạm ứng, soft delete |
| `receipts` | UUID | Thu tiền, 4 allocation modes, JSONB `allocation_targets` |
| `receipt_allocations` | UUID | Phân bổ thanh toán, FK đến invoice hoặc advance |
| `period_locks` | UUID | Khóa kỳ kế toán |
| `audit_logs` | UUID | JSONB before/after snapshots |
| `risk_rules` | UUID | Configurable risk levels |
| `reminder_settings` | UUID | Singleton pattern cho settings |
| `reminder_logs` | UUID | Log từng lần gửi nhắc nợ |
| `notifications` | UUID | In-app notifications |
| `backup_*` | UUID | 4 báº£ng cho backup system |

### 5.2 Phân tích chi tiết

| Tiêu chí | Đánh giá | Chi tiết |
|-----------|----------|----------|
| Chuẩn hóa | ✅ **Tốt** | 3NF, tách rõ seller/customer/invoice/receipt |
| Quan hệ | ✅ **Tốt** | FK constraints đầy đủ, ON DELETE CASCADE cho child tables |
| Index | ✅ **Tốt** | Composite indexes, partial indexes (WHERE deleted_at IS NULL), GIN trigram index cho search |
| Deadlock | ✅ **Tốt** | Advisory lock cho backup/restore, optimistic concurrency via `version` column |
| Audit log | ✅ **Tốt** | JSONB before/after data, user_id, action, entity tracking |
| Soft delete | ✅ **Tốt** | `deleted_at`/`deleted_by` trên invoices, advances, receipts |
| Dedup | ✅ **Tốt** | Unique index trên `(seller, customer, series, no, date) WHERE deleted_at IS NULL` |
| Optimistic concurrency | ✅ **Tốt** | `version` column trên users, sellers, customers, invoices, advances, receipts |
| CHECK constraints | ✅ **Tốt** | Status enums, amount validation cho adjust invoices |
| Triggers | ✅ **Tốt** | `set_updated_at()` function + triggers trên major tables |

### 5.3 Điểm cần cải thiện

~~**`CRITICAL`** — `current_balance` trên `customers` là cached field nhưng KHÔNG có mechanism tự động đảm bảo consistency~~  
✅ **Đã xử lý (2026-02-11)**:
- Đã thêm background reconcile job định kỳ (`CustomerBalanceReconcileHostedService`)
- Đã thêm reconcile service đối soát expected vs cached và hỗ trợ apply sửa lệch
- Đã thêm admin health summary + manual reconcile endpoint để vận hành chủ động

**Cần cải thiện** — Thiếu backup/restore strategy rõ ràng trong documentation:
- BackupService dùng pg_dump/pg_restore — tốt
- Có scheduled backup + manual backup — tốt
- Thiáº¿u: cross-region backup, backup verification, Point-in-time recovery
- `backup_daily.bat` script quá đơn giản (547 bytes)

**Cần cải thiện** — Không có database partitioning cho bảng lớn:
- `invoices`, `receipts`, `audit_logs` sẽ grow lớn theo thời gian
- Nên partition `audit_logs` theo tháng

**Cần cải thiện một phần** — Retention policy:
- ✅ Đã có retention automation cho `audit_logs`, `import_staging_rows`, `refresh_tokens` (scheduler + manual trigger)
- ⏸️ `reminder_logs` và `notifications` cleanup vẫn là backlog vận hành

### 5.4 Đánh giá

| Tiêu chí | Đánh giá |
|-----------|----------|
| DB khớp FE + BE | ✅ **Tốt** — Entity mapping chính xác, JSONB types đúng |
| Fields dư thừa | ✅ **Tốt** — Không phát hiện field dư, mỗi field có vai trò rõ ràng |
| Ràng buộc | ✅ **Tốt** — CHECK constraints đầy đủ, FK references chặt chẽ |

---

## 6️⃣ Review DevOps & Vận hành

### 6.1 Phân tích

| Tiêu chí | Đánh giá | Chi tiết |
|-----------|----------|----------|
| RBAC | ✅ **Tốt** | 4 roles rõ ràng, 17 authorization policies, role-based UI filtering |
| Environments | ⚠️ **Trung bình** | Đã có `appsettings.Production.json` + hướng dẫn env/secrets; còn thiếu staging profile rõ ràng |
| CI/CD | ✅ **Đã cải thiện** | Đã có pipeline `.github/workflows/ci.yml` cho build/test |
| Monitoring | 🟡 **Baseline** | Đã thêm OpenTelemetry metrics/tracing + `/health/ready` checks (kèm data retention worker); chưa có dashboard/alerting hoàn chỉnh |
| Centralized logging | ⚠️ **Trung bình** | Serilog ghi file local (`C:\apps\congno\api\logs\api.log`), không có centralized log aggregation |
| Alert hệ thống | ⚠️ **Trung bình** | Chỉ có notification khi backup fail. Không có uptime monitoring, error rate alerts |
| Backup tự động | ✅ **Tốt** | Scheduled backup via BackupSchedulerHostedService, retention policy, pg_dump |

### 6.2 Điểm cần cải thiện

~~**`CRITICAL`** — Không có CI/CD pipeline~~  
✅ **Đã xử lý (2026-02-11)**:
- Đã thêm GitHub Actions workflow CI cho build + test.

~~**`CRITICAL`** — Không tách environments~~  
✅ **Đã cải thiện (2026-02-11)**:
- Đã có `appsettings.Production.json` và tài liệu env/secrets.
- Vẫn nên bổ sung profile staging riêng khi mở rộng môi trường.

~~**Cần cải thiện** — Không có containerization~~  
✅ **Đã xử lý (2026-02-12)**:
- Đã có `Dockerfile` backend/frontend, `docker-compose.yml`, `.env.docker.example`, `DEPLOYMENT_GUIDE_DOCKER.md`.
- Hỗ trợ song song 2 runtime: Windows Service (legacy) và Docker (mặc định cho rollout mới).

**Cần cải thiện** — Health check endpoint cần mở rộng thêm runtime signals:
```csharp
// Đã có checks DB + worker toggles trong /health/ready.
// Có thể bổ sung thêm disk space, memory usage, external dependency probes (ví dụ Zalo API).
```

### 6.3 Đánh giá

| Tiêu chí | Đánh giá |
|-----------|----------|
| An toàn chạy production | ⚠️ **Trung bình** — Đã có CI + baseline observability, còn thiếu monitoring/alerting tập trung |
| Rủi ro vận hành | ⚠️ **Trung bình** — Giảm so với trước, nhưng vẫn cần hoàn thiện alerting + deployment automation sâu hơn |

---

## 7ï¸âƒ£ Review Báº£o máº­t

### 7.1 Phân tích

| Tiêu chí | Đánh giá | Chi tiết |
|-----------|----------|----------|
| JWT | ✅ **Tốt** | JWT Bearer auth, configurable issuer/audience, 60-min expiry, refresh token rotation |
| Mã hóa mật khẩu | ✅ **Tốt** | BCrypt.Net-Next (`BCrypt.Verify`), industry standard |
| SQL Injection | ✅ **Tốt** | EF Core parameterized queries + Dapper parameterized SQL |
| XSS | ⚠️ **Trung bình** | React mặc định escape output. Nhưng không kiểm tra user input đặc biệt (tên khách hàng, note) |
| CSRF | ⚠️ **Trung bình** | CORS policy chỉ cho localhost:5173. JWT Bearer tự immune CSRF. Nhưng refresh token dùng cookie — cần SameSite |
| Rate limiting | ✅ **Đã xử lý** | Đã thêm policy rate limiting cho `/auth/login` và `/auth/refresh` |
| Phân quyền API | ✅ **Tốt** | 17 named policies, mỗi endpoint group có authorization riêng |
| Bảo mật dữ liệu tài chính | ⚠️ **Trung bình** | Dữ liệu lưu plaintext trong DB, không encryption at rest |

### 7.2 Lỗ hổng và khuyến nghị

~~**`CRITICAL`** — Không có rate limiting~~  
✅ **Đã xử lý (2026-02-11)**:
- Đã thêm rate limiting policy cho login/refresh endpoints bằng .NET rate limiter.

~~**`CRITICAL`** — JWT Secret trong appsettings.json~~  
✅ **Đã xử lý (2026-02-11)**:
- Đã bỏ `Jwt:Secret` khỏi `appsettings.json` và `appsettings.Development.json` (tracked source).
- Cấu hình secret chuyển sang `Jwt__Secret` từ environment/secret manager.
- Startup fail-fast khi secret yếu hoặc dùng placeholder ở môi trường non-Development.

**Cần cải thiện** — Refresh token cookie settings:
```json
"RefreshCookieSecure": true
// Thiáº¿u: SameSite=Strict, HttpOnly, Path restriction
```

~~**Cần cải thiện** — Không có password complexity policy~~  
✅ **Đã xử lý (2026-02-11)**:
- Endpoint `/admin/users` đã validate mật khẩu theo policy tối thiểu (>=8 ký tự, có chữ hoa + chữ thường + số).

~~**Cần cải thiện** — Refresh token không có absolute expiry~~  
✅ **Đã xử lý (2026-02-11)**:
- Bổ sung `RefreshTokenAbsoluteDays` trong cấu hình.
- Refresh token chain hiện bị chặn bởi `absolute_expires_at` (không thể rotate vô hạn).

~~**Cần cải thiện** — Không có IP-based session binding~~  
✅ **Đã xử lý (2026-02-11)**:
- Refresh token được lưu kèm device fingerprint hash + IP prefix.
- Chính sách dual-signal: chỉ từ chối refresh khi **đồng thời** lệch cả device fingerprint và IP prefix.

### 7.3 Đánh giá

| Tiêu chí | Đánh giá |
|-----------|----------|
| Lỗ hổng nghiêm trọng | 🟡 **Giảm mạnh** — đã bổ sung IP/device binding dual-signal; còn backlog encryption-at-rest/centralized alerting |
| Tổng thể bảo mật | ✅ **Khá tốt** — Foundation tốt (BCrypt, JWT rotation, RBAC, rate limiting, absolute expiry, dual-signal binding) |

---

## 8️⃣ Tính đồng bộ Frontend – Backend – Database

### 8.1 Phân tích

| Tiêu chí | Đánh giá | Chi tiết |
|-----------|----------|----------|
| API contract khớp DB schema | ✅ **Tốt** | OpenAPI types file 67KB (`openapi.d.ts`) đồng bộ với backend DTOs |
| Mapping kiểu dữ liệu | ✅ **Tốt** | numeric(18,2) → number, UUID → string, timestamptz → string ISO format |
| Logic tính toán duplicate | ⚠️ **Trung bình** | Frontend tự tính formatMoney, formatRatio. Backend cũng có FormatMoney trong ReminderService |
| Business logic duplicate | ⚠️ **Trung bình** | Risk level labels định nghĩa ở cả FE (RiskAlertsPage) và BE (ReminderService). Nếu thêm level mới phải sửa 2 nơi |
| FE phụ thuộc quá nhiều BE | 🟡 **Đã cải thiện một phần** | Đã giảm round-trips bằng endpoint tổng hợp (`/reports/overview`, `/risk/bootstrap`) và tối ưu dashboard; vẫn còn dư địa tinh gọn thêm theo từng luồng |

### 8.2 Điểm cần cải thiện

**Đã cải thiện** — Giảm số API calls ở các luồng chính:
```typescript
// DashboardPage: giảm từ 3 call còn 2 (reuse fetchDashboardOverview cho KPI + cashflow)
// ReportsPage: dùng GET /reports/overview thay cho 3 call rời (kpis/charts/insights)
// RiskAlertsPage: dùng GET /risk/bootstrap để gom dữ liệu khởi tạo
```
**Ghi chú**: Tiếp tục theo dõi để tránh payload quá lớn ở endpoint composite và chỉ gom những phần truy cập cùng lifecycle.

**Cần cải thiện** — Risk/Status labels duplicate:
```typescript
// Frontend - RiskAlertsPage.tsx
const riskLabels = { VERY_HIGH: 'Rất cao', HIGH: 'Cao', MEDIUM: 'Trung bình', LOW: 'Thấp' }
```
```csharp
// Backend - ReminderService.cs
private static string ResolveRiskLabel(RiskLevel level) => level switch { ... }
```
**Giải pháp**: API trả về label cùng data, hoặc có 1 shared enum definition

---

## 9️⃣ Code thừa & Technical Debt

### 9.1 Code thừa

| # | File/Pattern | Vấn đề | Ưu tiên |
|---|-------------|---------|---------|
| 1 | `EnsureUser()` | Đã extract helper dùng chung và áp dụng cho Risk/Reminder/Advance/Receipt/Dashboard/PeriodLock; rà soát service còn lại theo nhu cầu | **High** |
| 2 | `ResolveOwnerFilter()` | Đã extract helper dùng chung và áp dụng cho Risk + Dashboard + Receipt (owner access); tiếp tục mở rộng khi có refactor mới | **High** |
| 3 | `NormalizeLevel()`, `NormalizeChannel()`, `NormalizeStatus()` | String normalize pattern láº·p láº¡i | **Medium** |
| 4 | Frontend fetch pattern | useState + useEffect + try/catch lặp lại ở >15 pages | **High** |
| 5 | Page size storage | `getStoredPageSize`/`storePageSize` pattern lặp ở 3+ pages | **Medium** |
| 6 | `pageLoaders.ts` (8976 bytes) | File utility cho page loading, nhưng vẫn để inline logic ở pages | **Low** |

### 9.2 Code không sử dụng

| # | Item | Chi tiáº¿t |
|---|------|----------|
| 1 | `CustomersPage.tsx` (52 bytes) | Chỉ re-export, page thực tế ở `customers/` subfolder |
| 2 | `ImportsPage.tsx` (48 bytes) | Chỉ re-export |
| 3 | `ReceiptsPage.tsx` (50 bytes) | Chỉ re-export |
| 4 | `AdvancesPage.tsx` (143 bytes) | Chỉ re-export |
| 5 | `DashboardPreviewPage.tsx` (12718 bytes) | Preview version — xác nhận có được sử dụng không? |

### 9.3 Cấu trúc khó bảo trì

| # | Vấn đề | Ảnh hưởng | Giải pháp |
|---|--------|-----------|-----------|
| 1 | `index.css` 2732 dòng | Khó tìm, khó debug styling | Tách theo feature/component (dashboard.css, reports.css...) hoặc dùng CSS Modules |
| 2 | `ReportsPage.tsx` 1018 dòng | Quá nhiều responsibilities | Tách thành ReportsSummaryTab, ReportsStatementTab, ReportsAgingTab |
| 3 | `DashboardPage.tsx` 774 dòng | Mix rendering + data fetching + formatting | Tách hooks, formatters, sub-components |
| 4 | `BackupService.cs` 838 dòng | Monolithic service | Tách BackupExecutor, BackupStorage, RestoreService |
| 5 | `ReminderService.cs` 688 dòng | Mix scheduling + sending + logging | Tách ReminderScheduler, ReminderSender, ReminderLogger |

### 9.4 Pháº§n cáº§n refactor

| # | Component | Mức độ ưu tiên | Lý do |
|---|-----------|----------------|-------|
| 1 | Frontend state management | **High** | Quá nhiều useState, nên dùng useReducer hoặc Zustand |
| 2 | Shared backend helpers | ✅ **Done (xác nhận 2026-02-11)** | Đã dùng extension chung `CurrentUserAccessExtensions`, không còn helper cục bộ trùng lặp |
| 3 | Frontend code-splitting | ✅ **Done (xác nhận 2026-02-11)** | Đã triển khai lazy routes + suspense/prefetch |
| 4 | CSS architecture | ✅ **Done (2026-02-13)** | Đã tách CSS theo feature cho AppShell/Reports/Dashboard (`layout-shell.css`, `reports.css`, `dashboard.css`), giảm `index.css` từ 2853 còn 2203 dòng và bật route-scoped CSS chunks |
| 5 | Backend service files | ✅ **Done (2026-02-13)** | Đã tách `BackupService` và `ReminderService` thành partial theo trách nhiệm (`BackupService.InternalOps.cs`, `ReminderService.Execution.cs`); các service chính còn lại đều <= 800 dòng |
| 6 | Frontend custom hooks | ✅ **Done (2026-02-13)** | Đã extract `useQuery`, `usePagination`, `usePersistedState` và áp dụng cho các page chính |

---

## 🔟 Đề xuất cải tiến

### Tóm tắt đánh giá tổng quan

| Hạng mục | Điểm (1-10) | Đánh giá |
|----------|-------------|----------|
| Kiến trúc | 8/10 | ✅ Tốt |
| UI/UX | 6/10 | ⚠️ Trung bình |
| Frontend | 6/10 | ⚠️ Trung bình |
| Backend | 8/10 | ✅ Tốt |
| Database | 8.5/10 | ✅ Tốt |
| DevOps | 4/10 | ❌ Cần cải thiện |
| Bảo mật | 6/10 | ⚠️ Trung bình |
| Synchronization | 7/10 | ✅ Tốt |
| Code quality | 6.5/10 | ⚠️ Trung bình |
| **Tổng** | **6.8/10** | **Trung bình khá** |

### Đề xuất cải tiến chi tiết

| # | Đề xuất | Loại | Ưu tiên | Mô tả |
|---|---------|------|---------|-------|
| 1 | **Rate Limiting** | Bảo mật | ✅ **Done (2026-02-11)** | Đã thêm rate limiter cho `/auth/login` và `/auth/refresh` |
| 2 | **Explicit transactions** | Backend | ✅ **Done (xác nhận 2026-02-11)** | `ReceiptService.ApproveAsync` đã dùng transaction cho luồng approve |
| 3 | **CI/CD Pipeline** | DevOps | ✅ **Done (2026-02-11)** | Đã có GitHub Actions workflow CI |
| 4 | **Environment separation** | DevOps | ✅ **Done (2026-02-11)** | Đã bổ sung production config + hướng dẫn env/secrets |
| 5 | **Frontend code-splitting** | Performance | ✅ **Done (xác nhận 2026-02-11)** | App routing đã dùng `React.lazy` + `Suspense` + prefetch |
| 6 | **Responsive layout** | UI/UX | ✅ **Done (2026-02-11)** | Đã có mobile nav toggle + collapsible sidebar cho AppShell |
| 7 | **Custom hooks** | Frontend | ✅ **Done (2026-02-13)** | Implemented shared hooks (`useQuery`, `usePagination`, `usePersistedState`) and applied in key pages |
| 8 | **Shared backend utilities** | Backend | ✅ **Done (xác nhận 2026-02-11)** | Đã audit service phụ trợ, không còn helper trùng lặp `EnsureUser/ResolveOwnerFilter` |
| 9 | **Cảnh báo qua Email** | Tính năng mới | 🟡 **Medium** | Thêm channel Email cho reminder service (SMTP integration) |
| 10 | **current_balance reconciliation** | Database | ✅ **Done (2026-02-11)** | Đã thêm reconcile job + API trigger + health drift summary |
| 11 | **Monitoring & APM** | DevOps | ✅ **Done (2026-02-13)** | Added OpenTelemetry + Prometheus metrics endpoint and runtime toggles |
| 12 | **Phân loại rủi ro khách hàng nâng cao** | Tính năng mới | ✅ **Done (2026-02-12)** | Đã có baseline explainable (`RiskAiScorer`) + tín hiệu AI hiển thị trên Risk Alerts |
| 13 | **AI dự đoán khả năng trễ hạn** | Tính năng mới | ✅ **Done (2026-02-12)** | Đã có pipeline huấn luyện thực tế theo snapshot lịch sử + nhãn horizon + seasonality, lưu model versioned, retrain scheduler và API quản trị model |
| 14 | **Dashboard phân tích dòng tiền** | UI/UX | ✅ **Done (2026-02-13)** | Added expected vs actual cashflow, variance indicators, and forecast trend |
| 15 | **Xuất báo cáo PDF** | Tính năng mới | ✅ **Done (2026-02-13)** | Added PDF export path for summary report (`format=pdf`) |
| 16 | **Dark mode** | UI/UX | ✅ **Done (2026-02-13)** | Implemented light/dark/system themes with persisted user preference |
| 17 | **Containerization** | DevOps | ✅ **Done (2026-02-12)** | Đã bổ sung Dockerfile backend/frontend + docker-compose + env mẫu + guide triển khai Docker |
| 18 | **Tích hợp MISA/ERP** | Tính năng mới | ✅ **Done (2026-02-13)** | Added ERP baseline integration (status + manual sync summary) |
| 19 | **Audit log retention** | Database | ✅ **Done (2026-02-12)** | Đã có retention scheduler/manual trigger và hoàn tất partition `audit_logs` theo tháng (migration `022` + auto-ensure partition hiện tại/kế tiếp) |
| 20 | **PWA support** | Frontend | ✅ **Done (2026-02-13)** | Upgraded SW caching, offline fallback page, and manifest metadata/shortcuts |

### Kiến trúc tối ưu đề xuất

Nếu scale lên, nên chuyển sang:

```
                    ┌─────────────┐
                    │  Nginx / LB │
                    └──────┬──────┘
              ┌────────────┼────────────┐
              ▼            ▼            ▼
        ┌──────────┐ ┌──────────┐ ┌──────────┐
        │ API Pod  │ │ API Pod  │ │ API Pod  │  ← Horizontal scale
        └────┬─────┘ └────┬─────┘ └────┬─────┘
             │             │             │
        ┌────▼─────────────▼─────────────▼────┐
        │              Redis Cache             │  ← Session + cache layer
        └─────────────────┬───────────────────┘
                          │
        ┌─────────────────▼───────────────────┐
        │     PostgreSQL (Primary + Replica)   │  ← Read replica cho reports
        └─────────────────────────────────────┘
        
        Background Workers (tách riêng):
        ├── ReminderWorker
        ├── BackupWorker  
        ├── ReconciliationWorker
        └── NotificationWorker
```

---

## Phụ lục: Tóm tắt Critical Issues

| # | Issue | File/Component | Mức độ | Hành động |
|---|-------|---------------|--------|-----------|
| 1 | Thiếu rate limiting | Program.cs | ✅ RESOLVED | Đã thêm rate limiter cho auth endpoints (2026-02-11) |
| 2 | JWT secret trong config | appsettings*.json | ✅ RESOLVED | Đã bỏ secret khỏi config tracked + fail-fast non-Development (2026-02-11) |
| 3 | Thiếu explicit transaction | ReceiptService.ApproveAsync | ✅ RESOLVED | Đã có transaction trong approve flow (xác nhận 2026-02-11) |
| 4 | Không responsive | index.css | ✅ RESOLVED | Đã thêm responsive breakpoints + mobile nav collapse (2026-02-11) |
| 5 | Không CI/CD | Project root | ✅ RESOLVED | Đã thêm GitHub Actions workflow CI (2026-02-11) |
| 6 | Không tách environments | appsettings | ✅ RESOLVED | Đã bổ sung production config + cập nhật docs env (2026-02-11) |
| 7 | `current_balance` inconsistency risk | customers table | ✅ RESOLVED | Đã thêm reconcile job + admin reconcile endpoint (2026-02-11) |
| 8 | Frontend bundle size | App.tsx | ✅ DONE | Đã hoàn tất code-splitting + font subset + budget gate (`build:budget`) + route-scoped CSS chunks; ngân sách bundle đang pass ổn định |
| 9 | Retry/circuit breaker Zalo | ZaloClient.cs | ✅ RESOLVED | Đã triển khai retry/backoff + circuit breaker configurable (2026-02-11) |
| 10 | Password complexity + refresh absolute expiry | AdminEndpoints/AuthService | ✅ RESOLVED | Đã thêm password policy và `absolute_expires_at` cho refresh token chain (2026-02-11) |
| 11 | IP/device binding cho refresh token | AuthService | ✅ RESOLVED | Đã có dual-signal binding (device fingerprint + IP prefix) với rule giảm false-positive (2026-02-11) |
| 12 | DB retention automation | DataRetentionService | ✅ RESOLVED | Đã có retention scheduler/manual trigger + partition `audit_logs` theo tháng qua migration `022_audit_logs_partition.sql` (2026-02-12) |
| 13 | AI risk scoring / overdue forecast | RiskService/Risk Alerts | ✅ RESOLVED | Đã bổ sung full pipeline: training + seasonality + model registry/runs + scheduler + admin control; runtime vẫn fallback heuristic khi chưa có active model |

---

> **Ghi chú cho Codex Agent**: Các items đánh dấu `CRITICAL` nên được ưu tiên xử lý trước. Các `HIGH` items nên được lên kế hoạch trong sprint tiếp theo. Mỗi item có giải pháp cụ thể — hãy review và triển khai theo thứ tự ưu tiên.

## Docker compatibility re-audit (Codex, 2026-02-12 14:25)

- Scope: full stack Docker runtime (db, api, web) after backup restore.
- Confirmed running services via docker compose ps.
- Verified smoke tests PASS on both paths:
  - Direct API: http://127.0.0.1:18080
  - Frontend proxy API: http://127.0.0.1:18081/api
  - Flow checked: /health, /health/ready, /auth/login, /auth/refresh, /customers, /auth/logout.
- Verified web routes render through Nginx fallback: / and /reports return HTTP 200.
- Verified Docker paths:
  - API logs written to /var/lib/congno/logs/api*.log
  - Backup dumps mounted at /var/lib/congno/backups/dumps
- Compatibility fixes already in place and validated:
  - JWT_REFRESH_COOKIE_PATH=/ to support both direct and /api proxy auth-refresh flow.
  - SERILOG_FILE_PATH=/var/lib/congno/logs/api.log + writable log directory in backend image.

### Out-of-scope (intentional)
- Legacy Windows Service control modules in src/ops/* are kept for dual-runtime support.
- These modules are not blockers for Docker deployment and remain for fallback/ops compatibility.

## Medium/Low execution update (Codex, 2026-02-13)

- Scope: execute Medium/Low backlog except Email channel, based on `docs/plans/2026-02-13-opus-medium-low-execution-excluding-email.md`.
- Completed:
  - Custom hooks (`usePersistedState`, `usePagination`, `useQuery`) and page refactors.
  - Monitoring/APM uplift with OpenTelemetry Prometheus exporter and configurable `/metrics`.
  - Dashboard cashflow expected vs actual + variance + forecast.
  - PDF export for Summary report (`/reports/export?...&format=pdf`).
  - Dark mode (light/dark/system) with persisted preference.
  - ERP baseline integration (admin status + manual sync summary endpoint/UI).
  - PWA upgrade (cache strategy SW, offline fallback page, richer manifest, SW lifecycle checks).
- Deferred by scope: Email channel reminder integration.

### Verification snapshot (2026-02-13)

- `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj` => pass (`94/94`)
- `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj` => pass (`36/36`)
- `npm run --prefix src/frontend test -- --run` => pass (`83/83`)
- `npm run --prefix src/frontend build` => pass
- `dotnet build src/backend/Api/CongNoGolden.Api.csproj` => pass




