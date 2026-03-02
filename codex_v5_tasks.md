# Opus Review V5 — Codex Task Log

## Re-evaluation Log

### VĐ-D1: Base font quá lớn — 16px vs benchmark 14-15px
- **Review đúng hay sai:** ✅ Đúng
- **Giá trị thực tế trong code:** `--font-size-body: 1rem`, `--font-size-h3: 1.2rem`, `--font-size-caption: 0.8rem`, `--font-size-h1: clamp(1.8rem, 1.4rem + 2vw, 2.6rem)`, `--font-size-h2: clamp(1.4rem, 1.2rem + 1.2vw, 2rem)` trong `src/frontend/src/index.css`.
- **Reviewer nói:** Body 16px và scale heading/caption đang lớn với data-dense UI.
- **Đánh giá giải pháp:** Hợp lý
- **Ghi chú:** Có thể giảm về 15px mà vẫn đảm bảo readability trên desktop.

### VĐ-D2: Spacing tokens quá rộng cho data-heavy UI
- **Review đúng hay sai:** ✅ Đúng
- **Giá trị thực tế trong code:** `--space-1..7 = 0.5, 0.75, 1, 1.5, 2, 2.5, 3rem` trong `src/frontend/src/index.css`.
- **Reviewer nói:** Spacing token đang rộng theo kiểu consumer UI.
- **Đánh giá giải pháp:** Hợp lý
- **Ghi chú:** Ảnh hưởng toàn cục, cần verify responsive sau khi chỉnh.

### VĐ-D3: Stat card values quá to, card border-radius quá tròn
- **Review đúng hay sai:** ✅ Đúng
- **Giá trị thực tế trong code:** `.stat-card { border-radius: 18px; }`, `.stat-card__value { font-size: 1.6rem; }`, `.stat-grid--primary .stat-card__value { font-size: 1.85rem; }`, `.stat-grid { minmax(220px, 1fr) }`.
- **Reviewer nói:** Values/radius quá lớn, min width card quá rộng.
- **Đánh giá giải pháp:** Cần điều chỉnh
- **Ghi chú:** Giảm min width về `180px` có thể chật ở một số viewport trung gian; ưu tiên `190px` để cân bằng density và readability.

### VĐ-D4: Content padding quá rộng, sidebar quá to
- **Review đúng hay sai:** ✅ Đúng
- **Giá trị thực tế trong code:** `.app-shell { grid-template-columns: 260px 1fr; }`, `.app-main { padding: var(--space-5) var(--space-6); }`.
- **Reviewer nói:** Sidebar 260px + padding ngang lớn làm hao hụt content width.
- **Đánh giá giải pháp:** Hợp lý
- **Ghi chú:** Chỉnh sidebar 240px là an toàn, phần padding sẽ giảm thêm khi áp dụng D2.

### VĐ-D5: Card container border-radius và padding quá generous
- **Review đúng hay sai:** ✅ Đúng
- **Giá trị thực tế trong code:** `.card { border-radius: 20px; padding: var(--space-4); }`, `.card-hero` cùng hệ card lớn dùng radius cao.
- **Reviewer nói:** Radius card lớn gây cảm giác "bubbly".
- **Đánh giá giải pháp:** Hợp lý
- **Ghi chú:** Chỉ giảm cho container/card lớn, giữ nguyên pill/button tròn.

### VĐ-Q1: DashboardPage.tsx quá lớn
- **Review đúng hay sai:** ⚠️ Đúng một phần
- **Giá trị thực tế trong code:** `DashboardPage.tsx` hiện `993` dòng (không phải 994), chứa nhiều UI block và helper render trong một file.
- **Reviewer nói:** File quá lớn, cần tách sub-components.
- **Đánh giá giải pháp:** Hợp lý
- **Ghi chú:** Hướng tách đúng, nhưng cần refactor theo từng block để tránh break logic hiện có.

### VĐ-Q2: index.css monolith quá lớn
- **Review đúng hay sai:** ⚠️ Đúng một phần
- **Giá trị thực tế trong code:** `index.css` dài `2438` dòng; đã có một phần tách riêng (`layout-shell.css`, `dashboard.css`).
- **Reviewer nói:** Cần tách triệt để thành nhiều file theo concern.
- **Đánh giá giải pháp:** Cần điều chỉnh
- **Ghi chú:** Tách toàn phần một lượt rủi ro regression cao; nên làm incremental theo nhóm style lớn ở đợt riêng.

### VĐ-Q3: Skeleton loaders thay "Đang tải..."
- **Review đúng hay sai:** ✅ Đúng
- **Giá trị thực tế trong code:** Nhiều loading state đang là text thuần (`Đang tải...`) ở Dashboard, Customers, Reports, Admin...; Dashboard chart/overdue vẫn dùng empty-state text.
- **Reviewer nói:** Thiếu skeleton nên gây flash layout.
- **Đánh giá giải pháp:** Hợp lý
- **Ghi chú:** Triển khai trước cho khu vực dashboard + các block dữ liệu chính để giảm rủi ro.

