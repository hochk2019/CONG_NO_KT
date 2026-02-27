# 📋 CONG NO GOLDEN — Review Vòng 5
## Full-Stack Comprehensive Review — Architecture, Density, Code Quality, Security, UX

> **Reviewer:** AI Expert Review Agent (Antigravity)  
> **Ngày review:** 2026-02-27  
> **Phiên bản hệ thống:** Post V4 + Codex Remediation (17/17 UI fixes hoàn thành)  
> **Dựa trên:** V3 (6.9/10) → V4 UI Fix → Final Review (8.5/10)  
> **Phạm vi:** Cải thiện toàn diện — UI Density, Code Quality, Security, UX Polish  
> **Skills sử dụng:** `ui-ux-pro-max`  
> **Score mục tiêu:** 8.5/10 → **9.0/10**

---

## 🔎 Tổng quan — Vấn đề còn lại sau V4

| Nhóm | Số VĐ | Mức ưu tiên |
|------|:-----:|:-----------:|
| UI Density (Mật độ hiển thị) | 5 | 🔴 CAO |
| Code Quality (Chất lượng mã) | 4 | 🟡 TRUNG BÌNH |
| Security (Bảo mật) | 3 | 🟡 TRUNG BÌNH |
| UX Polish (Trải nghiệm) | 4 | 🟢 THẤP |
| **Tổng** | **16** | |

---

# 🏗️ PHẦN 1 — UI DENSITY (Mật độ hiển thị trên Full HD)

## Bối cảnh

Người dùng trên màn hình Full HD (1920×1080, phổ biến nhất VN) phải zoom trình duyệt xuống **85-90%** mới thoải mái. Nguyên nhân: UI sử dụng spacing kiểu consumer/landing page thay vì data-dense enterprise app.

**Tính toán không gian hiện tại @ 100% zoom:**
- Sidebar: 260px → Content area: 1920-260 = 1660px
- Content padding: 2×40px (space-6) → Effective: 1580px
- Vertical: Header+Summary ≈ 260px → Chỉ còn ~788px cho KPI cards before fold

---

### ❌ VĐ-D1: Base font quá lớn — 16px vs benchmark 14-15px

**Mức độ:** 🔴 Cao  
**Khu vực:** Toàn bộ ứng dụng  
**Mô tả:** `--font-size-body: 1rem` (16px) lớn hơn 6-13% so với enterprise benchmark (Jira 14px, Salesforce 14px, SAP 13px). Khiến tất cả text, form, table chiếm nhiều không gian hơn cần thiết.

**File cần sửa:** `src/frontend/src/index.css`

**Thay đổi yêu cầu:**
```css
/* ---- :root ---- */
/* TRƯỚC */
--font-size-body: 1rem;
--font-size-h3: 1.2rem;
--font-size-caption: 0.8rem;
--font-size-h1: clamp(1.8rem, 1.4rem + 2vw, 2.6rem);
--font-size-h2: clamp(1.4rem, 1.2rem + 1.2vw, 2rem);

/* SAU */
--font-size-body: 0.9375rem;     /* 15px — enterprise standard */
--font-size-h3: 1.1rem;          /* 17.6px thay vì 19.2px */
--font-size-caption: 0.75rem;    /* 12px */
--font-size-h1: clamp(1.5rem, 1.2rem + 1.6vw, 2.2rem);
--font-size-h2: clamp(1.2rem, 1rem + 1vw, 1.7rem);
```

---

### ❌ VĐ-D2: Spacing tokens quá rộng cho data-heavy UI

**Mức độ:** 🔴 Cao  
**Khu vực:** Toàn bộ ứng dụng  
**Mô tả:** Spacing tokens từ `space-3` (16px) đến `space-7` (48px) quá generous. Enterprise apps thường dùng 12-16px cho section gaps, 8-12px cho element gaps.

**File cần sửa:** `src/frontend/src/index.css`

