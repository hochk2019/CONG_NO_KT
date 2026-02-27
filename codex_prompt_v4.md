# 🎯 CODEX PROMPT — Đánh giá & Triển khai Opus_review_V4.md

## Vai trò của bạn

Bạn là **Senior Frontend Engineer + UI/UX Specialist** chịu trách nhiệm:
1. **Đánh giá lại** (re-evaluate) từng vấn đề trong `Opus_review_V4.md` — kiểm tra xem phát hiện đó có đúng với codebase hiện tại không (có thể đã được fix hoặc không chính xác)
2. **Triển khai** (implement) tất cả các fix đã được xác nhận là đúng
3. **Ghi nhận** kết quả vào checklist để không bỏ sót

## Tài liệu tham chiếu

- **Review chính:** `Opus_review_V4.md` (17 vấn đề UI/UX, mã VĐ-L1 → VĐ-M2)
- **Review trước đó:** `Opus_review_v3.md` (context tổng thể hệ thống)
- **CSS chính:** `src/frontend/src/index.css`, `src/frontend/src/pages/dashboard/dashboard.css`, `src/frontend/src/styles/layout-shell.css`
- **Pages:** `src/frontend/src/pages/DashboardPage.tsx`, `src/frontend/src/pages/RiskAlertsPage.tsx`, `src/frontend/src/pages/ReportsPage.tsx`, `src/frontend/src/pages/customers/`

## Quy trình làm việc (BẮT BUỘC tuân thủ)

### BƯỚC 1: Tạo task checklist
Tạo file `codex_v4_tasks.md` với checklist chi tiết tất cả 17 vấn đề từ `Opus_review_V4.md`. Format:
```markdown
# Opus Review V4 — Task Checklist

## Phase 1: Đánh giá lại (Re-evaluate)
- [ ] VĐ-L1: Header quá dày → Kiểm tra layout-shell.css, đo thực tế
- [ ] VĐ-L2: 10 KPI cards → Đếm actual KPI cards trong DashboardPage.tsx
- [ ] VĐ-L3: Charts trống khi data thưa → Kiểm tra rendering logic
- [ ] VĐ-C1: Card quá hạn chưa nổi bật → Đo border/bg opacity
- [ ] VĐ-C2: Dark mode muted contrast → Tính contrast ratio thực tế
- [ ] VĐ-C3: Delta pills chỉ dùng màu → Kiểm tra có icon/text prefix chưa
- [ ] VĐ-R1: Font size labels quá nhỏ → Đo actual computed sizes
- [ ] VĐ-R2: Bảng KH thiếu color-coding → Kiểm tra conditional styling
- [ ] VĐ-R3: Date format không nhất quán → Kiểm tra locale settings
- [ ] VĐ-S1: Button style không nhất quán → So sánh across pages
- [ ] VĐ-S2: Tabs Risk dùng tiếng Anh → Kiểm tra text trong TSX
- [ ] VĐ-S3: Spacing sections không đều → Đo gap values
- [ ] VĐ-A1: Thiếu prefers-reduced-motion → Search codebase
- [ ] VĐ-A2: Focus visible thiếu form elements → Search CSS selectors
- [ ] VĐ-A3: Touch target pills quá nhỏ → Đo padding values
- [ ] VĐ-M1: KPI grid chật trên mobile → Kiểm tra breakpoints
- [ ] VĐ-M2: Table scroll thiếu indicator → Kiểm tra scroll UX

## Phase 2: Triển khai (Implement) — Theo thứ tự ưu tiên

### 🔴 Ưu tiên CAO (làm trước)
- [ ] Fix VĐ-A1: Thêm @media (prefers-reduced-motion: reduce)
- [ ] Fix VĐ-A2: Bổ sung :focus-visible cho input, select, textarea
- [ ] Fix VĐ-R1: Tăng font-size labels từ 0.72rem → 0.75rem, 0.8rem → 0.875rem
- [ ] Fix VĐ-C2: Tăng --color-muted dark mode #94a3b8 → #a8b8cc
- [ ] Fix VĐ-C3: Thêm prefix ▲/▼/─ cho delta pills trong DashboardPage.tsx

### 🟡 Ưu tiên TRUNG BÌNH (làm sau ưu tiên cao)
- [ ] Fix VĐ-C1: Tăng border + left accent bar cho stat-card--danger
- [ ] Fix VĐ-S2: Đổi Overview/Config/History → Tổng quan/Cấu hình/Lịch sử
- [ ] Fix VĐ-R3: Thống nhất date format dd/mm/yyyy
- [ ] Fix VĐ-L1: Compact header (nếu xác nhận đúng)
- [ ] Fix VĐ-S3: Thống nhất spacing sections
- [ ] Fix VĐ-A3: Tăng padding touch targets
- [ ] Fix VĐ-S1: Thống nhất button styles

### 🟢 Ưu tiên THẤP (làm nếu còn thời gian)
- [ ] Fix VĐ-R2: Color-coding cột "Dư nợ" bảng khách hàng
- [ ] Fix VĐ-L2: Nhóm KPI cards + section headings
- [ ] Fix VĐ-L3: Empty state cho charts khi data thưa
- [ ] Fix VĐ-M1: 1-column KPI grid cho mobile ≤480px
- [ ] Fix VĐ-M2: Scroll indicator cho tables

## Phase 3: Kiểm tra (Verify)
- [ ] Build thành công: npm run build không lỗi
- [ ] Dark mode: Tất cả fixes hiển thị đúng trong dark mode
- [ ] Light mode: Tất cả fixes hiển thị đúng trong light mode
- [ ] Responsive: Kiểm tra trên viewport 375px, 768px, 1024px
- [ ] Ghi kết quả: Cập nhật Opus_review_V4.md với status từng VĐ
```