### VĐ-Q4: Serilog log rotation thiếu cấu hình
- **Review đúng hay sai:** ❌ Sai
- **Giá trị thực tế trong code:** `appsettings.json` đã có `rollingInterval`, `retainedFileCountLimit`, `fileSizeLimitBytes`, `rollOnFileSizeLimit`.
- **Reviewer nói:** Thiếu rolling policy.
- **Đánh giá giải pháp:** Không nên làm
- **Ghi chú:** Không thiếu cấu hình; chỉ khác thông số đề xuất (14/10MB thay vì 30/50MB).

### VĐ-S1: Frontend thiếu CSP meta tag
- **Review đúng hay sai:** ✅ Đúng
- **Giá trị thực tế trong code:** `src/frontend/index.html` chưa có CSP meta; CSP hiện chỉ được set ở backend middleware cho API response.
- **Reviewer nói:** SPA frontend thiếu CSP.
- **Đánh giá giải pháp:** Hợp lý
- **Ghi chú:** Cần áp dụng CSP tương thích Vite dev/prod (style inline trong dev).

### VĐ-S2: Thiếu Rate Limiting cho API endpoints nhạy cảm
- **Review đúng hay sai:** ✅ Đúng
- **Giá trị thực tế trong code:** `Program.cs` mới có policies cho `auth-login`, `auth-refresh`; endpoints import/backup/report export/admin user chưa gắn `.RequireRateLimiting(...)`.
- **Reviewer nói:** Thiếu hạn mức cho mutation/export endpoints.
- **Đánh giá giải pháp:** Hợp lý
- **Ghi chú:** Scope nên tập trung đúng các endpoint nhạy cảm nêu trong review.

### VĐ-S3: Password policy chưa enforce complexity
- **Review đúng hay sai:** ❌ Sai
- **Giá trị thực tế trong code:** `AuthSecurityPolicy.ValidatePasswordComplexity` đã tồn tại, có check min length + uppercase + lowercase + digit, đang được gọi trong `AdminEndpoints` khi tạo user.
- **Reviewer nói:** Chưa có enforce complexity.
- **Đánh giá giải pháp:** Không nên làm
- **Ghi chú:** Có thể mở rộng thêm special-char ở task riêng nếu nghiệp vụ yêu cầu, nhưng không phải thiếu hoàn toàn.

### VĐ-U1: Empty state thiếu illustration
- **Review đúng hay sai:** ✅ Đúng
- **Giá trị thực tế trong code:** `.empty-state` hiện chủ yếu chỉ text + nền nhẹ, chưa có icon/title/description structure.
- **Reviewer nói:** Empty state thiếu visual guidance.
- **Đánh giá giải pháp:** Hợp lý
- **Ghi chú:** Nên làm component tái sử dụng để không phải lặp markup thủ công.

### VĐ-U2: Chart tooltip và hover interactivity
- **Review đúng hay sai:** ✅ Đúng
- **Giá trị thực tế trong code:** Cashflow bars đang dùng `title` attribute browser-native.
- **Reviewer nói:** Tooltip native kém UX, đặc biệt trên mobile.
- **Đánh giá giải pháp:** Hợp lý
- **Ghi chú:** Cần fallback accessibility rõ ràng (aria-label/title dự phòng).

### VĐ-U3: Table row height quá cao trên desktop
- **Review đúng hay sai:** ⚠️ Đúng một phần
- **Giá trị thực tế trong code:** App đang dùng `.table-row td, .table-row th { padding: 0.6rem 0.65rem; }` (không phải selector/giá trị `data-table` reviewer nêu).
- **Reviewer nói:** Row height cao, giảm padding.
- **Đánh giá giải pháp:** Cần điều chỉnh
- **Ghi chú:** Giảm có kiểm soát xuống `0.5rem` để giữ readability và click area.

### VĐ-U4: Nav sidebar thiếu collapse-to-icons mode
- **Review đúng hay sai:** ✅ Đúng
- **Giá trị thực tế trong code:** `AppShell.tsx` chỉ có mobile nav open state; `layout-shell.css` chưa có mode collapsed desktop.
- **Reviewer nói:** Thiếu collapse mode để tăng content area ở màn hình trung bình.
- **Đánh giá giải pháp:** Hợp lý
- **Ghi chú:** Cần persist trạng thái bằng localStorage và vẫn giữ full nav cho mobile behavior hiện tại.

## Implementation Plan