**Thay đổi yêu cầu:**
```css
/* ---- :root ---- */
/* TRƯỚC */
--space-1: 0.5rem;     /* 8px */
--space-2: 0.75rem;    /* 12px */
--space-3: 1rem;       /* 16px */
--space-4: 1.5rem;     /* 24px */
--space-5: 2rem;       /* 32px */
--space-6: 2.5rem;     /* 40px */
--space-7: 3rem;       /* 48px */

/* SAU */
--space-1: 0.375rem;   /* 6px */
--space-2: 0.625rem;   /* 10px */
--space-3: 0.875rem;   /* 14px */
--space-4: 1.25rem;    /* 20px */
--space-5: 1.5rem;     /* 24px */
--space-6: 2rem;       /* 32px */
--space-7: 2.5rem;     /* 40px */
```

---

### ❌ VĐ-D3: Stat card values quá to, card border-radius quá tròn

**Mức độ:** 🔴 Cao  
**Khu vực:** Dashboard KPI cards, trang Khách hàng stat cards  
**Mô tả:** `.stat-card__value` 1.6rem (25.6px) và primary 1.85rem (29.6px) chiếm quá nhiều vertical space. `border-radius: 18px` tạo cảm giác "bubbly" thay vì professional. Enterprise benchmark: stat values 20-22px, radius 8-12px.

**File cần sửa:** `src/frontend/src/index.css`

**Thay đổi yêu cầu:**
```css
/* ---- .stat-card ---- */
/* TRƯỚC */
.stat-card {
  padding: var(--space-3);          /* 16px → sẽ thành 14px sau VĐ-D2 */
  border-radius: 18px;
}
.stat-card__value {
  font-size: 1.6rem;               /* 25.6px */
}

/* SAU */
.stat-card {
  padding: var(--space-3);
  border-radius: 12px;             /* compact hơn, professional hơn */
}
.stat-card__value {
  font-size: 1.35rem;              /* 21.6px — theo benchmark */
}

/* ---- stat-grid primary override ---- */
/* TRƯỚC (index.css dòng ~2317) */
.stat-grid--primary .stat-card__value {
  font-size: 1.85rem;             /* 29.6px */
}

/* SAU */
.stat-grid--primary .stat-card__value {
  font-size: 1.5rem;              /* 24px */
}
```

**Ngoài ra**, giảm stat-grid minmax từ 220px → 180px để fit thêm cards/row:
```css
/* TRƯỚC */
.stat-grid {
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
}

/* SAU */
.stat-grid {
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
}
```

---

### ❌ VĐ-D4: Content padding quá rộng, sidebar quá to

**Mức độ:** 🟡 Trung bình  
**Khu vực:** Layout shell  
**Mô tả:** `.app-main` padding `var(--space-5) var(--space-6)` = 32px 40px. Sidebar 260px fixed. Effective content width chỉ còn 1580px trên 1920px screen — lãng phí 18% horizontal space.

**File cần sửa:** `src/frontend/src/styles/layout-shell.css`

**Thay đổi yêu cầu:**
```css
/* ---- .app-shell ---- */
/* TRƯỚC */
.app-shell {
  grid-template-columns: 260px 1fr;
}

/* SAU */
.app-shell {
  grid-template-columns: 240px 1fr;    /* -20px sidebar */
}

/* ---- .app-main ---- */
/* padding sẽ tự giảm qua VĐ-D2 (space-5/space-6 tokens đã giảm) */
/* Không cần thay đổi thêm nếu VĐ-D2 đã áp dụng */
```

---

### ❌ VĐ-D5: Card container border-radius và padding quá generous

**Mức độ:** 🟡 Trung bình  
**Khu vực:** Tất cả `.card` elements  
**Mô tả:** `.card` containers dùng padding `var(--space-4)` (24px → 20px sau VĐ-D2) và border-radius lớn. Cần thống nhất giảm radius.

**File cần sửa:** `src/frontend/src/index.css`

**Thay đổi yêu cầu:**  
Tìm tất cả `border-radius` có giá trị `≥ 16px` áp dụng cho `.card`, `.card-hero`, containers lớn, và giảm xuống `12px`. Giữ nguyên radius cho buttons (`999px`), pills, và small elements.

