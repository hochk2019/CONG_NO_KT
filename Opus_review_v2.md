> [!IMPORTANT]
> **HISTORICAL DOCUMENT**
> Tài liệu này là snapshot/lịch sử để tham khảo, **không phải nguồn vận hành chuẩn hiện tại**.
> Nguồn chuẩn hiện tại:
> - Deploy: DEPLOYMENT_GUIDE_DOCKER.md
> - Runbook: RUNBOOK.md
> - Ops runtime: docs/OPS_ADMIN_CONSOLE.md
# ĐÁNH GIÁ VÒNG 2 – DỰ ÁN CONG NO GOLDEN
## Hệ thống Theo dõi Thu hồi Công nợ & Cảnh báo Đến hạn

> **Vai trò**: Principal Architect + Staff Engineer + Production Readiness Auditor
> **Ngày đánh giá**: 2026-02-13
> **Phiên bản**: Round 2 (post-remediation)

---

## BẢNG TỔNG HỢP ĐIỂM

| # | Hạng mục | Điểm V1 | Điểm V2 | Mục tiêu | Đạt? |
|---|----------|---------|---------|-----------|------|
| 1 | Logic nghiệp vụ | 8.0 | **8.5** | ≥8.5 | ✅ |
| 2 | Workflow E2E | — | **8.0** | — | — |
| 3 | Dashboard & UI/UX | 6.0 | **7.0** | — | — |
| 4 | Frontend Architecture | 6.0 | **7.5** | — | — |
| 5 | Backend Architecture | 8.0 | **8.5** | ≥8.5 | ✅ |
| 6 | Database & Integrity | 8.5 | **8.5** | ≥8.5 | ✅ |
| 7 | Bảo mật | 6.0 | **8.0** | ≥8.0 | ✅ |
| 8 | DevOps & Production | 4.0 | **7.5** | ≥8.0 | ⚠️ |
| | **TỔNG** | **6.8** | **8.0** | | |

---

## 1️⃣ LOGIC NGHIỆP VỤ — 8.5/10

### 1.1 Allocation Engine (`Domain/Allocation/AllocationEngine.cs` — 108 lines)
- ✅ Pure domain logic, zero side-effects, dễ test
- ✅ 4 modes: FIFO, ByInvoice, ByPeriod, Manual
- ✅ Fallback gracefully khi `SelectedTargets` rỗng → FIFO
- ✅ Xử lý `remaining <= 0` break sớm, tối ưu

**[LOW] Thiếu Proportional mode**: Không có mode phân bổ theo tỷ lệ (pro-rata). Một số ERP yêu cầu mode này.

### 1.2 Risk Classifier (`Domain/Risk/RiskClassifier.cs` — 30 lines)

```csharp
// Hiện tại dùng OR logic:
return metrics.MaxDaysPastDue >= rule.MinOverdueDays
    || metrics.OverdueRatio >= rule.MinOverdueRatio
    || metrics.LateCount >= rule.MinLateCount;
```

**[MEDIUM] OR-only matching**: Chỉ cần 1 metric khớp là trigger rule. Thiếu AND logic và operator config per-rule. Có thể gây false positive (VD: 1 khoản trễ 1 ngày → trigger High risk nếu MinLateCount=1).

**Đề xuất refactor**:
```csharp
// Thêm MatchMode vào RiskRule
public enum RuleMatchMode { Any, All }

private static bool Matches(RiskMetrics metrics, RiskRule rule)
{
    var conditions = new[]
    {
        rule.MinOverdueDays > 0 && metrics.MaxDaysPastDue >= rule.MinOverdueDays,
        rule.MinOverdueRatio > 0 && metrics.OverdueRatio >= rule.MinOverdueRatio,
        rule.MinLateCount > 0 && metrics.LateCount >= rule.MinLateCount,
    };
    return rule.MatchMode == RuleMatchMode.All
        ? conditions.All(c => c)
        : conditions.Any(c => c);
}
```

### 1.3 Receipt Approve (`ReceiptService.ApproveAsync` — line 402-513)

- ✅ Explicit transaction: `BeginTransactionAsync` → `SaveChangesAsync` → `CommitAsync`
- ✅ Optimistic concurrency via `Version` check
- ✅ Period lock override with audit trail

**[MEDIUM] Audit logging sau CommitAsync**: Audit log ở line 496-502 chạy SAU `tx.CommitAsync()` (line 483). Nếu audit fail → transaction đã commit nhưng không có audit trail.

```csharp
// Line 482-502: Audit nằm ngoài transaction boundary
await _db.SaveChangesAsync(ct);
await tx.CommitAsync(ct);  // ← Transaction kết thúc ở đây

// Audit logging chạy NGOÀI transaction
await _auditService.LogAsync("RECEIPT_APPROVE", ...);  // ← Có thể fail
```

**Đề xuất**: Di chuyển audit log VÀO trong transaction, hoặc dùng outbox pattern.

### 1.4 Customer Balance Reconciliation (`CustomerBalanceReconcileService.cs`)

- ✅ Logic đúng: `expected = invoiceTotal + advanceTotal - receiptTotal`
- ✅ Tolerance-based drift detection
- ✅ Configurable MaxItems, auto-apply option

