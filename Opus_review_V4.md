# 📋 CONG NO GOLDEN — Review Vòng 4
## UI/UX Visual Deep Review — Đánh giá Giao diện & Trải nghiệm Trực quan

> **Reviewer:** AI Expert Review Agent (Antigravity + Skills)  
> **Ngày review:** 2026-02-27  
> **Phiên bản hệ thống:** Post V3 + Codex Remediation  
> **Dựa trên:** V3 (6.9/10) → Gemini Re-eval + Codex Remediation  
> **Phạm vi:** Đánh giá chuyên sâu UI/UX trực quan — bố cục, màu sắc, khả năng quan sát, accessibility  
> **Skills sử dụng:** `ui-visual-validator`, `ui-ux-pro-max`, `kpi-dashboard-design`, `accessibility-compliance-accessibility-audit`  
> **Phương pháp:** Duyệt 12 trang trong cả Light & Dark mode, phân tích CSS source code, so sánh với WCAG 2.1 & KPI dashboard best practices

---

## 🔎 Tổng quan đánh giá

| Tiêu chí | Điểm | Mức |
|----------|:----:|:---:|
| Bố cục tổng thể (Layout) | **8/10** | ✅ Tốt |
| Hệ thống màu sắc (Color System) | **7/10** | ⚠️ Khá |
| Khả năng quan sát (Readability) | **6.5/10** | ⚠️ Cần cải thiện |
| Nhất quán giao diện (Consistency) | **7.5/10** | ✅ Khá tốt |
| Accessibility (WCAG) | **5.5/10** | 🔴 Cần cải thiện nhiều |
| Dashboard KPI Design | **7/10** | ⚠️ Khá |
| **Trung bình** | **6.9/10** | |

---

# 🏗️ PHẦN 1 — BỐ CỤC TỔNG THỂ (Layout)

## 1.1 Điểm mạnh

- ✅ **Grid layout chuyên nghiệp**: Sidebar 260px + Content area 1fr, responsive collapse tại 1024px
- ✅ **Phân nhóm menu logic**: Nghiệp vụ (Nhập liệu → Báo cáo) vs Admin (Người dùng → Sao lưu) với badge `ADMIN` xanh nổi bật
- ✅ **Dashboard information flow**: Tóm tắt điều hành → KPI cards → Charts → Top lists — đi từ tổng quát đến chi tiết
- ✅ **Mobile-first breakpoints**: 3 tầng responsive (1024px, 768px, 640px)
- ✅ **Sidebar scrollable**: Menu dài vẫn accessible trên viewport nhỏ

## 1.2 Vấn đề phát hiện

### ❌ VĐ-L1: Header quá dày — chiếm ~30% viewport above-the-fold

**Mức độ:** 🟡 Trung bình  
**Khu vực:** Tất cả các trang  
**Mô tả:** Header area gồm 3 tầng thông tin chồng lên nhau:
1. Context bar (tên trang + vai trò + "Tìm nhanh" + theme toggle)
2. Page title section (Tổng quan công nợ + mô tả + timestamp + "Kỳ hiển thị" dropdown)
3. Executive summary banner

Kết quả: Trên màn hình 1080p, user phải cuộn xuống mới thấy các KPI cards — thông tin quan trọng nhất.

**File liên quan:** `src/frontend/src/styles/layout-shell.css` (`.app-context`, `.app-context__summary`)

**Đề xuất xử lý:**
```css
/* Gộp context bar với page title thành 1 dòng compact */
.app-context__summary {
  flex-direction: row;
  align-items: center;
  gap: 0.75rem;
}

/* Giảm padding vertical cho header */
.app-main {
  padding-top: 1.5rem; /* thay vì 2.5rem */
}

/* Timestamp di chuyển vào badge inline */
/* Đưa "Cập nhật lúc 14:38" thành tooltip hoặc badge nhỏ bên cạnh title */
```

---

### ❌ VĐ-L2: 10 KPI cards — quá nhiều, vi phạm nguyên tắc "5-7 KPIs per view"

**Mức độ:** 🟡 Trung bình  
**Khu vực:** Dashboard  
**Mô tả:** Dashboard hiển thị 10 KPI cards chia 2 hàng:
- **Hàng trên (5):** Tổng dư công nợ, Dư hóa đơn, Dư trả hộ, Đã thu chưa phân bổ, **Quá hạn** (viền đỏ)
- **Hàng dưới (5):** Thu thực tế, KH trả đúng hạn, Thu kỳ vọng, Chênh lệch Actual-Expected, % Actual/Expected