### Sẽ triển khai (đã xác nhận)
- [x] VĐ-D1: Giảm typography scale nền (body/heading/caption tokens) — `src/frontend/src/index.css`
- [x] VĐ-D2: Compact spacing tokens toàn cục — `src/frontend/src/index.css`
- [x] VĐ-D3: Giảm kích thước số KPI/radius stat cards/minmax card width — `src/frontend/src/index.css`
- [x] VĐ-D4: Giảm sidebar width desktop 260→240 — `src/frontend/src/styles/layout-shell.css`
- [x] VĐ-D5: Giảm radius card containers chính — `src/frontend/src/index.css`
- [x] VĐ-S1: Thêm CSP meta cho frontend entry — `src/frontend/index.html`
- [x] VĐ-S2: Bổ sung rate-limit policy và apply endpoint nhạy cảm — `src/backend/Api/Program.cs`, `src/backend/Api/Endpoints/{Import,Backup,Report,Admin}Endpoints.cs`
- [x] VĐ-Q1: Tách DashboardPage thành các sub-components theo block chính — `src/frontend/src/pages/DashboardPage.tsx`, `src/frontend/src/pages/dashboard/*`
- [x] VĐ-Q3: Bổ sung Skeleton + dùng cho loading states dashboard chính — `src/frontend/src/components/{Skeleton,StatCardSkeleton}.tsx`, dashboard pages
- [x] VĐ-U1: Cải thiện empty-state structure/style (icon/title/description/action) — `src/frontend/src/components/EmptyState.tsx`, CSS liên quan, các chỗ dashboard/data-table chính
- [x] VĐ-U2: Tooltip chart custom cho cashflow bars — dashboard component + CSS
- [x] VĐ-U3: Giảm row padding table có kiểm soát — `src/frontend/src/index.css`
- [x] VĐ-U4: Desktop nav collapsed mode + persist localStorage — `src/frontend/src/layouts/AppShell.tsx`, `src/frontend/src/styles/layout-shell.css`

### Điều chỉnh so với review gốc
- VĐ-D3: Dùng `minmax(190px, 1fr)` thay vì `180px` để giữ cân bằng density/readability.
- VĐ-Q1: Refactor theo block UI lớn, giữ nguyên data/state logic ở page container để giảm rủi ro regression.
- VĐ-Q3: Ưu tiên rollout skeleton cho dashboard và các block dữ liệu chính trước.
- VĐ-U3: Giảm table row padding xuống `0.5rem` (không xuống 0.45rem) để không giảm khả năng scan/click quá mức.

### Bỏ qua (không hợp lệ hoặc rủi ro cao)
- VĐ-Q2: Tạm chưa tách toàn bộ `index.css` trong đợt này; refactor full CSS monolith là thay đổi lớn, rủi ro regression giao diện cao. Đề xuất tách incremental ở task riêng.
- VĐ-Q4: Bỏ qua vì Serilog rolling đã được cấu hình trong `appsettings.json`.
- VĐ-S3: Bỏ qua vì password complexity validation đã tồn tại và đang được enforce khi tạo user.

## Update 2026-02-28 - VĐ-Q2 incremental execution
- [x] Triển khai tách incremental cho `src/frontend/src/index.css` theo nhóm concern.
- [x] Khóa import order ở `index.css`:
  - `tokens -> base -> primitives -> app-shell -> feedback -> data-display -> forms-filters -> responsive`.
- [x] Tạo thêm `src/frontend/src/styles/global/app-shell.css` để giảm kích thước module lõi.
- [x] `primitives.css` giảm từ `966` xuống `745` lines (đạt guideline < 800 lines/module).
- [x] Tạo tài liệu mapping: `docs/frontend/css-selector-mapping.md`.
- [x] Verify build + e2e baseline:
  - `npm --prefix src/frontend run build` => pass.
  - `npm --prefix src/frontend run test:e2e -- e2e/css-baseline.spec.ts` => pass.
- [x] Regression matrix full viewport + dark/light:
  - `npm --prefix src/frontend run test:e2e -- e2e/css-regression-matrix.spec.ts` (with `E2E_API_TARGET=http://127.0.0.1:18080`) => pass (`2/2`).

## Phase 3 Checklist

### UI Density
- [x] VĐ-D1
- [x] VĐ-D2
- [x] VĐ-D3
- [x] VĐ-D4
- [x] VĐ-D5
- [x] Build verified (`cd src/frontend && npm run build`)

### Security
- [x] VĐ-S1
- [x] VĐ-S2
- [x] Build verified (`cd src/frontend && npm run build`)

### Code Quality
- [x] VĐ-Q1
- [x] VĐ-Q3
- [x] Build verified (`cd src/frontend && npm run build`)

### UX Polish
- [x] VĐ-U1
- [x] VĐ-U2
- [x] VĐ-U3
- [x] VĐ-U4
- [x] Build verified (`cd src/frontend && npm run build`)