```css
/* Áp dụng cho các selectors chứa card lớn */
/* TRƯỚC */
.card { border-radius: 20px; }
.card-hero { border-radius: 20px; }

/* SAU */
.card { border-radius: 14px; }
.card-hero { border-radius: 14px; }
```

> **Lưu ý:** KHÔNG thay đổi border-radius cho `.btn`, `.pill`, `.nav-pill`, `.notification-bell__badge`, `.user-chip`, `.filter-chip` — giữ nguyên rounded pills.

---

# 🔧 PHẦN 2 — CODE QUALITY (Chất lượng mã)

### ❌ VĐ-Q1: DashboardPage.tsx quá lớn (994 dòng)

**Mức độ:** 🟡 Trung bình  
**Khu vực:** `src/frontend/src/pages/DashboardPage.tsx`  
**Mô tả:** File 994 dòng / 39KB — khó maintain, debug, review. Nên tách thành sub-components.

**Thay đổi yêu cầu:**  
Tách `DashboardPage.tsx` thành các sub-components riêng trong thư mục `src/frontend/src/pages/dashboard/`:

| Component mới | Nội dung tách ra | Dòng ước tính |
|---------------|-----------------|:------------:|
| `DashboardKpiSection.tsx` | KPI cards rendering (cả 2 groups) | ~150 |
| `DashboardCashflowChart.tsx` | Cashflow chart + forecast | ~200 |
| `DashboardOverdueChart.tsx` | Overdue bar chart | ~100 |
| `DashboardTopCustomers.tsx` | Top customers table | ~100 |
| `DashboardExecutiveSummary.tsx` | Executive summary banner | ~50 |

`DashboardPage.tsx` chỉ giữ: state management, data fetching, và compose layout.

---

### ❌ VĐ-Q2: index.css monolith quá lớn (2439 dòng)

**Mức độ:** 🟡 Trung bình  
**Khu vực:** `src/frontend/src/index.css`  
**Mô tả:** File 2439 dòng / 46KB. Đã bắt đầu tách `dashboard.css` và `layout-shell.css`, nhưng phần lớn styles vẫn nằm trong index.css.

**Thay đổi yêu cầu:**  
Tách thành các file CSS theo component/concern:

| File mới | Nội dung tách | Dòng ước tính |
|----------|--------------|:------------:|
| `src/frontend/src/styles/tokens.css` | `:root` variables, dark mode tokens, reset | ~80 |
| `src/frontend/src/styles/components.css` | `.btn`, `.card`, `.pill`, `.alert`, `.field`, `.stat-card`, `.stat-grid` | ~400 |
| `src/frontend/src/styles/tables.css` | `.data-table`, scroll indicator, cells | ~200 |
| `src/frontend/src/styles/forms.css` | `.form-grid`, `.filters-grid`, `.modal` | ~200 |
| `src/frontend/src/styles/utilities.css` | `.muted`, `.empty-state`, responsive media queries | ~200 |

`index.css` chỉ import tất cả: `@import './styles/tokens.css';` etc.  

> **Kỹ thuật:** Dùng CSS native `@import` hoặc Vite tự bundle. Không cần thay đổi build config — Vite hỗ trợ CSS imports natively.

---

### ❌ VĐ-Q3: Skeleton loaders thay "Đang tải..."

**Mức độ:** 🟡 Trung bình  
**Khu vực:** Tất cả loading states  
**Mô tả:** Hiện tại tất cả loading states dùng text thuần "Đang tải..." — không giữ được layout structure và gây "flash" khi data load xong.

**File cần sửa:** Tạo component mới + update pages

**Thay đổi yêu cầu:**

1. Tạo `src/frontend/src/components/Skeleton.tsx`:
```tsx
type SkeletonProps = {
  width?: string
  height?: string
  borderRadius?: string
  count?: number
}

export default function Skeleton({
  width = '100%',
  height = '1rem',
  borderRadius = '8px',
  count = 1,
}: SkeletonProps) {
  return (
    <>
      {Array.from({ length: count }, (_, index) => (
        <div
          key={index}
          className="skeleton"
          style={{ width, height, borderRadius }}
          aria-hidden="true"
        />
      ))}
    </>
  )
}
```

