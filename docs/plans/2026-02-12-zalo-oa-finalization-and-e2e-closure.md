# Zalo OA Finalization + E2E Closure Implementation Plan

> [!IMPORTANT]
> **HISTORICAL EXECUTION PLAN**
> Tài liệu này là kế hoạch/thực thi theo thời điểm viết, có thể chứa giả định cũ.
> Nguồn vận hành hiện hành: `DEPLOYMENT_GUIDE_DOCKER.md`, `RUNBOOK.md`, `task.md`.


> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Hoàn tất các mục Opus còn mở liên quan Zalo OA thật và xác nhận lại E2E/regression sau chuyển Docker runtime.

**Architecture:** Tách thành 3 lớp thực thi: (1) xác minh cấu hình runtime + endpoint health, (2) xác thực luồng Zalo OA thật (link, callback, reminder SENT), (3) chạy lại bộ E2E mục tiêu để chốt tương thích FE/BE/DB/UI. Chỉ chỉnh code khi phát hiện lỗi thật trong quá trình verify; ưu tiên giữ thay đổi nhỏ, có test/bằng chứng.

**Tech Stack:** .NET 8 API, React/Vite frontend, Docker Compose, Playwright E2E, Beads task tracking.

---

### Task 1: Baseline trạng thái Opus + Beads + task.md

**Files:**
- Modify: `task.md`
- Modify: `.beads/*` (qua `bd` CLI)

**Step 1: Xác định mục Opus còn mở thực tế**

Run: `rg -n "Zalo OA|Phase 30|cng-j1t" Opus_4.6_review.md task.md`
Expected: Chỉ còn `cng-j1t.1`, `cng-j1t.2`, `cng-j1t.4` mở.

**Step 2: Xác nhận trạng thái beads**

Run: `bd ready --json`
Expected: Thấy các bead mở tương ứng.

**Step 3: Đồng bộ checklist thi công trong task.md**

Thêm/chuẩn hóa checklist thực thi cho từng bead chưa đóng.

**Step 4: Commit (nếu có thay đổi)**

```bash
git add task.md
git commit -m "docs: align pending Opus Zalo OA closure tasks"
```

### Task 2: Verify runtime Docker + env Zalo OA

**Files:**
- Verify: `.env`
- Verify: `.env.docker.example`
- Verify: `docker-compose.yml`
- Verify: `DEPLOYMENT_GUIDE_DOCKER.md`

**Step 1: Kiểm tra đủ biến Zalo trong runtime**

Run: `docker compose config`
Expected: Service API nhận đủ `Zalo__OaId`, `Zalo__AccessToken`, `Zalo__WebhookToken`, `Zalo__Enabled`.

**Step 2: Verify API health và endpoint Zalo**

Run: `curl`/PowerShell invoke các endpoint `/health`, `/health/ready`, `/zalo/link/request`, `/zalo/link/status`.
Expected: API healthy; endpoint trả response hợp lệ.

**Step 3: Sửa cấu hình nếu thiếu**

Cập nhật `docker-compose.yml` hoặc docs/env example nếu phát hiện mismatch.

**Step 4: Test nhanh sau sửa**

Run: `docker compose up -d --build api`
Expected: API boot sạch, không lỗi config Zalo.

### Task 3: Zalo OA thật - callback + reminder SENT

**Files:**
- Verify runtime logs: `docker compose logs api`
- Modify docs nếu cần: `DEPLOYMENT_GUIDE_DOCKER.md`, `docs/OPS_ADMIN_CONSOLE.md`
- Modify tracker: `task.md`

**Step 1: Validate callback URL/token thực tế**

Run: gọi thử `/webhooks/zalo?token=...` theo flow verify.
Expected: Callback accepted theo token đúng.

**Step 2: Chạy flow reminder thật**

Run: endpoint `/reminders/run` với auth hợp lệ.
Expected: tạo/đẩy reminder, log có trạng thái `SENT` cho kênh Zalo OA.

**Step 3: Thu bằng chứng log**

Run: lọc log `SENT`, `zalo`, `reminder`.
Expected: có bản ghi chứng minh OA thật hoạt động end-to-end.

**Step 4: Đóng bead tương ứng**

Run:
```bash
bd close cng-j1t.1
bd close cng-j1t.2
bd close cng-j1t.4
bd sync
```

### Task 4: E2E/regression chốt sau Zalo OA

**Files:**
- Verify: `src/frontend/playwright.config.ts`
- Verify: `src/frontend/e2e/**/*.spec.ts`
- Modify tracker/docs nếu cần: `task.md`, `QA_REPORT.md`

**Step 1: Cài lại deps sạch nếu cần**

Run: `npm ci` (frontend)
Expected: thành công, không còn lock issue.

**Step 2: Chạy smoke e2e mục tiêu**

Run: `npx playwright test e2e/auth-login.spec.ts e2e/dashboard.spec.ts e2e/receipts-flow.spec.ts`
Expected: pass.

**Step 3: Chạy full regression e2e (nếu thời gian cho phép)**

Run: `npx playwright test`
Expected: pass hoặc có danh sách fail rõ root cause.

**Step 4: Cập nhật kết quả**

Ghi pass/fail + command đã chạy vào `task.md`/`QA_REPORT.md`.

### Task 5: Verify-before-completion

**Files:**
- Modify: `task.md`
- Modify: `Opus_4.6_review.md` (nếu trạng thái thay đổi)

**Step 1: Chạy bộ verify cuối**

Run:
```bash
dotnet test src/backend/Tests.Unit/Tests.Unit.csproj
dotnet test src/backend/Tests.Integration/Tests.Integration.csproj
npm run -C src/frontend test
```
Expected: pass hoặc báo cáo fail có hướng xử lý.

**Step 2: Đồng bộ trạng thái**

- tick hoàn tất trong `task.md`
- cập nhật trạng thái Opus còn mở thành done (nếu có bằng chứng)
- đảm bảo beads đã đóng/sync.

**Step 3: Commit (nếu có thay đổi)**

```bash
git add task.md Opus_4.6_review.md QA_REPORT.md
git commit -m "chore: close remaining Opus Zalo OA and E2E verification tasks"
```