Theo `kpi-dashboard-design` skill: **"Limit to 5-7 KPIs — Focus on what matters"**.

**File liên quan:** `src/frontend/src/pages/DashboardPage.tsx` (stat-grid section)

**Đề xuất xử lý:**
1. Nhóm thành 2 section có heading rõ ràng:
   - **"TÌNH TRẠNG CÔNG NỢ"** (4 cards: Tổng dư, Dư hóa đơn, Dư trả hộ, Chưa phân bổ)
   - **"HIỆU SUẤT THU HỒI"** (4 cards: Thu thực tế, Thu kỳ vọng, Chênh lệch, % Actual/Expected)
2. Card **"Quá hạn"** nên tách thành **alert banner** riêng (vì nó là critical information, cần priority cao hơn KPI card thông thường)
3. Cards "KH trả đúng hạn" có thể gộp vào section hiệu suất dưới dạng sub-metric

---

### ❌ VĐ-L3: Charts area tỷ lệ 2fr:1fr — không tối ưu khi data thưa

**Mức độ:** 🟢 Thấp  
**Khu vực:** Dashboard — "Dòng tiền Expected vs Actual" + "Trạng thái phân bổ"  
**Mô tả:** Biểu đồ dòng tiền chiếm 2/3 nhưng phần lớn cột hiển thị "+0 tỷ" (trống). Donut chart 1/3 bên phải bị hẹp, legend stacked cards chiếm nhiều diện tích.

**File liên quan:** `src/frontend/src/pages/dashboard/dashboard.css` (`.dashboard-charts`), `src/frontend/src/pages/DashboardPage.tsx`

**Đề xuất xử lý:**
```css
/* Khi data thưa, auto-collapse cột 0 hoặc hiển thị message */
.cashflow-chart__group--empty {
  display: none;
}

/* Hoặc hiển thị placeholder */
.cashflow-chart--no-data::after {
  content: "Chưa có dữ liệu giai đoạn này";
  color: var(--color-muted);
  font-size: 0.9rem;
}

/* Donut legend nên dùng inline layout */
.allocation-donut__legend {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}
```

---

# 🎨 PHẦN 2 — HỆ THỐNG MÀU SẮC (Color System)

## 2.1 Điểm mạnh

- ✅ **Design token system hoàn chỉnh**: CSS custom properties cho toàn bộ palette
  - Light: `--color-bg: #f8fafc`, `--color-ink: #0f172a`, `--color-accent: #2563eb`
  - Dark: `--color-bg: #0b1220`, `--color-ink: #e2e8f0`, `--color-accent: #60a5fa`
- ✅ **Dark mode chuyển đổi mượt**: Dùng `color-scheme` + `data-theme` attribute đúng chuẩn
- ✅ **Semantic colors đúng ý nghĩa**: Đỏ = danger/quá hạn, Xanh dương = accent/primary, Xanh lá = success/trả đúng hạn
- ✅ **`color-mix()` function**: Sử dụng hiện đại cho mixed states (danger card, warning borders...)
- ✅ **3 chế độ hiển thị**: Sáng / Tối / Tự động theo hệ thống

## 2.2 Vấn đề phát hiện

### ❌ VĐ-C1: Card "Quá hạn" chưa đủ visual weight so với critical level

**Mức độ:** 🟡 Trung bình  
**Khu vực:** Dashboard — stat-card--danger  
**Mô tả:** 993 triệu VND quá hạn là **thông tin critical nhất** trên dashboard, nhưng card chỉ có:
- Border: `1px solid rgba(220, 38, 38, 0.25)` — quá nhạt (25% opacity)
- Background: `linear-gradient(135deg, rgba(220, 38, 38, 0.12), var(--color-surface))` — gần như invisible

So sánh: User cung cấp 2 screenshots (dark & light) đều cho thấy card quá hạn không nổi bật đủ so với các card bình thường.

**File liên quan:** `src/frontend/src/index.css` dòng 662-665