2. Thêm CSS animation vào `index.css` (hoặc file tách ra):
```css
.skeleton {
  background: linear-gradient(
    90deg,
    var(--color-border) 25%,
    color-mix(in srgb, var(--color-border) 60%, var(--color-surface)) 50%,
    var(--color-border) 75%
  );
  background-size: 200% 100%;
  animation: skeleton-shimmer 1.5s ease infinite;
}

@keyframes skeleton-shimmer {
  0% { background-position: 200% 0; }
  100% { background-position: -200% 0; }
}
```

3. Tạo `src/frontend/src/components/StatCardSkeleton.tsx`:
```tsx
import Skeleton from './Skeleton'

export default function StatCardSkeleton({ count = 4 }: { count?: number }) {
  return (
    <div className="stat-grid">
      {Array.from({ length: count }, (_, i) => (
        <div className="stat-card" key={i}>
          <Skeleton width="60%" height="0.875rem" />
          <Skeleton width="45%" height="1.5rem" />
          <Skeleton width="40%" height="0.75rem" />
        </div>
      ))}
    </div>
  )
}
```

4. Thay thế `"Đang tải..."` trong `DashboardPage.tsx`, `CustomerListSection.tsx`, `ReportsPage.tsx` bằng skeleton components tương ứng.

---

### ❌ VĐ-Q4: Serilog log rotation thiếu cấu hình

**Mức độ:** 🟡 Trung bình  
**Khu vực:** `src/backend/Api/appsettings.json`, `appsettings.Production.json`

**Thay đổi yêu cầu:**  
Thêm rolling policy cho Serilog file sink:

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "/var/lib/congno/logs/api.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "fileSizeLimitBytes": 52428800,
          "rollOnFileSizeLimit": true,
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  }
}
```

---

# 🔒 PHẦN 3 — SECURITY (Bảo mật)

### ❌ VĐ-S1: Frontend thiếu CSP meta tag

**Mức độ:** 🟡 Trung bình  
**Khu vực:** `src/frontend/index.html`  
**Mô tả:** Backend SecurityHeadersMiddleware đặt `Content-Security-Policy` cho API responses, nhưng frontend SPA (served bởi Vite/Nginx) không có CSP. Điều này để mở XSS injection qua inline scripts.

**Thay đổi yêu cầu:**  
Thêm CSP meta tag vào `src/frontend/index.html`:

```html
<head>
  <!-- Thêm dòng sau -->
  <meta http-equiv="Content-Security-Policy"
        content="default-src 'self';
                 script-src 'self';
                 style-src 'self' 'unsafe-inline' https://fonts.googleapis.com;
                 font-src 'self' https://fonts.gstatic.com;
                 img-src 'self' data: blob:;
                 connect-src 'self' ${VITE_API_BASE_URL};
                 frame-ancestors 'none';
                 base-uri 'self';
                 form-action 'self';">
</head>
```

> **Lưu ý:** Nếu dùng Vite, `'unsafe-inline'` cho style-src cần thiết vì Vite inject styles inline trong dev mode. Trong production build, có thể thắt chặt bằng nonce-based CSP nếu cần.

---

### ❌ VĐ-S2: Thiếu Rate Limiting cho API endpoints nhạy cảm

**Mức độ:** 🟡 Trung bình  
**Khu vực:** `src/backend/Api/Program.cs`  
**Mô tả:** Hiện chỉ có rate limiting cho `/auth/login` (10/min) và `/auth/refresh` (30/5min). Các endpoints nhạy cảm khác (import commit, backup create, user create) chưa có rate limiting.

**Thay đổi yêu cầu:**  
Thêm rate limiting policies trong `Program.cs`:

```csharp
// Thêm vào phần AddRateLimiter
options.AddFixedWindowLimiter("MutationRateLimit", opt =>
{
    opt.PermitLimit = 60;
    opt.Window = TimeSpan.FromMinutes(1);
    opt.QueueLimit = 0;
    opt.AutoReplenishment = true;
});