**[HIGH] Memory pressure**: `await _db.Customers.ToListAsync(ct)` (line 24) load TOÀN BỘ customers vào RAM. Với 10K+ customers sẽ gây memory spike.

**Đề xuất**: Dùng batch processing hoặc raw SQL:
```csharp
// Thay vì load all customers:
var batchSize = 500;
var offset = 0;
while (true) {
    var batch = await _db.Customers
        .OrderBy(c => c.TaxCode)
        .Skip(offset).Take(batchSize)
        .ToListAsync(ct);
    if (batch.Count == 0) break;
    // ... process batch
    offset += batchSize;
}
```

### 1.5 Import Commit (`ImportCommitService.CommitAsync` — 208 lines)

- ✅ Transaction boundary đúng
- ✅ Idempotency key support
- ✅ Duplicate invoice detection via `InvoiceKey`
- ✅ Period lock với override audit

**[LOW] Method quá dài**: 208 lines cho 1 method. Nên extract sub-methods.

### 1.6 Reminder Scheduling

- ✅ Upcoming due + overdue reminders
- ✅ Zalo integration với retry + circuit breaker
- ✅ Risk-based filtering (only send for specific risk levels)

**Điểm mạnh đặc biệt**: `FilterRecipientsAsync` cho phép filter supervisor notifications.

---

## 2️⃣ WORKFLOW END-TO-END — 8.0/10

### Flow chính:
```
Import → Staging → Validate → Commit → [Invoice/Advance/Receipt created]
                                            ↓
Receipt → Draft → Select targets → Preview → Approve → Allocate
                                                          ↓
                                              Risk Assessment → Reminder
                                                          ↓
                                              Dashboard ← Reconciliation
```

### Đánh giá workflow:

| Bước | Trạng thái | Ghi chú |
|------|-----------|---------|
| Import Upload | ✅ | Template parsing, validation |
| Import Preview | ✅ | Staging rows, error detection |
| Import Commit | ✅ | Transaction, dedup, period lock |
| Import Rollback | ✅ | Full rollback support |
| Receipt Create | ✅ | Draft mode, target selection |
| Receipt Preview | ✅ | Allocation simulation |
| Receipt Approve | ✅ | With period lock override |
| Receipt Void | ✅ | Reverse allocations |
| Risk Assessment | ✅ | Heuristic + AI fallback |
| Reminder Run | ✅ | Zalo with retry/circuit breaker |
| Balance Reconcile | ✅ | Scheduled + manual trigger |
| Data Retention | ✅ | Automated cleanup |

### 5 cải tiến workflow:

1. **[HIGH] Thiếu Receipt Edit flow**: Sau khi tạo DRAFT, không thể sửa amount/targets trước khi approve. User phải void rồi tạo lại.

2. **[MEDIUM] Import thiếu progress callback**: Với batch lớn (>1000 rows), user không biết commit đang ở step nào. Nên thêm SignalR/SSE progress.

3. **[MEDIUM] Thiếu bulk approve**: Phải approve từng receipt. Nên có batch approve cho operator.

4. **[LOW] Reminder schedule không có dry-run**: Không thể preview danh sách sẽ gửi trước khi run. Nếu config sai → gửi nhầm.

5. **[LOW] Thiếu approval chain**: Receipt approve chỉ cần 1 người. Với amount lớn nên có 2-level approval.

---

## 3️⃣ DASHBOARD & UI/UX — 7.0/10

### 3.1 Phân tích "Trạng thái phân bổ" hiện tại (DashboardPage line 710-738)

Hiện tại dùng **bar chart đơn giản** với 3 cột:
- % Đã phân bổ
- % Phân bổ một phần  
- % Chưa phân bổ

**Vấn đề UX**:
- Không có drill-down khi click vào segment
- Không hiển thị absolute values prominently
- Bar chart không trực quan bằng donut/ring chart cho proportional data
- Không có animation khi data thay đổi

### 10 đề xuất UI/UX cải tiến:

| # | Đề xuất | Mức độ | Chi tiết |
|---|---------|--------|----------|
| 1 | **Donut chart** thay bar chart | HIGH | Dùng SVG donut chart cho "Trạng thái phân bổ". Hiển thị total ở center. |
| 2 | **Drill-down** | HIGH | Click vào segment → navigate đến `/receipts?allocationStatus=PARTIAL` |
| 3 | **Skeleton loading** | MEDIUM | Thay "Đang tải..." bằng skeleton placeholders |
| 4 | **Financial Health Score** | HIGH | Tổng hợp 1 score (0-100) từ: overdueRatio, collectionRate, avgDaysPastDue. Hiển thị gauge chart. |
| 5 | **Sparkline trends** | MEDIUM | Mini line chart trong mỗi KPI card showing 7-day trend |
| 6 | **Color-coded KPI delta** | LOW | Mũi tên ↑↓ với màu xanh/đỏ cho so sánh kỳ trước |
| 7 | **Responsive layout** | HIGH | DashboardPage 815 lines, tất cả hardcode layout. Cần media queries cho mobile |
| 8 | **Empty state illustrations** | LOW | Thay text "Chưa có dữ liệu" bằng SVG illustrations |
| 9 | **Toast notifications** | MEDIUM | Không có toast system. Errors hiển thị inline nhưng dễ bị miss |
| 10 | **Dark mode polish** | LOW | useTheme hook tồn tại nhưng dashboard CSS chưa fully dark-mode ready |