**Đề xuất xử lý:**
```css
/* Trước: */
.stat-card--danger {
  border: 1px solid rgba(220, 38, 38, 0.25);
  background: linear-gradient(135deg, rgba(220, 38, 38, 0.12), var(--color-surface));
}

/* Sau: Tăng border + thêm left accent bar */
.stat-card--danger {
  border: 1px solid rgba(220, 38, 38, 0.45);
  border-left: 4px solid var(--color-danger);
  background: linear-gradient(135deg, rgba(220, 38, 38, 0.18), var(--color-surface));
}
```

---

### ❌ VĐ-C2: Dark Mode — Chữ phụ "muted" contrast ratio borderline WCAG AA

**Mức độ:** 🔴 Cao (Accessibility)  
**Khu vực:** Toàn bộ trang (Dark Mode)  
**Mô tả:**
- `--color-muted: #94a3b8` trên `--color-surface: #0f172a` → contrast ratio ≈ **4.6:1**
- WCAG AA yêu cầu **4.5:1** cho normal text — đang borderline pass
- Tuy nhiên, nhiều labels dùng font-size ≤ 0.78rem (~12.5px). Ở kích thước nhỏ, text cần contrast ratio cao hơn để readable
- Các vị trí bị ảnh hưởng:
  - KPI labels (`.stat-card__label`, `.kpi-card__label`): 0.8-0.85rem
  - Delta pills (`.kpi-delta`): 0.78rem
  - Chart labels (`.cashflow-chart__label`, `.cashflow-chart__variance`): 0.72rem
  - Dashboard summary label (`.dashboard-summary__label`): 0.78rem
  - Sidebar version text

**File liên quan:** `src/frontend/src/index.css` dòng 44

**Đề xuất xử lý:**
```css
/* Trước: */
:root[data-theme='dark'] {
  --color-muted: #94a3b8;
}

/* Sau: Tăng brightness để đạt contrast ratio ~5.5:1 */
:root[data-theme='dark'] {
  --color-muted: #a8b8cc;
}
```

---

### ❌ VĐ-C3: KPI delta pills chỉ phân biệt bằng màu — vi phạm "Color is not the only indicator"

**Mức độ:** 🔴 Cao (Accessibility)  
**Khu vực:** Dashboard — KPI cards delta area  
**Mô tả:** Các pill "Không đổi so với tháng trước", "Tăng 307.680 (+0%)" chỉ phân biệt bằng:
- Neutral: nền xanh nhạt, text xám
- Positive: nền xanh lá nhạt, text xanh lá
- Negative: nền đỏ nhạt, text đỏ

Người dùng mù màu (color blind) không thể phân biệt positive vs negative.

**File liên quan:** `src/frontend/src/pages/dashboard/dashboard.css` dòng 295-322, `src/frontend/src/pages/DashboardPage.tsx`

**Đề xuất xử lý:**
1. Thêm **icon prefix** trước text delta:
   - Positive: `▲ Tăng 307.680 (+0%)`
   - Negative: `▼ Giảm 50.000 (-5%)`
   - Neutral: `─ Không đổi so với tháng trước`
2. Hoặc thêm **border-left accent** để tạo thêm 1 layer phân biệt ngoài màu

---

# 👁️ PHẦN 3 — KHẢ NĂNG QUAN SÁT (Readability)

## 3.1 Điểm mạnh

- ✅ **Font family chuyên nghiệp**: IBM Plex Sans (body) + Space Grotesk (display) — clear cho cả text lẫn số
- ✅ **`font-variant-numeric: tabular-nums`** cho số tiền → căn thẳng hàng perfect trong bảng
- ✅ **Line-height 1.5** cho body text — đạt chuẩn readability
- ✅ **Font size responsive**: `clamp()` cho headings (1.8rem → 2.6rem), đảm bảo scalable

## 3.2 Vấn đề phát hiện

### ❌ VĐ-R1: Font size cho labels/charts quá nhỏ — gần mức không đọc được

**Mức độ:** 🔴 Cao  
**Khu vực:** Dashboard charts, KPI labels, delta pills  
**Mô tả:** Nhiều text elements có font-size dưới 13px, gây khó đọc đặc biệt trên:
- Màn hình DPI thấp (1366x768 phổ biến ở VN)
- Khoảng cách > 50cm
- Người dùng > 40 tuổi (đối tượng kế toán, quản lý tài chính)

