# Opus Medium/Low Backlog (No Email Channel) Implementation Plan

> [!IMPORTANT]
> **HISTORICAL EXECUTION PLAN**
> Tài liệu này là kế hoạch/thực thi theo thời điểm viết, có thể chứa giả định cũ.
> Nguồn vận hành hiện hành: `DEPLOYMENT_GUIDE_DOCKER.md`, `RUNBOOK.md`, `task.md`.


> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Hoàn thành các hạng mục Medium/Low còn lại trong `Opus_4.6_review.md` (trừ Email channel), có bằng chứng kỹ thuật và test tối thiểu.

**Architecture:** Triển khai theo 3 lớp: (1) frontend foundations (custom hooks + dark mode + PWA), (2) backend/platform capabilities (APM/monitoring + PDF export + ERP integration), (3) business UI (dashboard dòng tiền expected/actual + forecast và màn hình ERP admin). Giữ backward compatibility cho API hiện hữu, chỉ mở rộng payload/endpoint theo kiểu additive.

**Tech Stack:** .NET 8 Minimal API, React 19 + Vite + TypeScript, OpenTelemetry/Prometheus, QuestPDF, Docker runtime.

---

### Task 1: Custom hooks foundation

**Files:**
- Create: `src/frontend/src/hooks/usePersistedState.ts`
- Create: `src/frontend/src/hooks/usePagination.ts`
- Create: `src/frontend/src/hooks/useQuery.ts`
- Modify: `src/frontend/src/pages/ReportsPage.tsx`
- Modify: `src/frontend/src/pages/DashboardPage.tsx`
- Test: `src/frontend/src/hooks/__tests__/usePersistedState.test.tsx`
- Test: `src/frontend/src/hooks/__tests__/usePagination.test.tsx`

**Step 1:** Tạo 3 custom hooks dùng chung (`usePersistedState`, `usePagination`, `useQuery`) với API đơn giản, typed.

**Step 2:** Refactor `ReportsPage` thay các pattern lưu localStorage + paging state lặp lại bằng hooks mới.

**Step 3:** Refactor `DashboardPage` dùng `usePersistedState` cho lựa chọn granularity/option cần lưu.

**Step 4:** Thêm unit tests cho hooks mới.

**Step 5:** Chạy test mục tiêu frontend.

---

### Task 2: Monitoring/APM uplift (Prometheus + OpenTelemetry config)

**Files:**
- Modify: `src/backend/Api/CongNoGolden.Api.csproj`
- Modify: `src/backend/Api/Program.cs`
- Modify: `src/backend/Api/appsettings.json`
- Modify: `src/backend/Api/appsettings.Development.json`
- Modify: `src/backend/Api/appsettings.Production.json`
- Modify: `ENV_SAMPLE.md`

**Step 1:** Bổ sung Prometheus exporter package cho OpenTelemetry.

**Step 2:** Mở rộng cấu hình `Observability` để bật/tắt Prometheus endpoint (`/metrics`) qua config.

**Step 3:** Map scraping endpoint khi bật Prometheus exporter.

**Step 4:** Cập nhật env/docs cho biến observability mới.

**Step 5:** Build backend và chạy test unit/integration mục tiêu.

---

### Task 3: Dashboard dòng tiền expected vs actual + forecast

**Files:**
- Modify: `src/backend/Application/Dashboard/DashboardTrendPoint.cs`
- Create: `src/backend/Application/Dashboard/DashboardCashflowForecastPoint.cs`
- Modify: `src/backend/Application/Dashboard/DashboardOverviewDto.cs`
- Modify: `src/backend/Infrastructure/Services/DashboardService.cs`
- Modify: `src/backend/Tests.Integration/DashboardOverviewTests.cs`
- Modify: `src/frontend/src/api/dashboard.ts`
- Modify: `src/frontend/src/pages/DashboardPage.tsx`
- Modify: `src/frontend/src/index.css`

**Step 1:** Mở rộng trend DTO để có expected/actual/variance (additive fields) và danh sách forecast.

**Step 2:** Tính forecast ngắn hạn bằng moving average từ chuỗi actual/expected gần nhất.

**Step 3:** Cập nhật frontend dashboard để hiển thị expected vs actual rõ ràng và panel forecast.

**Step 4:** Bổ sung integration test backend cho payload trend/forecast.

**Step 5:** Chạy test backend + frontend liên quan dashboard.

---

### Task 4: PDF export cho báo cáo tổng hợp

**Files:**
- Modify: `src/backend/Application/Reports/ReportExportRequest.cs`
- Modify: `src/backend/Application/Reports/ReportExportResult.cs`
- Create: `src/backend/Application/Reports/ReportExportFormat.cs`
- Modify: `src/backend/Infrastructure/CongNoGolden.Infrastructure.csproj`
- Modify: `src/backend/Infrastructure/Services/ReportExportService.cs`
- Create: `src/backend/Infrastructure/Services/ReportExportService.Pdf.cs`
- Modify: `src/backend/Api/Endpoints/ReportEndpoints.cs`
- Create: `src/backend/Tests.Unit/ReportPdfExportTests.cs`
- Modify: `src/frontend/src/api/reports.ts`
- Modify: `src/frontend/src/pages/reports/ReportsTablesSection.tsx`
- Modify: `src/frontend/src/pages/ReportsPage.tsx`

