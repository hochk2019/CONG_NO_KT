# Opus Review V4 — Task Checklist

## Phase 1: Đánh giá lại (Re-evaluate)
- [x] VĐ-L1: Header quá dày → Kiểm tra layout-shell.css, đo thực tế
- [x] VĐ-L2: 10 KPI cards → Đếm actual KPI cards trong DashboardPage.tsx
- [x] VĐ-L3: Charts trống khi data thưa → Kiểm tra rendering logic
- [x] VĐ-C1: Card quá hạn chưa nổi bật → Đo border/bg opacity
- [x] VĐ-C2: Dark mode muted contrast → Tính contrast ratio thực tế
- [x] VĐ-C3: Delta pills chỉ dùng màu → Kiểm tra có icon/text prefix chưa
- [x] VĐ-R1: Font size labels quá nhỏ → Đo actual computed sizes
- [x] VĐ-R2: Bảng KH thiếu color-coding → Kiểm tra conditional styling
- [x] VĐ-R3: Date format không nhất quán → Kiểm tra locale settings
- [x] VĐ-S1: Button style không nhất quán → So sánh across pages
- [x] VĐ-S2: Tabs Risk dùng tiếng Anh → Kiểm tra text trong TSX
- [x] VĐ-S3: Spacing sections không đều → Đo gap values
- [x] VĐ-A1: Thiếu prefers-reduced-motion → Search codebase
- [x] VĐ-A2: Focus visible thiếu form elements → Search CSS selectors
- [x] VĐ-A3: Touch target pills quá nhỏ → Đo padding values
- [x] VĐ-M1: KPI grid chật trên mobile → Kiểm tra breakpoints
- [x] VĐ-M2: Table scroll thiếu indicator → Kiểm tra scroll UX

## Phase 2: Triển khai (Implement) — Theo thứ tự ưu tiên

### 🔴 Ưu tiên CAO (làm trước)
- [x] Fix VĐ-A1: Thêm @media (prefers-reduced-motion: reduce)
- [x] Fix VĐ-A2: Bổ sung :focus-visible cho input, select, textarea
- [x] Fix VĐ-R1: Tăng font-size labels từ 0.72rem → 0.75rem, 0.8rem → 0.875rem
- [x] Fix VĐ-C2: Tăng --color-muted dark mode #94a3b8 → #a8b8cc
- [x] Fix VĐ-C3: Thêm prefix ▲/▼/─ cho delta pills trong DashboardPage.tsx

### 🟡 Ưu tiên TRUNG BÌNH (làm sau ưu tiên cao)
- [x] Fix VĐ-C1: Tăng border + left accent bar cho stat-card--danger
- [x] Fix VĐ-S2: Đổi Overview/Config/History → Tổng quan/Cấu hình/Lịch sử
- [x] Fix VĐ-R3: Thống nhất date format dd/mm/yyyy
- [x] Fix VĐ-L1: Compact header (nếu xác nhận đúng)
- [x] Fix VĐ-S3: Thống nhất spacing sections
- [x] Fix VĐ-A3: Tăng padding touch targets
- [x] Fix VĐ-S1: Thống nhất button styles

### 🟢 Ưu tiên THẤP (làm nếu còn thời gian)
- [x] Fix VĐ-R2: Color-coding cột "Dư nợ" bảng khách hàng
- [x] Fix VĐ-L2: Nhóm KPI cards + section headings
- [x] Fix VĐ-L3: Empty state cho charts khi data thưa
- [x] Fix VĐ-M1: 1-column KPI grid cho mobile ≤480px
- [x] Fix VĐ-M2: Scroll indicator cho tables

## Phase 3: Kiểm tra (Verify)
- [x] Build thành công: npm run build không lỗi
- [x] Dark mode: Tất cả fixes hiển thị đúng trong dark mode
- [x] Light mode: Tất cả fixes hiển thị đúng trong light mode
- [x] Responsive: Kiểm tra trên viewport 375px, 768px, 1024px
- [x] Ghi kết quả: Cập nhật Opus_review_V4.md với status từng VĐ