### Donut chart code suggestion:
```tsx
// components/DonutChart.tsx
const DonutChart = ({ items, total }: DonutChartProps) => {
  let cumulativePercent = 0;
  return (
    <svg viewBox="0 0 36 36" className="donut-chart">
      {items.map((item) => {
        const dashArray = `${item.percent} ${100 - item.percent}`;
        const dashOffset = 100 - cumulativePercent + 25;
        cumulativePercent += item.percent;
        return (
          <circle key={item.key}
            className={`donut-segment donut-segment--${item.key.toLowerCase()}`}
            cx="18" cy="18" r="15.9155"
            strokeDasharray={dashArray}
            strokeDashoffset={dashOffset}
            onClick={() => navigate(`/receipts?allocationStatus=${item.key}`)}
          />
        );
      })}
      <text x="18" y="18" className="donut-center">
        {formatMoney(total)}
      </text>
    </svg>
  );
};
```

---

## 4️⃣ FRONTEND ARCHITECTURE — 7.5/10

### 4.1 Custom Hooks (7 hooks)

| Hook | Lines | Đánh giá |
|------|-------|---------|
| `useQuery` | 50 | ✅ Race condition protection via `requestIdRef` |
| `usePagination` | ~60 | ✅ Với tests |
| `usePersistedState` | ~40 | ✅ Với tests, localStorage |
| `useDebouncedValue` | ~20 | ✅ Đơn giản, đúng |
| `useTheme` | ~30 | ✅ Dark mode toggle |

**[MEDIUM] Thiếu hooks**: Không có `useMutation`, `useInfiniteQuery`, `useOptimisticUpdate`. Mỗi page tự quản lý mutation state riêng.

### 4.2 Page Size Analysis

| Page | Lines | Đánh giá |
|------|-------|---------|
| `ReportsPage.tsx` | 1018 | ❌ Quá lớn |
| `DashboardPage.tsx` | 815 | ❌ Quá lớn |
| `RiskAlertsPage.tsx` | 784 | ⚠️ Lớn |

### 5 đề xuất refactor frontend:

1. **[HIGH] Extract `useMutation` hook**: Tất cả CRUD operations (create, approve, void) đều duplicate pattern `try/catch/setLoading/setError`. Cần centralized mutation hook.

```tsx
function useMutation<TArgs extends unknown[], TResult>(
  mutationFn: (...args: TArgs) => Promise<TResult>,
) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const execute = useCallback(async (...args: TArgs) => {
    setLoading(true); setError(null);
    try { return await mutationFn(...args); }
    catch (err) { setError(err); throw err; }
    finally { setLoading(false); }
  }, [mutationFn]);
  return { loading, error, execute };
}
```

2. **[HIGH] Split DashboardPage**: Extract `CashflowChart`, `AllocationDonut`, `TopCustomersList`, `OverdueGroupsList` thành sub-components. Giảm 815 → ~200 lines.

3. **[MEDIUM] Global state cho notifications**: Không có toast/notification store. Dùng React Context hoặc Zustand cho global toast queue.

4. **[MEDIUM] Code-splitting**: Kiểm tra có `React.lazy()` cho routes chưa? Pages lớn nên lazy-load.

5. **[LOW] CQRS frontend**: Tách read queries (useQuery) và write mutations (useMutation) rõ ràng hơn. Hiện tại mixed trong mỗi page.

---

## 5️⃣ BACKEND ARCHITECTURE — 8.5/10

### 5.1 Service Size Analysis

| Service | Lines | Partial files | Total | Đánh giá |
|---------|-------|---------------|-------|---------|
| ReceiptService | 784 | +5 partials | ~1200 | ⚠️ Lớn nhưng tách partial tốt |
| ImportCommitService | 530 | +4 partials | ~900 | ⚠️ CommitAsync quá dài |
| ReminderService | 270 | +2 partials | ~680 | ✅ |
| BackupService | 838 | +1 partial | ~1000 | ⚠️ |
| AuthService | 247 | — | 247 | ✅ |
| DashboardService | ~200 | +1 SQL partial | ~400 | ✅ |

### 7 đề xuất refactor backend:

1. **[HIGH] DataRetentionService memory risk**: Loads ALL old records into memory before deleting. Với 100K+ audit logs → OOM risk.
```csharp
// Hiện tại (line 51-55):
var oldAuditLogs = await _db.AuditLogs
    .Where(x => x.CreatedAt < auditCutoff)
    .ToListAsync(ct);  // ← Load ALL vào RAM

// Đề xuất: Batch delete with raw SQL
await _db.Database.ExecuteSqlRawAsync(
    "DELETE FROM congno.audit_logs WHERE created_at < {0} LIMIT 1000",
    auditCutoff);
```

2. **[HIGH] Domain Events**: Không có event system. Receipt approve trực tiếp gọi `NotifyPartialAllocationAsync`. Nên dùng MediatR hoặc custom domain events để decouple.