| Element | CSS class | Font size hiện tại | Khuyến nghị tối thiểu |
|---------|-----------|:------------------:|:---------------------:|
| Chart variance | `.cashflow-chart__variance` | 0.72rem (11.5px) | **0.75rem (12px)** |
| Chart label | `.cashflow-chart__label` | 0.72rem (11.5px) | **0.75rem (12px)** |
| Dashboard summary label | `.dashboard-summary__label` | 0.78rem (12.5px) | **0.8rem (12.8px)** |
| KPI delta pill | `.kpi-delta` | 0.78rem (12.5px) | **0.8rem (12.8px)** |
| KPI card label | `.kpi-card__label` | 0.8rem (12.8px) | **0.875rem (14px)** |
| Stat card label | `.stat-card__label` | 0.85rem (13.6px) | **0.875rem (14px)** |
| Role cockpit status | `.role-cockpit__status` | 0.72rem (11.5px) | **0.75rem (12px)** |

**File liên quan:** `src/frontend/src/index.css`, `src/frontend/src/pages/dashboard/dashboard.css`

**Đề xuất xử lý:**
```css
/* Tăng các font-size labels lên mức readable tối thiểu */
.cashflow-chart__variance,
.cashflow-chart__label,
.role-cockpit__status {
  font-size: 0.75rem; /* 12px thay vì 11.5px */
}

.dashboard-summary__label,
.kpi-delta {
  font-size: 0.8rem; /* 12.8px thay vì 12.5px */
}

.kpi-card__label,
.stat-card__label {
  font-size: 0.875rem; /* 14px thay vì 12.8-13.6px */
}
```

---

### ❌ VĐ-R2: Trang Khách hàng — Bảng data thiếu visual hierarchy cho giá trị lớn

**Mức độ:** 🟡 Trung bình  
**Khu vực:** `/customers` — Danh sách khách hàng  
**Mô tả:** Cột "Dư nợ" hiển thị số tiền cho tất cả khách hàng cùng style:
- 239 đ → nhìn giống
- 5.594.400 đ → nhìn giống
- 492.654.457 đ → nhìn giống

Không có phân biệt trực quan giữa nợ lớn vs nợ nhỏ. User phải đọc từng số để so sánh.

**File liên quan:** `src/frontend/src/pages/customers/` (table rendering)

**Đề xuất xử lý:**
1. Thêm color-coding cho cột "Dư nợ":
   - ≥ 100 triệu: `color: var(--color-danger)` + bold
   - 10–100 triệu: `color: var(--color-warning)`
   - < 10 triệu: `color: var(--color-ink)` (default)
2. Hoặc thêm progress bar mini inline để trực quan hóa tỷ lệ

---

### ❌ VĐ-R3: Date format không nhất quán — mm/dd/yyyy vs dd/mm/yyyy

**Mức độ:** 🟡 Trung bình  
**Khu vực:** Reports, Risk Alerts  
**Mô tả:**
- Date picker placeholder hiển thị `mm/dd/yyyy` (US format)
- Timestamp trên Dashboard hiển thị `27/02/2026` (VN format)
- Ứng dụng dành cho thị trường Việt Nam → nên thống nhất `dd/mm/yyyy`

**File liên quan:** `src/frontend/src/pages/ReportsPage.tsx`, HTML `<input type="date">` default browser locale

**Đề xuất xử lý:**
- Sử dụng custom date picker component (hoặc set locale cho HTML date input)
- Thống nhất display format: `dd/mm/yyyy`
- Hoặc dùng format `27 Thg 2, 2026` cho readability tốt hơn

---

# 🔄 PHẦN 4 — NHẤT QUÁN GIAO DIỆN (Consistency)

## 4.1 Vấn đề phát hiện

### ❌ VĐ-S1: Button style không nhất quán giữa các trang

**Mức độ:** 🟡 Trung bình  
**Khu vực:** So sánh Dashboard, Báo cáo, Cảnh báo rủi ro  
**Mô tả:**

| Trang | Primary actions | Secondary actions |
|-------|----------------|-------------------|
| Dashboard | Pill filled xanh (`Tùy chỉnh Dashboard`) | Pill ghost (`Hướng dẫn`) |
| Báo cáo | Pill filled xanh (`Tải tổng quan`) | Pill outline xanh (`Tải tổng hợp`, `Tải sao kê`) |
| **HEADer: Bộ lọc** | Rounded-rect outline, khác style pill ở dưới | — |
| Cảnh báo rủi ro | Pill filled xanh (`Overview`) | Pill ghost (`Config`, `History`) |
| Receipts | Pill filled (`Tạo phiếu thu draft`) | — |