## Re-evaluate Log (Codex)
| VĐ | Status | Evidence |
|---|---|---|
| VĐ-L1 | ✅ CONFIRMED | `src/frontend/src/styles/layout-shell.css:140` (`.app-main` padding `var(--space-6) var(--space-7)`), `src/frontend/src/layouts/AppShell.tsx:336-385` block context dày, cộng với `src/frontend/src/pages/DashboardPage.tsx:919-950` page header. |
| VĐ-L2 | ✅ CONFIRMED | `src/frontend/src/pages/DashboardPage.tsx:662-699` (5 primary cards) + `700-729` (5 secondary cards) = 10 KPI cards. |
| VĐ-L3 | ⚠️ PARTIALLY | Có empty-state khi `cashflowPoints.length===0` ở `src/frontend/src/pages/DashboardPage.tsx:785-787`, nhưng chưa xử lý trạng thái data thưa/all-zero. |
| VĐ-C1 | ✅ CONFIRMED | `src/frontend/src/index.css:662-665` danger card chỉ có border/background nhẹ (`rgba(...,0.25)` và gradient `0.12`). |
| VĐ-C2 | ✅ CONFIRMED | `src/frontend/src/index.css:42` dark token `--color-muted: #94a3b8`. |
| VĐ-C3 | ✅ CONFIRMED | `src/frontend/src/pages/DashboardPage.tsx:264-285` delta text chưa có prefix icon ▲/▼/─. |
| VĐ-R1 | ✅ CONFIRMED | `src/frontend/src/pages/dashboard/dashboard.css:416,432` font-size `0.72rem`; `src/frontend/src/index.css:410` `0.8rem`. |
| VĐ-R2 | ✅ CONFIRMED | `src/frontend/src/pages/customers/CustomerListSection.tsx:480-483` cột “Dư nợ” render text thuần, chưa conditional color-coding. |
| VĐ-R3 | ⚠️ PARTIALLY | Hầu hết dùng helper thống nhất (`src/frontend/src/utils/format.ts`), nhưng `src/frontend/src/pages/ReportsPage.tsx:838-841` dùng `dateStyle/timeStyle: short` riêng. |
| VĐ-S1 | ⚠️ PARTIALLY | Nhiều action dùng `btn`, nhưng controls chính ở dashboard/risk dùng class riêng (`unit-toggle__btn`, `tab`) tại `DashboardPage.tsx:742-767`, `RiskAlertsPage.tsx:708-721`. |
| VĐ-S2 | ✅ CONFIRMED | `src/frontend/src/pages/RiskAlertsPage.tsx:60-64` tabs vẫn là English (`Overview/Config/History`). |
| VĐ-S3 | ⚠️ PARTIALLY | Có base spacing (`.page-stack` tại `src/frontend/src/index.css:247-250`) nhưng dashboard còn nhiều margin/gap hardcoded khác nhau trong `src/frontend/src/pages/dashboard/dashboard.css`. |
| VĐ-A1 | ✅ CONFIRMED | Không có `@media (prefers-reduced-motion: reduce)` trong CSS chính. |
| VĐ-A2 | ✅ CONFIRMED | `src/frontend/src/index.css:238-245` chỉ focus-visible cho `.btn/.nav-item/.table-sort/a`; chưa có `input/select/textarea`. |
| VĐ-A3 | ✅ CONFIRMED | Touch targets nhỏ: `src/frontend/src/pages/dashboard/dashboard.css:299` (`0.2rem 0.6rem`), `src/frontend/src/styles/layout-shell.css:91` (`0.2rem 0.55rem`), `src/frontend/src/index.css:1038` (`0.2rem 0.6rem`). |
| VĐ-M1 | ✅ CONFIRMED | `src/frontend/src/index.css:393-398` KPI grid minmax `170px`, chưa có override 1 cột cho `<=480px`. |
| VĐ-M2 | ✅ CONFIRMED | `src/frontend/src/index.css:730-735` `.table-scroll` chỉ overflow-x auto, chưa có scroll indicator UX. |