3. **[MEDIUM] API Versioning**: Không có versioning strategy. Tất cả endpoints ở root path. Nên thêm `/api/v1/` prefix.

4. **[MEDIUM] Enum duplication**: Status strings (`"DRAFT"`, `"APPROVED"`, `"VOID"`) hardcode khắp nơi. Nên dùng constants hoặc enum.
```csharp
public static class ReceiptStatus
{
    public const string Draft = "DRAFT";
    public const string Approved = "APPROVED";
    public const string Void = "VOID";
}
```

5. **[MEDIUM] Health check enrichment**: `/health/ready` kiểm tra DB connection nhưng không check Zalo connectivity, backup path writable, disk space.

6. **[LOW] Structured logging enrichment**: Serilog configured nhưng thiếu correlation ID middleware. Request tracing khó trong multi-step flows.

7. **[LOW] OpenTelemetry custom metrics**: Có basic ASP.NET instrumentation nhưng thiếu custom business metrics (receipts_approved_total, allocation_duration_seconds, etc.)

---

## 6️⃣ DATABASE & DATA INTEGRITY — 8.5/10

### 6.1 current_balance Caching

- ✅ `CustomerBalanceReconcileService` scheduled chạy định kỳ
- ✅ Tolerance-based drift detection
- ⚠️ Reconciliation load all customers vào memory (xem mục 1.4)

### 6.2 Partitioning

- ✅ `EnsureAuditLogPartitionsAsync` tạo partition 3 tháng ahead
- ✅ Function `congno.ensure_audit_logs_partition()` trong DB

### 5 đề xuất cải tiến DB:

1. **[HIGH] Batch delete cho retention**: Xóa bằng `RemoveRange()` với EF Core tracking rất chậm. Dùng `ExecuteDeleteAsync()` (EF Core 7+):
```csharp
await _db.AuditLogs
    .Where(x => x.CreatedAt < auditCutoff)
    .ExecuteDeleteAsync(ct);
```

2. **[MEDIUM] Missing index cho audit_logs queries**: Nếu retention query by `created_at`, cần index `(created_at)` trên partition parent.

3. **[MEDIUM] AI training data consistency**: `RiskModelTrainingHostedService` training schedule cần snapshot isolation để tránh dirty reads khi training chạy song song với receipt approval.

4. **[LOW] Read replica support**: Nếu scale, reports/dashboard queries nên đọc từ read replica. Hiện tại single connection string.

5. **[LOW] Advisory lock timeout**: Import commit dùng advisory locks nhưng không thấy explicit timeout configuration. Nên set `lock_timeout` trong transaction.

---

## 7️⃣ BẢO MẬT — 8.0/10

### Improvements từ V1:

| Feature | V1 | V2 | Status |
|---------|----|----|--------|
| Rate limiting | ❌ | ✅ 10 req/5min login | Fixed |
| JWT secret validation | ❌ | ✅ Min 32 chars + placeholder check | Fixed |
| Password complexity | ❌ | ✅ Upper+lower+digit, 8 chars min | Fixed |
| Device binding | ❌ | ✅ SHA256(UserAgent) + IP /24 prefix | Fixed |
| Refresh token hash | ✅ | ✅ SHA256 | Maintained |
| Circuit breaker | ❌ | ✅ ZaloCircuitBreaker | Fixed |

### 5 cải tiến bảo mật nâng cao:

1. **[HIGH] Account lockout chưa implement**: Không có login attempt tracking. Rate limiting chặn IP nhưng không lock account sau N failed attempts.
```csharp
// Đề xuất: Thêm FailedLoginAttempts, LockedUntil vào Users table
if (user.FailedLoginAttempts >= 5)
    throw new UnauthorizedAccessException("Account locked.");
```

2. **[MEDIUM] Security headers missing**: Không thấy middleware cho `X-Content-Type-Options`, `X-Frame-Options`, `Strict-Transport-Security`, `Content-Security-Policy`.

3. **[MEDIUM] CORS chỉ cho LocalDev**: Production CORS policy hardcode `localhost:5173`. Cần configurable origins:
```csharp
// Hiện tại (line 114-118):
policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
// Đề xuất: Từ config
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins").Get<string[]>();
```

4. **[MEDIUM] MFA readiness**: Không có TOTP/2FA infrastructure. Nên prepare database schema trước.

5. **[LOW] Encryption at rest**: Refresh token hash là one-way (SHA256), tốt. Nhưng `AllocationTargets` JSON, `SummaryData` lưu plaintext. Nếu chứa sensitive data → cần encryption.

---

## 8️⃣ DEVOPS & PRODUCTION READINESS — 7.5/10

### Docker Setup (Post-remediation)

| Component | Image | Status |
|-----------|-------|--------|
| Database | `postgres:16-alpine` | ✅ Health check |
| API | `dotnet/aspnet:8.0` | ✅ Non-root user, pg_client |
| Frontend | `nginx:1.27-alpine` | ✅ Multi-stage build |