Vấn đề: Button "In báo cáo", "Bộ lọc" ở Reports trông khác style so với "Tùy chỉnh Dashboard".

**Đề xuất xử lý:**
Thống nhất quy tắc:
- **Primary action** = `.btn-primary` (filled pill, blue)
- **Secondary action** = `.btn-outline` (outline pill, blue border)
- **Tertiary action** = `.btn-ghost` (no border, subtle bg)
- Không mix rounded-rect với pill trong cùng 1 page header

---

### ❌ VĐ-S2: Tabs Cảnh báo rủi ro dùng tiếng Anh lẫn Việt

**Mức độ:** 🟡 Trung bình  
**Khu vực:** `/risk` — Risk Alerts page  
**Mô tả:** Tabs hiện tại: `Overview / Config / History` — tiếng Anh. Trong khi:
- Title: "Rủi ro công nợ & nhắc kế toán" — tiếng Việt
- Mọi label, content đều tiếng Việt
- Inconsistency rõ ràng

**File liên quan:** `src/frontend/src/pages/RiskAlertsPage.tsx`

**Đề xuất xử lý:**
```tsx
// Trước:
const tabs = ['Overview', 'Config', 'History'];

// Sau:
const tabs = ['Tổng quan', 'Cấu hình', 'Lịch sử'];
```

---

### ❌ VĐ-S3: Spacing giữa sections trên Dashboard không đều

**Mức độ:** 🟢 Thấp  
**Khu vực:** Dashboard  
**Mô tả:** Gap giữa các section:
- Executive summary → KPI hàng 1: ~`space-3` (1rem)
- KPI hàng 1 → KPI hàng 2: ~0.75rem (nhỏ hơn)
- KPI hàng 2 → Charts: ~`space-4` (1.5rem)
- Charts → Top lists: ~`space-4`

**Đề xuất xử lý:** Thống nhất tất cả major section gaps = `var(--space-4)` (1.5rem)

---

# ♿ PHẦN 5 — ACCESSIBILITY (WCAG Compliance)

## 5.1 Vấn đề phát hiện

### ❌ VĐ-A1: Thiếu `prefers-reduced-motion` — vi phạm WCAG 2.1 Level AAA

**Mức độ:** 🔴 Cao  
**Khu vực:** Toàn bộ ứng dụng  
**Mô tả:** 
Có transition trên:
- Buttons: `transition: transform 0.2s ease, box-shadow 0.2s ease` (~dòng 150, index.css)
- Chart bars: `transition: transform 0.2s ease` (`.cashflow-chart__bar`)
- Nav items: `transition: background 0.2s ease, color 0.2s ease`

Nhưng **KHÔNG CÓ** `@media (prefers-reduced-motion: reduce)`. Người dùng có vestibular disorders hoặc motion sensitivity sẽ bị ảnh hưởng.

**File liên quan:** `src/frontend/src/index.css` (nên thêm ở cuối file)

**Đề xuất xử lý:**
```css
@media (prefers-reduced-motion: reduce) {
  *,
  *::before,
  *::after {
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
    scroll-behavior: auto !important;
  }
}
```

---

### ❌ VĐ-A2: Focus visible không đầy đủ cho form elements

**Mức độ:** 🔴 Cao  
**Khu vực:** Tất cả form-based pages  
**Mô tả:**
Hiện tại đã có `:focus-visible` cho (dòng 238-244, index.css):
- `.btn`
- `.nav-item`
- `.table-sort`
- `a`

Nhưng **THIẾU** cho:
- `<input>` text fields
- `<select>` dropdowns
- `<textarea>`
- Donut chart (`.allocation-donut` — có role clickable)
- Cards nếu có role interactive
- Checkbox / radio buttons

**File liên quan:** `src/frontend/src/index.css` dòng 238-245

**Đề xuất xử lý:**
```css
/* Bổ sung focus visible cho form elements */
input:focus-visible,
select:focus-visible,
textarea:focus-visible,
[role="button"]:focus-visible,
.allocation-donut:focus-visible {
  outline: 2px solid var(--color-focus);
  outline-offset: 3px;
  box-shadow: 0 0 0 4px rgba(37, 99, 235, 0.12);
}
```

