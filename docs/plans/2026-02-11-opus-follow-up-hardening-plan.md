# Opus Follow-up Hardening Implementation Plan

> [!IMPORTANT]
> **HISTORICAL EXECUTION PLAN**
> Tài liệu này là kế hoạch/thực thi theo thời điểm viết, có thể chứa giả định cũ.
> Nguồn vận hành hiện hành: `DEPLOYMENT_GUIDE_DOCKER.md`, `RUNBOOK.md`, `task.md`.


> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Hoàn tất các hạng mục còn lại có tác động cao nhất về bảo mật, toàn vẹn dữ liệu và hiệu năng frontend sau đợt remediation Opus 4.6.

**Architecture:** Ưu tiên hardening theo chiều dọc từng luồng quan trọng (Auth, Receipt approve, frontend route loading), giữ backward compatibility cho API hiện có, triển khai theo batch nhỏ với tiêu chí rollback rõ ràng.

**Tech Stack:** .NET 8 Minimal API, EF Core + Npgsql/PostgreSQL 16, React 18 + TypeScript + Vite, Vitest, xUnit.

---

### Task 1: Auth secret externalization + startup fail-fast

**Files:**
- Modify: `src/backend/Application/Auth/JwtOptions.cs`
- Modify: `src/backend/Api/Program.cs`
- Modify: `ENV_SAMPLE.md`
- Test: `src/backend/Tests.Unit/AuthSecurityPolicyTests.cs`

**Step 1: Write/extend failing tests**
- Add tests cho trường hợp production dùng secret placeholder hoặc secret quá ngắn phải fail startup config validation.

**Step 2: Implement validation rules**
- Bắt buộc secret từ environment/secret store trong production profile, cấm fallback unsafe.

**Step 3: Update docs**
- Cập nhật hướng dẫn cấu hình secrets theo môi trường.

**Step 4: Verify**
- Run: `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj`
- Expected: pass, có test guard cho production secret.

---

### Task 2: Password policy + refresh-token hardening (absolute expiry)

**Files:**
- Modify: `src/backend/Infrastructure/Services/AuthService.cs`
- Modify: `src/backend/Application/Auth/JwtOptions.cs`
- Modify: `src/backend/Api/Endpoints/AuthEndpoints.cs`
- Test: `src/backend/Tests.Unit/AuthSecurityPolicyTests.cs`

**Step 1: Add failing tests**
- Password yếu bị từ chối.
- Refresh token quá absolute TTL bị từ chối rotate.

**Step 2: Implement minimal secure policy**
- Password: min length + uppercase/lowercase/number.
- Refresh token: thêm absolute expiry cap.

**Step 3: Verify compatibility**
- Giữ nguyên login/refresh contract response.

**Step 4: Verify**
- Run: `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj`

---

### Task 3: Explicit transaction cho Receipt approve flow

**Files:**
- Modify: `src/backend/Infrastructure/Services/ReceiptService.cs`
- Test: `src/backend/Tests.Integration/*Receipt*.cs` (tạo test rollback nếu lỗi giữa chừng)

**Step 1: Add failing integration test**
- Mô phỏng lỗi trong approve pipeline, assert state rollback.

**Step 2: Wrap critical flow**
- Dùng `BeginTransactionAsync` + `CommitAsync` + `RollbackAsync`.

**Step 3: Verify**
- Run: `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj`

---

### Task 4: Frontend route-level code splitting

**Files:**
- Modify: `src/frontend/src/App.tsx`
- Modify: route/page loaders nếu cần
- Test: `src/frontend/src/pages/__tests__/page-loaders.test.ts`

**Step 1: Add failing/adjusted tests**
- Route loader behavior không đổi sau lazy load.

**Step 2: Implement**
- Chuyển import tĩnh page nặng sang `React.lazy` + `Suspense`.

**Step 3: Verify**
- Run: `npm --prefix src/frontend run lint`
- Run: `npm --prefix src/frontend test -- --run`
- Run: `npm --prefix src/frontend run build`

---

### Task 5: Shared helper completion audit (service phụ trợ)

**Files:**
- Modify: `src/backend/Infrastructure/Services/*` (service có `EnsureUser`/`ResolveOwnerFilter` cục bộ còn lại)
- Test: `src/backend/Tests.Unit/CurrentUserAccessTests.cs`

**Step 1: Audit**
- Liệt kê toàn bộ helper trùng lặp còn tồn tại.

**Step 2: Migrate**
- Chuyển sang `CurrentUserAccessExtensions`.

**Step 3: Verify**
- Run: `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj`

---

### Task 6: Baseline observability (metrics/tracing tối thiểu)

**Files:**
- Modify: `src/backend/Api/Program.cs`
- Add: config observability trong `appsettings.*`
- Docs: `RUN_BACKEND.md`

**Step 1: Add baseline**
- Thêm OpenTelemetry metrics/tracing exporters phù hợp môi trường local/self-host.

**Step 2: Add health detail**
- Bổ sung signal chính (DB, background workers) vào readiness/diagnostic output.

**Step 3: Verify**
- Smoke test API chạy bình thường, không regress endpoint cũ.

---

### Task 7: Verification gate

**Files:**
- N/A (verification + docs update)

**Step 1: Run full validation**
- `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj`
- `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj`
- `npm --prefix src/frontend run lint`
- `npm --prefix src/frontend test -- --run`
- `npm --prefix src/frontend run build`

**Step 2: Sync tracking**
- Cập nhật `task.md`, `Opus_4.6_review.md`, beads status tương ứng.

---

## Deferred / Not in current cycle (with reasons)

- Zalo circuit breaker nâng cao: **defer** vì cần số liệu thực tế về failure-rate/traffic để tránh cấu hình quá tay gây false-open.
- IP/device binding cho refresh token: **defer** vì rủi ro false-positive cao trong môi trường NAT/proxy; cần thiết kế session fingerprint trước.
- DB partition + retention automation: **defer** cho đến khi có ngưỡng dữ liệu/vận hành rõ ràng (kèm metrics quan sát).
- Containerization: **defer** vì hệ thống đang vận hành theo Windows Service ổn định; ưu tiên hardening bảo mật và transaction trước.
- AI risk scoring / dự báo trễ hạn: **out of scope** cho đợt hardening kỹ thuật hiện tại.

## Done When

- [ ] Các mục High trong phụ lục Opus (secret, transaction, password, code-splitting) được xử lý và có test.
- [ ] `Opus_4.6_review.md` không còn mục trạng thái mâu thuẫn với code hiện tại.
- [ ] Beads + `task.md` đồng bộ trạng thái cho toàn bộ phase follow-up.