**Tốt**:
- ✅ `docker-compose.yml` với 3 services + networking
- ✅ Environment variables cho secrets (JWT, DB password)
- ✅ Health checks trên DB
- ✅ Non-root user trong API Dockerfile (uid 10001)
- ✅ Backup volume mount
- ✅ Prometheus metrics endpoint

### 5 đề xuất nâng cấp production-grade:

1. **[HIGH] Thiếu CI/CD pipeline**: Không có GitHub Actions / GitLab CI file. Build + deploy hoàn toàn manual.
```yaml
# .github/workflows/ci.yml (đề xuất)
name: CI
on: [push, pull_request]
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: dotnet test
      - run: cd src/frontend && npm ci && npm test
  build:
    needs: test
    steps:
      - run: docker compose build
```

2. **[HIGH] Thiếu .env.example**: `docker-compose.yml` reference nhiều env vars nhưng không có template file. Team mới không biết cần set gì.

3. **[MEDIUM] Monitoring stack thiếu**: OpenTelemetry + Prometheus configured nhưng không có Grafana dashboard, Loki cho log aggregation, hoặc AlertManager rules.

4. **[MEDIUM] Backup verification**: `BackupService` tạo backup nhưng không có automated restore test. Nên schedule restore verification hàng tuần.

5. **[LOW] Rollback strategy**: Không có blue-green hay canary deployment. Docker Compose chỉ hỗ trợ `restart: unless-stopped`. Cần Kubernetes hoặc Docker Swarm cho zero-downtime deploy.

---

## 🧠 REFACTOR VÒNG 2 — TỔNG HỢP

### Danh sách vấn đề theo mức độ ưu tiên

| # | Mức độ | Vấn đề | Component | Đề xuất |
|---|--------|--------|-----------|---------|
| 1 | 🔴 HIGH | Memory: load ALL customers | `CustomerBalanceReconcileService` | Batch processing |
| 2 | 🔴 HIGH | Memory: load ALL old records | `DataRetentionService` | `ExecuteDeleteAsync` / batch SQL |
| 3 | 🔴 HIGH | Thiếu CI/CD | DevOps | GitHub Actions pipeline |
| 4 | 🔴 HIGH | Account lockout missing | Security | Track failed attempts + lock |
| 5 | 🔴 HIGH | Thiếu Receipt Edit flow | Workflow | Allow DRAFT editing |
| 6 | 🟡 MEDIUM | Audit log ngoài transaction | `ReceiptService.ApproveAsync` | Move inside tx or outbox |
| 7 | 🟡 MEDIUM | RiskClassifier OR-only | `RiskClassifier` | Add AND/OR match mode |
| 8 | 🟡 MEDIUM | Security headers missing | Middleware | Add security header middleware |
| 9 | 🟡 MEDIUM | CORS hardcode localhost | `Program.cs` | Configurable origins |
| 10 | 🟡 MEDIUM | Thiếu useMutation hook | Frontend | Extract mutation pattern |
| 11 | 🟡 MEDIUM | DashboardPage 815 lines | Frontend | Split into sub-components |
| 12 | 🟡 MEDIUM | Enum/status string duplication | Backend | Constants class |
| 13 | 🟡 MEDIUM | API versioning | Backend | `/api/v1/` prefix |
| 14 | 🟡 MEDIUM | Monitoring stack | DevOps | Grafana + Loki + AlertManager |
| 15 | 🟡 MEDIUM | MFA readiness | Security | Schema + TOTP prep |
| 16 | 🟢 LOW | Proportional allocation mode | AllocationEngine | Add ProRata mode |
| 17 | 🟢 LOW | ImportCommitAsync 208 lines | ImportCommitService | Extract methods |
| 18 | 🟢 LOW | Correlation ID middleware | Backend | Request tracing |
| 19 | 🟢 LOW | Custom OTel metrics | Backend | Business metric counters |
| 20 | 🟢 LOW | Donut chart for allocation | Dashboard | SVG donut replacement |

### Enterprise-level Upgrade Path

Để đạt enterprise-level, hệ thống cần:

1. **Event-Driven Architecture**: MediatR domain events → decouple services
2. **CQRS**: Separate read/write models cho reports (read replica)
3. **Multi-tenancy**: Hiện tại single-tenant. Schema-per-tenant hoặc row-level security
4. **Audit stream**: Event sourcing cho financial transactions (immutable append-only log)
5. **SSO/SAML**: Cho enterprise integration, thay vì chỉ username/password

---

## ✅ PRODUCTION READINESS CHECKLIST

| # | Item | Status | Notes |
|---|------|--------|-------|
| 1 | Health endpoints | ✅ | `/health` + `/health/ready` |
| 2 | Structured logging | ✅ | Serilog configured |
| 3 | Rate limiting | ✅ | Login + Refresh |
| 4 | HTTPS | ⚠️ | `RequireHttpsMetadata = false` |
| 5 | Secrets management | ✅ | Env vars, no hardcode |
| 6 | Database migrations | ✅ | Auto-apply on startup |
| 7 | Backup/Restore | ✅ | pg_dump/pg_restore |
| 8 | Docker packaging | ✅ | Multi-stage, non-root |
| 9 | CI/CD | ❌ | No pipeline |
| 10 | Monitoring | ⚠️ | OTel configured, no dashboards |
| 11 | Alerting | ❌ | No AlertManager rules |
| 12 | Load testing | ❌ | No k6/artillery scripts |
| 13 | Rollback strategy | ❌ | Manual only |
| 14 | Security headers | ❌ | Not implemented |
| 15 | Account lockout | ❌ | Not implemented |