### BƯỚC 2: Re-evaluate từng vấn đề
Với MỖI vấn đề (VĐ-L1 đến VĐ-M2):
1. **Mở file liên quan** và kiểm tra code thực tế
2. **So sánh** với mô tả trong Opus_review_V4.md
3. **Đánh dấu** kết quả:
   - ✅ CONFIRMED — vấn đề đúng, cần fix
   - ⚠️ PARTIALLY — đúng nhưng cần điều chỉnh cách fix
   - ❌ OUTDATED — đã được fix hoặc không chính xác
4. **Ghi lại** bằng chứng file + dòng code cụ thể

### BƯỚC 3: Triển khai theo thứ tự ưu tiên
- Fix 🔴 Ưu tiên CAO trước (5 items)
- Sau đó fix 🟡 Ưu tiên TRUNG BÌNH (7 items)
- Cuối cùng fix 🟢 Ưu tiên THẤP nếu còn thời gian (5 items)
- **Sau MỖI fix**: chạy `npm run build` để đảm bảo không break
- **Mỗi fix là 1 commit riêng** với message format: `fix(ui): VĐ-XX — [mô tả ngắn]`

### BƯỚC 4: Verify & Report
- Chạy `npm run build` tổng thể
- Cập nhật `codex_v4_tasks.md` đánh dấu [x] cho tất cả items đã hoàn thành
- Cập nhật `Opus_review_V4.md` thêm cột "Codex Status" vào bảng tổng hợp Phase 7

## Quy tắc quan trọng

1. **KHÔNG bỏ sót**: Checklist có 17 VĐ + 17 fix + 4 verify = 38 items. Tất cả phải được xử lý
2. **KHÔNG sửa bừa**: Re-evaluate TRƯỚC khi sửa. Nếu vấn đề không tồn tại, đánh dấu OUTDATED
3. **KHÔNG sửa nhiều file cùng lúc**: Mỗi VĐ fix riêng + build test riêng
4. **ƯU TIÊN accessibility**: VĐ-A1, VĐ-A2, VĐ-C2, VĐ-C3, VĐ-R1 phải fix trước tất cả
5. **GIỮ backward compatible**: Không thay đổi layout structure, chỉ tweak values
6. **Sử dụng Skills**: Nếu có skills `ui-visual-validator`, `ui-ux-pro-max`, `accessibility-compliance-accessibility-audit` trong `.gemini/antigravity/skills/`, hãy đọc và tuân thủ guidelines từ các skills này

## Kết quả mong đợi

Sau khi hoàn thành:
- `codex_v4_tasks.md` — checklist hoàn chỉnh với tất cả [x]
- `Opus_review_V4.md` — cập nhật status Codex cho từng VĐ
- Code changes trong `src/frontend/` — tất cả fixes đã apply
- Build thành công, không regression