---

### ❌ VĐ-A3: Touch target size cho pills và badges quá nhỏ

**Mức độ:** 🟡 Trung bình  
**Khu vực:** Dashboard KPI delta pills, sidebar nav pills  
**Mô tả:**
- `.kpi-delta` padding: `0.2rem 0.6rem` → kích thước rendered ~ **32×20px**
- `.nav-pill` padding: `0.2rem 0.55rem` → tương tự
- **Chuẩn WCAG 2.2**: Target size tối thiểu 24×24px (Level AA) hoặc 44×44px (Level AAA)
- Nếu pills có `cursor: pointer` / interactive → cần ≥ 44×44px

**File liên quan:** `src/frontend/src/pages/dashboard/dashboard.css` dòng 295-300, `src/frontend/src/styles/layout-shell.css` (~dòng 92)

**Đề xuất xử lý:**
```css
.kpi-delta {
  padding: 0.35rem 0.75rem; /* Tăng từ 0.2rem 0.6rem */
  min-height: 32px; /* Đảm bảo touch target */
}
```

---

# 📱 PHẦN 6 — RESPONSIVE & MOBILE

## 6.1 Điểm mạnh

- ✅ Sidebar collapse hamburger tại ≤1024px (`layout-shell.css`)
- ✅ Dashboard charts stack vertically tại ≤1200px
- ✅ Main content padding giảm 2rem → 1rem tại ≤640px
- ✅ Modal responsive `width: min(960px, 100%)`

## 6.2 Vấn đề phát hiện

### ❌ VĐ-M1: KPI grid minmax(170px, 1fr) — trên mobile 375px, cards bị chật

**Mức độ:** 🟢 Thấp  
**Khu vực:** Dashboard on mobile  
**Mô tả:** Grid `repeat(auto-fit, minmax(170px, 1fr))` → trên 375px viewport, 2 cards/row với mỗi card chỉ ~170px, số liệu lớn (993.145.665 đ) có thể bị truncate hoặc wrap xấu.

**Đề xuất xử lý:**
```css
@media (max-width: 480px) {
  .stat-grid,
  .kpi-grid {
    grid-template-columns: 1fr; /* 1 card/row trên mobile nhỏ */
  }
}
```

---

### ❌ VĐ-M2: Table horizontal scroll thiếu scroll indicator

**Mức độ:** 🟢 Thấp  
**Khu vực:** Customers, Reports, Risk Alerts — data tables  
**Mô tả:** Tables dùng `overflow-x: auto` nhưng không có visual hint cho user biết "có thể cuộn ngang". Trên mobile, nhiều cột bị ẩn ngoài viewport mà user không biết.

**Đề xuất xử lý:**
```css
.table-scroll {
  position: relative;
}

.table-scroll::after {
  content: '';
  position: absolute;
  top: 0;
  right: 0;
  bottom: 0;
  width: 40px;
  background: linear-gradient(90deg, transparent, var(--color-surface));
  pointer-events: none;
  opacity: 0;
  transition: opacity 0.2s;
}

/* Show indicator when scrollable */
.table-scroll--scrollable::after {
  opacity: 1;
}
```

---

# 📋 PHẦN 7 — TỔNG HỢP ĐỀ XUẤT XỬ LÝ

## Bảng tổng hợp 14 vấn đề — Sắp xếp theo mức ưu tiên

### 🔴 Ưu tiên CAO (Accessibility & Readability) — NÊN LÀM NGAY

| # | Mã VĐ | Vấn đề | File cần sửa | Hành động | Codex Status |
|:-:|:-----:|--------|:------------:|-----------|--------------|
| 1 | VĐ-A1 | Thiếu `prefers-reduced-motion` | `index.css` | Thêm media query tắt animation/transition | ✅ Fixed (`4b82c38`) |
| 2 | VĐ-A2 | Focus visible thiếu cho input/select | `index.css` | Bổ sung `:focus-visible` cho form elements | ✅ Fixed (`33d7cba`) |
| 3 | VĐ-R1 | Font size labels ≤ 0.72rem quá nhỏ | `dashboard.css`, `index.css` | Tăng labels lên ≥ 0.75rem, KPI labels ≥ 0.875rem | ✅ Fixed (`b5d2df2`) |
| 4 | VĐ-C2 | Dark mode muted color borderline WCAG | `index.css` | `--color-muted: #a8b8cc` (dark mode) | ✅ Fixed (`cb4f960`) |
| 5 | VĐ-C3 | Delta pills chỉ phân biệt bằng màu | `DashboardPage.tsx` | Thêm prefix ▲/▼/─ cho text delta | ✅ Fixed (`7331343`) |