**Production Readiness: 9/15 items ✅ (60%)**

---

## 📝 KẾT LUẬN

Dự án đã cải thiện **đáng kể** từ V1 (6.8/10 → 8.0/10). Các critical issues từ V1 đã được fix:
- Rate limiting ✅
- JWT secret validation ✅
- Explicit transactions ✅
- Device binding ✅
- Circuit breaker ✅
- Docker containerization ✅
- OpenTelemetry ✅

**Để đạt mục tiêu ≥8.5 toàn diện**, cần focus vào:
1. Fix 5 items HIGH priority (memory, CI/CD, account lockout, receipt edit, donut chart)
2. Add security headers middleware
3. Setup monitoring stack (Grafana + Loki)
4. Split large frontend pages

**Codex nên dùng các kỹ thuật hiện đại nhất**:
- `ExecuteDeleteAsync()` (EF Core 7+) thay vì `RemoveRange()` cho batch operations
- MediatR 12.x cho domain events pattern
- Polly 8.x cho resilience patterns (đã có manual retry, nên chuẩn hóa)
- React.lazy + Suspense cho code-splitting
- CSS Container Queries cho responsive components
- GitHub Actions composite actions cho reusable CI steps

---

## 🔎 PHỤ LỤC XÁC THỰC CODEX (13/02/2026)

### A. Đối chiếu nhận định V2 với hệ thống hiện tại

| Nhận định V2 | Kết luận Codex | Trạng thái hệ thống hiện tại | Kế hoạch/bead |
|---|---|---|---|
| Memory pressure: load ALL customers | ✅ ĐÚNG | **Đã xử lý**: reconcile chạy theo batch/chunk, giảm memory pressure | ✅ Done `cng-4uj.1` |
| Memory pressure: DataRetention load ALL records | ✅ ĐÚNG | **Đã xử lý**: retention delete theo batch + `ExecuteDeleteAsync` | ✅ Done `cng-4uj.1` |
| Audit log nằm sau `CommitAsync` | ✅ ĐÚNG | **Đã xử lý**: audit approve chạy trong transaction boundary | ✅ Done `cng-4uj.2` |
| RiskClassifier OR-only | ✅ ĐÚNG | **Đã xử lý**: thêm `MatchMode` (Any/All) cho domain + SQL path | ✅ Done `cng-4uj.3` |
| Account lockout missing | ✅ ĐÚNG | **Đã xử lý**: lockout policy có cấu hình + migration user lockout fields | ✅ Done `cng-4uj.4` |
| Security headers missing | ✅ ĐÚNG | **Đã xử lý**: thêm `SecurityHeadersMiddleware` cho API | ✅ Done `cng-4uj.4` |
| CORS hardcode localhost | ✅ ĐÚNG | **Đã xử lý**: CORS đọc từ `Cors:AllowedOrigins` theo môi trường | ✅ Done `cng-4uj.4` |
| Thiếu Receipt draft edit flow | ✅ ĐÚNG | **Đã xử lý**: có draft update flow + bulk approve + reminder dry-run + commit progress | ✅ Done `cng-4uj.5` |
| Reminder thiếu dry-run | ✅ ĐÚNG | **Đã xử lý**: thêm chế độ dry-run + preview recipients | ✅ Done `cng-4uj.5` |
| Import thiếu progress callback | 🟨 MỘT PHẦN | **Đã xử lý**: bổ sung commit progress steps trong result | ✅ Done `cng-4uj.5` |
| Thiếu `useMutation` hook | ✅ ĐÚNG | **Đã xử lý**: thêm `useMutation` + test và dùng trong luồng mới | ✅ Done `cng-4uj.6` |
| Page quá lớn (Dashboard/Reports/Risk) | ✅ ĐÚNG | **Đã xử lý theo scope P1**: tách module trọng điểm + donut drill-down cho dashboard | ✅ Done `cng-4uj.6` |
| Correlation ID middleware thiếu | ✅ ĐÚNG | **Đã xử lý**: thêm middleware correlation id + response header | ✅ Done `cng-4uj.7` |
| Custom business OTel metrics thiếu | ✅ ĐÚNG | **Đã xử lý**: thêm meter `CongNoGolden.Business` + metrics nghiệp vụ | ✅ Done `cng-4uj.7` |
| Status string duplication | ✅ ĐÚNG | **Đã xử lý theo roadmap**: chuẩn hóa constants cho receipt/import các flow chính | ✅ Done `cng-4uj.9` |
| API versioning thiếu | ✅ ĐÚNG | **Đã xử lý**: thêm compatibility middleware `/api/v1` + deprecation headers | ✅ Done `cng-4uj.9` |
| Monitoring stack thiếu | 🟨 MỘT PHẦN | **Đã xử lý**: có baseline Grafana/Loki/Alertmanager + runbook fallback port | ✅ Done `cng-4uj.8` |
| Thiếu Proportional allocation mode | ✅ ĐÚNG | **Đã xử lý**: thêm mode `PRO_RATA` + test regression | ✅ Done `cng-4uj.10` |
| Thiếu CI/CD pipeline | ❌ KHÔNG CÒN ĐÚNG | Đã có `.github/workflows/ci.yml` | Không tạo task mới |
| Thiếu toast notifications | ❌ KHÔNG CÒN ĐÚNG | Đã có Notification Center + toast host | Không tạo task mới |
| Dashboard thiếu responsive hoàn toàn | 🟨 MỘT PHẦN | Đã có media queries cơ bản; cần polish tiếp | `cng-4uj.6` |
| Thiếu `.env.example` | 🟨 MỘT PHẦN | Có `.env.docker.example` + `ENV_SAMPLE.md`, chưa có root `.env.example` thống nhất | `cng-4uj.8` |