options.AddFixedWindowLimiter("ExportRateLimit", opt =>
{
    opt.PermitLimit = 10;
    opt.Window = TimeSpan.FromMinutes(1);
    opt.QueueLimit = 0;
    opt.AutoReplenishment = true;
});
```

Áp dụng cho các endpoints:
- `ImportEndpoints.cs`: `.RequireRateLimiting("MutationRateLimit")` cho commit/rollback
- `BackupEndpoints.cs`: `.RequireRateLimiting("MutationRateLimit")` cho create/restore
- `ReportEndpoints.cs`: `.RequireRateLimiting("ExportRateLimit")` cho export PDF/Excel
- `AdminEndpoints.cs`: `.RequireRateLimiting("MutationRateLimit")` cho user create/update

---

### ❌ VĐ-S3: Password policy chưa enforce complexity

**Mức độ:** 🟡 Trung bình  
**Khu vực:** `src/backend/Application/Auth/`  
**Mô tả:** Admin tạo user/đổi mật khẩu, nhưng chưa thấy password complexity validation (min length, uppercase, number, special char).

**Thay đổi yêu cầu:**  
Thêm password validation helper:

```csharp
// Tạo file: src/backend/Application/Auth/PasswordPolicy.cs
public static class PasswordPolicy
{
    public const int MinLength = 8;
    
    public static (bool IsValid, string? Error) Validate(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return (false, "Mật khẩu không được để trống.");
        if (password.Length < MinLength)
            return (false, $"Mật khẩu tối thiểu {MinLength} ký tự.");
        if (!password.Any(char.IsUpper))
            return (false, "Mật khẩu phải có ít nhất 1 chữ hoa.");
        if (!password.Any(char.IsDigit))
            return (false, "Mật khẩu phải có ít nhất 1 chữ số.");
        return (true, null);
    }
}
```

Gọi `PasswordPolicy.Validate()` trong `AuthService` tại các điểm: user create, password change, password reset.

---

# ✨ PHẦN 4 — UX POLISH (Trải nghiệm)

### ❌ VĐ-U1: Empty state thiếu illustration

**Mức độ:** 🟢 Thấp  
**Khu vực:** Các trang trống / empty states  
**Mô tả:** Empty states chỉ hiện text "Không có dữ liệu" — thiếu visual interest, không hướng dẫn user hành động tiếp theo.

**File cần sửa:** `src/frontend/src/index.css` + các pages

**Thay đổi yêu cầu:**  
Cải thiện `.empty-state` class:

```css
/* TRƯỚC */
.empty-state {
  /* chỉ có text styling cơ bản */
}

/* SAU */
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.75rem;
  padding: var(--space-5) var(--space-4);
  text-align: center;
  color: var(--color-muted);
}

.empty-state__icon {
  font-size: 2.5rem;
  opacity: 0.4;
}

.empty-state__title {
  font-weight: 600;
  font-size: 1rem;
  color: var(--color-ink);
}

.empty-state__description {
  max-width: 32ch;
  font-size: 0.875rem;
}

.empty-state__action {
  margin-top: var(--space-2);
}
```

Trong các trang, thay đổi empty states:
```tsx
/* TRƯỚC */
<div className="empty-state">Không có dữ liệu.</div>

/* SAU */
<div className="empty-state">
  <span className="empty-state__icon">📊</span>
  <div className="empty-state__title">Chưa có dữ liệu</div>
  <div className="empty-state__description">
    Dữ liệu sẽ hiển thị sau khi nhập liệu hoặc thay đổi bộ lọc.
  </div>
</div>
```

---

### ❌ VĐ-U2: Chart tooltip và hover interactivity

**Mức độ:** 🟢 Thấp  
**Khu vực:** Dashboard charts (Cashflow, Overdue)  
**Mô tả:** Bar charts hiện tại chỉ có `title` attribute cho tooltip — browser default, bất tiện trên mobile, không đẹp.

**File cần sửa:** `src/frontend/src/pages/DashboardPage.tsx`, `src/frontend/src/pages/dashboard/dashboard.css`

**Thay đổi yêu cầu:**  
1. Tạo custom tooltip component hiện khi hover vào bar:

```tsx
// Trong DashboardPage.tsx (hoặc component chart mới)
const [tooltip, setTooltip] = useState<{ x: number; y: number; content: string } | null>(null)