### 🟡 Ưu tiên TRUNG BÌNH (Visual Polish) — NÊN LÀM TRONG SPRINT KẾ TIẾP

| # | Mã VĐ | Vấn đề | File cần sửa | Hành động | Codex Status |
|:-:|:-----:|--------|:------------:|-----------|--------------|
| 6 | VĐ-C1 | Card "Quá hạn" chưa đủ nổi bật | `index.css` | Tăng border + thêm left accent bar | ✅ Fixed (`af6f465`) |
| 7 | VĐ-S2 | Tabs Risk dùng tiếng Anh | `RiskAlertsPage.tsx` | Đổi Overview/Config/History → Tổng quan/Cấu hình/Lịch sử | ✅ Fixed (`3d6dff5`) |
| 8 | VĐ-R3 | Date format không nhất quán | Frontend config | Thống nhất dd/mm/yyyy | ✅ Fixed (`f1ed206`) |
| 9 | VĐ-L1 | Header quá dày, chiếm 30% viewport | `layout-shell.css` | Compact header, gộp context bar | ✅ Fixed (`dd0be89`) |
| 10 | VĐ-S3 | Spacing sections dashboard không đều | `dashboard.css` | Thống nhất gap = space-4 | ✅ Fixed (`ae3cfeb`) |
| 11 | VĐ-A3 | Touch target pills quá nhỏ | `dashboard.css` | Tăng padding pills | ✅ Fixed (`b5e7020`) |
| 12 | VĐ-S1 | Button style không nhất quán | Multiple pages | Thống nhất primary/secondary/tertiary pattern | ✅ Fixed (`6d74f3e`) |

### 🟢 Ưu tiên THẤP (Nice to have) — KHI CÓ THỜI GIAN

| # | Mã VĐ | Vấn đề | File cần sửa | Hành động | Codex Status |
|:-:|:-----:|--------|:------------:|-----------|--------------|
| 13 | VĐ-R2 | Bảng KH thiếu color-coding dư nợ | `CustomersPage.tsx` | Conditional styling cho cột "Dư nợ" | ✅ Fixed (`379c91d`) |
| 14 | VĐ-L2 | 10 KPI cards quá nhiều | `DashboardPage.tsx` | Nhóm + section headings + collapse | ✅ Fixed (`e821288`) |
| 15 | VĐ-L3 | Charts trống khi data thưa | `DashboardPage.tsx`, `dashboard.css` | Empty state placeholder | ✅ Fixed (`f4a3b74`) |
| 16 | VĐ-M1 | KPI grid chật trên mobile 375px | `index.css` | 1-column layout cho ≤480px | ✅ Fixed (`5617907`) |
| 17 | VĐ-M2 | Table scroll thiếu indicator | `index.css` | Gradient shadow hint | ✅ Fixed (`e8384e4`) |

---

## Tóm tắt Review Vòng 4

> **Score UI/UX trực quan: 6.9/10**
> 
> Hệ thống có nền tảng UI/UX **khá tốt**:
> - ✅ Design token system hoàn chỉnh (CSS custom properties)
> - ✅ Light/Dark mode chuyên nghiệp
> - ✅ Layout grid rõ ràng, responsive
> - ✅ Typography chuyên nghiệp (IBM Plex Sans + Space Grotesk)
> 
> **Cần cải thiện ngay** (5 items ưu tiên cao):
> 1. **Accessibility**: `prefers-reduced-motion` + focus visible cho form elements
> 2. **Readability**: Font size labels tối thiểu 12px, dark mode muted color tăng contrast
> 3. **Color-only**: Delta pills cần thêm shape/text indicator ngoài màu
> 
> **Sau khi fix 5 items ưu tiên cao**, dự kiến score tăng lên **7.5-8.0/10**.
> 
> Review này **chỉ đánh giá trực quan**, không bao gồm review backend/security/architecture (đã có trong V1-V3). Kết hợp V3 (6.9/10 overall) + V4 UI/UX fixes → hệ thống sẽ đạt mức **7.5/10 tổng thể**.