### B. Kế hoạch triển khai đã chốt (epic `cng-4uj`)

**P0 (ưu tiên cao nhất)**  
- `cng-4uj.1`: Batch/chunk reconcile + retention, tránh load toàn bộ dữ liệu vào RAM.  
- `cng-4uj.2`: Đảm bảo transactional integrity cho audit ở luồng approve receipt.  
- `cng-4uj.3`: Thêm `MatchMode` cho risk rules, đồng bộ domain + SQL path.  
- `cng-4uj.4`: Security hardening round 3 (lockout, headers, config-driven CORS).

**P1 (workflow + kiến trúc ứng dụng)**  
- `cng-4uj.5`: Draft edit flow, reminder dry-run, commit progress visibility, bulk actions feasibility.  
- `cng-4uj.6`: `useMutation`, split page lớn, allocation donut + drill-down.  
- `cng-4uj.7`: Correlation id + custom business metrics + readiness checks.  
- `cng-4uj.9`: Status constants/enums + API versioning roadmap.

**P2 (scale/readiness)**  
- `cng-4uj.8`: Monitoring baseline dashboards/alerts + env template consolidation.  
- `cng-4uj.10`: Allocation Pro-rata mode + regression tests.

### C. Kết luận cập nhật sau đối chiếu

- Opus V2 nêu đúng phần lớn vấn đề trọng yếu kỹ thuật (đặc biệt nhóm memory, workflow, security, architecture).  
- Có một số nhận định đã lỗi thời theo thời điểm kiểm tra ngày **13/02/2026** (CI/CD, toast).  
- Backlog đã được chuyển đầy đủ vào `task.md` + bead epic `cng-4uj` và các bead con để triển khai theo thứ tự P0 → P1 → P2.

### D. Tiến độ triển khai thực tế (cập nhật 13/02/2026)

- ✅ Hoàn tất `cng-4uj.1` (P0):  
  - `CustomerBalanceReconcileService` chuyển sang xử lý theo batch, không còn load toàn bộ customers vào memory trong một lần; giảm giữ entity trong `ChangeTracker` và vẫn giữ đúng kết quả drift/update.  
  - `DataRetentionService` chuyển sang xóa theo batch có cấu hình `DeleteBatchSize`, hỗ trợ nhánh relational bằng `ExecuteDeleteAsync` và non-relational bằng remove/save theo lô nhỏ.  
  - Bổ sung test regression cho hai service theo vòng **RED → GREEN**, sau đó chạy lại full backend verify.
- ✅ Hoàn tất `cng-4uj.2` (P0):  
  - Luồng `ReceiptService.ApproveAsync` đã ghi audit nghiệp vụ (`RECEIPT_APPROVE` và `PERIOD_LOCK_OVERRIDE`) **trước** `CommitAsync`, đảm bảo atomicity giữa dữ liệu nghiệp vụ và nhật ký audit.  
  - Thêm integration test `Approve_WhenAuditFails_RollsBackReceiptAndAllocations` để chứng minh khi audit throw thì approve rollback toàn bộ.
- ✅ Hoàn tất `cng-4uj.3` (P0):  
  - Bổ sung `MatchMode` (`ANY`/`ALL`) cho `RiskRule` xuyên suốt Domain (`RiskClassifier`), Application DTO/request, Infrastructure entity/service và SQL classify path (`RiskService.Sql.cs`).  
  - Thêm migration `024_risk_rule_match_mode.sql` (column + default + constraint).  
  - Thêm test regression xác nhận mode `ALL` giảm false-positive so với `ANY`.
- ✅ Hoàn tất `cng-4uj.4` (P0):  
  - Triển khai account lockout có cấu hình (`AuthSecurityOptions`: max failed attempts, lockout minutes), thêm cột theo dõi vào `users` + migration `025_user_login_lockout.sql`.  
  - Thêm `SecurityHeadersMiddleware` với bộ header bảo vệ chuẩn cho API và HSTS khi HTTPS.  
  - Thay CORS hard-code localhost bằng policy đọc từ config (`Cors:AllowedOrigins`), có fallback hợp lý cho Development.
- ✅ Hoàn tất `cng-4uj.5` (P1):  
  - Bổ sung sửa phiếu thu trạng thái DRAFT, bulk approve có kiểm soát, reminder dry-run, và commit progress visibility.  
  - Bổ sung test tích hợp cho draft/bulk approve/reminder/import rollback.