// Trên mỗi bar, thay title bằng mouse events:
<div
  className="cashflow-chart__bar cashflow-chart__bar--expected"
  style={{ height: `${expectedHeight}%` }}
  onMouseEnter={(e) => setTooltip({
    x: e.clientX,
    y: e.clientY,
    content: `${point.fullLabel}\nExpected: ${formatUnitValue(point.expected, unit)}`
  })}
  onMouseLeave={() => setTooltip(null)}
/>
```

2. CSS cho tooltip:
```css
.chart-tooltip {
  position: fixed;
  z-index: 50;
  padding: 0.5rem 0.75rem;
  background: var(--color-ink);
  color: #fff;
  border-radius: 8px;
  font-size: 0.8rem;
  pointer-events: none;
  white-space: pre-line;
  transform: translate(-50%, -120%);
  box-shadow: 0 4px 12px rgba(0,0,0,0.2);
}
```

---

### ❌ VĐ-U3: Table row height quá cao trên desktop

**Mức độ:** 🟢 Thấp  
**Khu vực:** Tất cả DataTable instances  
**Mô tả:** Table row padding tạo hàng cao, hiển thị ít data/page trên FHD.

**File cần sửa:** `src/frontend/src/index.css` (hoặc `tables.css` sau khi tách)

**Thay đổi yêu cầu:**
```css
/* Tìm .data-table td, .data-table th padding và giảm */
/* TRƯỚC */
.data-table td { padding: 0.6rem 0.75rem; }
.data-table th { padding: 0.55rem 0.75rem; }

/* SAU */
.data-table td { padding: 0.45rem 0.75rem; }
.data-table th { padding: 0.45rem 0.75rem; }
```

---

### ❌ VĐ-U4: Nav sidebar thiếu collapse-to-icons mode

**Mức độ:** 🟢 Thấp  
**Khu vực:** `src/frontend/src/styles/layout-shell.css`, `src/frontend/src/layouts/AppShell.tsx`  
**Mô tả:** Sidebar luôn full-width 240px (sau VĐ-D4). Trên screens 1280-1440px, có thể collapse sang icon-only mode (~60px) để tăng content area.

**Thay đổi yêu cầu:**  
1. Thêm state `isNavCollapsed` persist vào `localStorage`
2. Khi collapsed:
```css
.app-shell--nav-collapsed {
  grid-template-columns: 60px 1fr;
}

.app-shell--nav-collapsed .app-nav {
  width: 60px;
  padding: 1rem 0.5rem;
  align-items: center;
}

.app-shell--nav-collapsed .brand__title,
.app-shell--nav-collapsed .brand__kicker,
.app-shell--nav-collapsed .brand__tag,
.app-shell--nav-collapsed .brand-meta,
.app-shell--nav-collapsed .user-chip__name,
.app-shell--nav-collapsed .user-chip__role,
.app-shell--nav-collapsed .nav-pill {
  display: none;
}

.app-shell--nav-collapsed .nav-item {
  justify-content: center;
  padding: 0.65rem;
}
```

3. Thêm toggle button ở bottom sidebar:
```tsx
<button
  className="btn btn-ghost nav-collapse-toggle"
  onClick={() => setNavCollapsed(prev => !prev)}
  title={isNavCollapsed ? 'Mở rộng menu' : 'Thu gọn menu'}
>
  {isNavCollapsed ? '»' : '«'}