**Step 1:** Mở rộng contract export hỗ trợ `format` (xlsx mặc định, pdf tùy chọn).

**Step 2:** Triển khai render PDF summary bằng QuestPDF, trả đúng `Content-Type`.

**Step 3:** Bổ sung endpoint validation cho format/kind hỗ trợ.

**Step 4:** Bổ sung button “Tải PDF tổng hợp” trên Reports UI.

**Step 5:** Thêm unit test backend cho PDF export và chạy test liên quan.

---

### Task 5: Dark mode

**Files:**
- Create: `src/frontend/src/hooks/useTheme.ts`
- Modify: `src/frontend/src/layouts/AppShell.tsx`
- Modify: `src/frontend/src/index.css`
- Modify: `src/frontend/src/layouts/__tests__/app-shell.test.tsx`
- Modify: `src/frontend/src/main.tsx` (nếu cần bootstrap theme sớm)

**Step 1:** Tạo `useTheme` (light/dark/system) với persisted state.

**Step 2:** Thêm toggle dark mode ở AppShell, không phá responsive/mobile nav.

**Step 3:** Khai báo biến CSS cho theme dark và apply qua `data-theme`/root.

**Step 4:** Cập nhật test AppShell cho toggle.

**Step 5:** Chạy test frontend mục tiêu.

---

### Task 6: MISA/ERP integration baseline

**Files:**
- Create: `src/backend/Application/Integrations/IErpIntegrationService.cs`
- Create: `src/backend/Application/Integrations/ErpIntegrationModels.cs`
- Create: `src/backend/Infrastructure/Services/ErpIntegrationOptions.cs`
- Create: `src/backend/Infrastructure/Services/ErpIntegrationService.cs`
- Create: `src/backend/Api/Endpoints/ErpIntegrationEndpoints.cs`
- Modify: `src/backend/Infrastructure/DependencyInjection.cs`
- Modify: `src/backend/Api/Program.cs`
- Modify: `src/backend/Api/appsettings.json`
- Modify: `src/backend/Api/appsettings.Development.json`
- Modify: `src/backend/Api/appsettings.Production.json`
- Create: `src/frontend/src/api/erpIntegration.ts`
- Create: `src/frontend/src/pages/AdminErpIntegrationPage.tsx`
- Modify: `src/frontend/src/App.tsx`
- Modify: `src/frontend/src/pages/pageLoaders.ts`
- Modify: `src/frontend/src/layouts/AppShell.tsx`

**Step 1:** Tạo integration service với 2 chức năng: `status` và `sync summary` (manual).

**Step 2:** Thêm endpoint admin để xem trạng thái + trigger sync.

**Step 3:** Bổ sung cấu hình ERP (provider/baseUrl/apiKey/companyCode/timeout).

**Step 4:** Thêm trang Admin ERP trong frontend và route/nav tương ứng.

**Step 5:** Build FE/BE và chạy test smoke cho endpoint + UI render.

---

### Task 7: PWA upgrade

**Files:**
- Modify: `src/frontend/public/sw.js`
- Create: `src/frontend/public/offline.html`
- Modify: `src/frontend/public/manifest.json`
- Modify: `src/frontend/src/main.tsx`

**Step 1:** Nâng service worker sang cache strategy (app shell cache + network-first navigation + API GET fallback cơ bản).

**Step 2:** Bổ sung offline fallback page.

**Step 3:** Cập nhật manifest cho trải nghiệm install tốt hơn (metadata + shortcuts cơ bản).

**Step 4:** Cập nhật đăng ký SW ở frontend để xử lý lifecycle ổn định hơn.

**Step 5:** Build frontend và kiểm tra smoke PWA (installability + offline fallback).

---

### Task 8: Tracking + verification-before-completion

**Files:**
- Modify: `task.md`
- Modify: `Opus_4.6_review.md`
- Modify: `QA_REPORT.md` (nếu có command/test evidence mới)

**Step 1:** Chạy bộ verify:

```bash
dotnet test src/backend/Tests.Unit/Tests.Unit.csproj
dotnet test src/backend/Tests.Integration/Tests.Integration.csproj
npm run -C src/frontend test -- --run
npm run -C src/frontend build
dotnet build src/backend/Api/CongNoGolden.Api.csproj
```

**Step 2:** Cập nhật trạng thái hạng mục Medium/Low đã hoàn thành (trừ Email) trong tracker.

**Step 3:** Đồng bộ evidence command pass/fail và phần còn lại (nếu có).