- ✅ Hoàn tất `cng-4uj.6` (P1):  
  - Thêm `useMutation` + test, cập nhật dashboard dùng allocation donut + drill-down.  
  - Hoàn thiện verify frontend sau refactor (`test/build/budget`).
- ✅ Hoàn tất `cng-4uj.7` (P1):  
  - Thêm `CorrelationIdMiddleware`, metrics nghiệp vụ (`CongNoGolden.Business`), và mở rộng readiness checks.
- ✅ Hoàn tất `cng-4uj.9` (P1):  
  - Chuẩn hóa status constants nhóm receipt/import theo roadmap và thêm middleware tương thích `/api/v1` + deprecation headers.
- ✅ Hoàn tất `cng-4uj.8` (P2):  
  - Provision baseline monitoring `Prometheus + Alertmanager + Loki + Grafana`, dashboard/alert mặc định, và runbook vận hành.
  - Bổ sung fallback port cho môi trường local (`LOKI_PORT`, `GRAFANA_PORT`) + fix cấu hình Loki 3.x.
- ✅ Hoàn tất `cng-4uj.10` (P2):  
  - Thêm mode phân bổ `PRO_RATA` trong `AllocationEngine` + parser/service + test regression.

#### Verification snapshot (13/02/2026)
- Backend: `dotnet build` pass; unit tests `115/115` pass; integration tests `41/41` pass.  
- Frontend: `vitest --run` pass (`88/88`), `vite build` pass, `build:budget` pass.
- Monitoring local: `loki/prometheus/grafana` health đều `200` tại `13100/9090/13001`.

### G. Cập nhật phiên scale-readiness đã hoàn tất (23/02/2026)

> Mục này bổ sung các đầu việc bạn yêu cầu cho vòng review 3.  
> Bead roadmap: `cng-oiw` và các task con `cng-oiw.1` -> `cng-oiw.5` (đã đóng).

- ✅ 1) Baseline k6 + SLO
  - Đã triển khai baseline tải bằng k6 với script `scripts/load/k6/baseline.js` và runner `scripts/load/run-k6-baseline.ps1`.
  - Đã chốt mục tiêu SLO tại `docs/performance/SLO_TARGETS.md` (availability, error rate, p95/p99, throughput, queue targets).
  - Tài liệu vận hành baseline: `docs/performance/LOAD_TESTING_BASELINE.md`.

- ✅ 2) Redis cache (đọc nhiều, invalidate rõ)
  - Đã bật read-model cache cho nhóm endpoint read-heavy (dashboard/reports/risk) qua `ReadModelCacheService`.
  - Đã có invalidation middleware theo namespace (`dashboard`, `reports`, `risk`) khi mutation phát sinh.
  - Có test xác nhận invalidation/cache behavior (`ReadModelCacheServiceTests`, `ReadModelCacheInvalidationMiddlewareTests`).

- ✅ 3) Queue/worker cho job nặng
  - Đã thêm maintenance queue + worker nền:
    - `IMaintenanceJobQueue` / `MaintenanceJobQueue`
    - `MaintenanceJobWorkerHostedService`
  - API async đã có:
    - `POST /admin/health/reconcile-balances/queue`
    - `POST /admin/health/run-retention/queue`
    - `GET /admin/maintenance/jobs`
    - `GET /admin/maintenance/jobs/{jobId}`
  - Đã có metrics queue/job và runbook: `docs/performance/QUEUE_WORKER_OPERATIONS.md`.

- ✅ 4) DB read-replica + tách read/write
  - Đã tách route kết nối:
    - `CreateWrite()` -> primary
    - `CreateRead()` -> read-replica (fallback primary nếu chưa cấu hình).
  - Đã áp dụng cho nhóm read-heavy services (dashboard/reports/risk).
  - Tài liệu: `docs/performance/READ_REPLICA_ROUTING.md`.

- ✅ 5) Autoscaling policy + observability/alert theo SLO
  - Đã thiết lập guardrails autoscaling theo latency/saturation/queue pressure tại:
    - `docs/performance/AUTOSCALING_GUARDRAILS.md`
  - Đã gắn baseline observability + alerting:
    - Prometheus rules: `src/ops/monitoring/prometheus/rules/congno-alerts.yml`
    - Alertmanager config: `src/ops/monitoring/alertmanager/alertmanager.yml`
    - Monitoring runbook/baseline: `docs/OPS_MONITORING_BASELINE.md`
  - Mục tiêu SLO làm ngưỡng vận hành/alert đã được chuẩn hóa tại `docs/performance/SLO_TARGETS.md`.

- ✅ Verification snapshot cho phase scale-readiness (23/02/2026)
  - `dotnet build src/backend/Api/CongNoGolden.Api.csproj` pass.
  - `dotnet test src/backend/Tests.Unit/Tests.Unit.csproj` pass (`127/127`).
  - `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj` pass (`41/41`).
  - `npm --prefix src/frontend run lint` pass.
  - `npm --prefix src/frontend run test -- --run` pass (`90/90`).
  - `npm --prefix src/frontend run build` + `build:budget` pass.