</button>
```

---

# 📊 BẢNG TỔNG HỢP 16 VẤN ĐỀ — Sắp xếp theo ưu tiên

### 🔴 Ưu tiên CAO — NÊN LÀM NGAY

| # | Mã VĐ | Vấn đề | File cần sửa | Hành động tóm tắt | Effort |
|:-:|:-----:|--------|:------------:|---------|:------:|
| 1 | VĐ-D1 | Base font 16px quá lớn | `index.css` `:root` | Giảm `--font-size-body` xuống `0.9375rem` (15px), giảm h1-h3 | 0.5h |
| 2 | VĐ-D2 | Spacing tokens quá rộng | `index.css` `:root` | Giảm space-1→space-7 theo bảng | 0.5h |
| 3 | VĐ-D3 | Stat values quá to, radius quá tròn | `index.css` | Giảm `.stat-card__value` 1.35rem, radius 12px, minmax 180px | 1h |
| 4 | VĐ-D4 | Content padding rộng, sidebar to | `layout-shell.css` | Sidebar 260→240px | 0.5h |
| 5 | VĐ-D5 | Card radius quá generous | `index.css` | `.card` radius 20→14px, `.card-hero` 20→14px | 0.5h |

### 🟡 Ưu tiên TRUNG BÌNH — SPRINT KẾ TIẾP

| # | Mã VĐ | Vấn đề | File cần sửa | Hành động tóm tắt | Effort |
|:-:|:-----:|--------|:------------:|---------|:------:|
| 6 | VĐ-Q1 | DashboardPage.tsx 994 dòng | `DashboardPage.tsx` | Tách 5 sub-components | 4h |
| 7 | VĐ-Q2 | index.css 2439 dòng | `index.css` | Tách thành 5 file CSS | 3h |
| 8 | VĐ-Q3 | Skeleton loaders | Tạo mới + pages | Component `Skeleton.tsx` + `StatCardSkeleton.tsx` | 3h |
| 9 | VĐ-Q4 | Serilog log rotation | `appsettings*.json` | Rolling policy Day, 30 files, 50MB limit | 0.5h |
| 10 | VĐ-S1 | Frontend CSP | `index.html` | Thêm CSP meta tag | 0.5h |
| 11 | VĐ-S2 | Rate limiting API | `Program.cs`, endpoints | MutationRateLimit 60/min, ExportRateLimit 10/min | 2h |
| 12 | VĐ-S3 | Password policy | Tạo `PasswordPolicy.cs` | Min 8, uppercase, digit validation | 1h |

### 🟢 Ưu tiên THẤP — KHI CÓ THỜI GIAN

| # | Mã VĐ | Vấn đề | File cần sửa | Hành động tóm tắt | Effort |
|:-:|:-----:|--------|:------------:|---------|:------:|
| 13 | VĐ-U1 | Empty state cần illustration | `index.css`, pages | CSS `.empty-state` + icon/title/description/action | 2h |
| 14 | VĐ-U2 | Chart tooltip đẹp hơn | `DashboardPage.tsx`, CSS | Custom tooltip component thay title attribute | 3h |
| 15 | VĐ-U3 | Table row height giảm | `index.css` | Table td/th padding 0.6→0.45rem | 0.5h |
| 16 | VĐ-U4 | Nav sidebar collapse-to-icons | `layout-shell.css`, `AppShell.tsx` | Collapsed mode 60px, persist localStorage | 4h |

---

## Tóm tắt Review Vòng 5

> **Score hiện tại: 8.5/10**  
> **Score mục tiêu sau fix: 9.0/10**
> 
> **Trọng tâm chính:**  
> 1. **UI Density** (VĐ-D1→D5): Giải quyết vấn đề #1 từ user — zoom 85-90% trên FHD. ~3 giờ effort.
> 2. **Code Quality** (VĐ-Q1→Q4): Reduce technical debt, improve DX. ~10.5 giờ effort.
> 3. **Security** (VĐ-S1→S3): Hardening trước production. ~3.5 giờ effort.
> 4. **UX Polish** (VĐ-U1→U4): Professional touches. ~9.5 giờ effort.
> 
> **Tổng effort ước tính: ~27 giờ** (3-4 ngày developer)
> 
> **Lưu ý cho Codex:**
> - VĐ-D1 và VĐ-D2 ảnh hưởng TOÀN BỘ UI — cần test visual regression sau khi thay đổi
> - VĐ-Q1 và VĐ-Q2 là refactor — phải đảm bảo không break imports
> - VĐ-S1 CSP có thể block fonts/API nếu sai — test kỹ sau khi thêm
> - VĐ-D3 stat-grid minmax thay đổi sẽ tác động layout responsive — kiểm tra breakpoints 480px, 768px, 1024px
