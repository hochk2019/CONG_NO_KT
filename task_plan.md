# Task Plan: Full system review + fixes (FE/BE/DB/UX)
<!-- 
  WHAT: This is your roadmap for the entire task. Think of it as your "working memory on disk."
  WHY: After 50+ tool calls, your original goals can get forgotten. This file keeps them fresh.
  WHEN: Create this FIRST, before starting any work. Update after each phase completes.
-->

## Goal
<!-- 
  WHAT: One clear sentence describing what you're trying to achieve.
  WHY: This is your north star. Re-reading this keeps you focused on the end state.
  EXAMPLE: "Create a Python CLI todo app with add, list, and delete functionality."
-->
Hoàn tất review toàn hệ thống, xử lý lỗi ưu tiên (groupBy dashboard, UI notifications), rà BE services+migrations và UI chính, ghi lại issue/fix kèm test.

## Current Phase
<!-- 
  WHAT: Which phase you're currently working on (e.g., "Phase 1", "Phase 3").
  WHY: Quick reference for where you are in the task. Update this as you progress.
-->
Phase 5

## Phases
<!-- 
  WHAT: Break your task into 3-7 logical phases. Each phase should be completable.
  WHY: Breaking work into phases prevents overwhelm and makes progress visible.
  WHEN: Update status after completing each phase: pending → in_progress → complete
-->

### Phase 1: Requirements & Discovery
<!-- 
  WHAT: Understand what needs to be done and gather initial information.
  WHY: Starting without understanding leads to wasted effort. This phase prevents that.
-->
- [x] Understand user intent (làm cả 3: groupBy, BE review, UI review)
- [x] Identify constraints (theo AGENTS.md: test, module <800 LOC, cập nhật task+beads)
- [x] Document findings in findings.md
- **Status:** complete
<!-- 
  STATUS VALUES:
  - pending: Not started yet
  - in_progress: Currently working on this
  - complete: Finished this phase
-->

### Phase 2: Planning & Structure
<!-- 
  WHAT: Decide how you'll approach the problem and what structure you'll use.
  WHY: Good planning prevents rework. Document decisions so you remember why you chose them.
-->
- [x] Define approach + order (groupBy bugfix → BE review → UI review)
- [x] Document decisions with rationale
- **Status:** complete

### Phase 3: Implementation
<!-- 
  WHAT: Actually build/create/write the solution.
  WHY: This is where the work happens. Break into smaller sub-tasks if needed.
-->
- [x] Fix dashboard overdue groupBy
- [x] Add integration test for overdue groups
- [x] Review BE services + migrations, log issues/fixes
- [x] Review UI pages (Dashboard/Customers/Imports/Receipts/Admin/Notifications) + fixes
- [x] Run backend tests after groupBy change
- **Status:** complete

### Phase 4: Testing & Verification
<!-- 
  WHAT: Verify everything works and meets requirements.
  WHY: Catching issues early saves time. Document test results in progress.md.
-->
- [x] Verify requirements met
- [x] Document test results in progress.md
- [x] Fix remaining issues
- **Status:** complete

### Phase 5: Delivery
<!-- 
  WHAT: Final review and handoff to user.
  WHY: Ensures nothing is forgotten and deliverables are complete.
-->
- [x] Review outputs
- [x] Update task.md + beads status
- [ ] Deliver to user
- **Status:** in_progress

## Key Questions
<!-- 
  WHAT: Important questions you need to answer during the task.
  WHY: These guide your research and decision-making. Answer them as you go.
  EXAMPLE: 
    1. Should tasks persist between sessions? (Yes - need file storage)
    2. What format for storing tasks? (JSON file)
-->
1. Nên hỗ trợ groupBy nào cho Dashboard overdue? (owner/customer/seller) hay chỉ owner
2. BE review sẽ ưu tiên khu vực nào (imports/receipts/risk/notifications) nếu phát hiện nhiều issue?

## Decisions Made
<!-- 
  WHAT: Technical and design decisions you've made, with the reasoning behind them.
  WHY: You'll forget why you made choices. This table helps you remember and justify decisions.
  WHEN: Update whenever you make a significant choice (technology, approach, structure).
  EXAMPLE:
    | Use JSON for storage | Simple, human-readable, built-in Python support |
-->
| Decision | Rationale |
|---|---|
| Fix groupBy before reviews | Bug trực tiếp ảnh hưởng dashboard, ít scope |
| BE review sau groupBy | tránh thay đổi song song chồng chéo |

## Errors Encountered
<!-- 
  WHAT: Every error you encounter, what attempt number it was, and how you resolved it.
  WHY: Logging errors prevents repeating the same mistakes. This is critical for learning.
  WHEN: Add immediately when an error occurs, even if you fix it quickly.
  EXAMPLE:
    | FileNotFoundError | 1 | Check if file exists, create empty list if not |
    | JSONDecodeError | 2 | Handle empty file case explicitly |
-->
| Error | Attempt | Resolution |
|-------|---------|------------|
| CustomerEntities.cs not found | 1 | Sẽ tìm đúng file entity theo thư mục `Infrastructure/Data/Entities` |
| dotnet test timeout | 1 | Sẽ chạy lại với timeout dài hơn / scope Tests.Integration |
| dotnet test failed (DLL locked by API process) | 2 | Thử chạy `dotnet test --no-build` hoặc dừng API đang chạy |
| Get-Content không hỗ trợ -Skip | 1 | Dùng `Get-Content | Select-Object -Skip/-First` thay thế |
| rg path src/backend/Tests không tồn tại | 1 | Dùng đúng thư mục Tests.Unit và Tests.Integration |
| dotnet test bị khóa DLL (CongNoGolden.Api đang chạy) | 3 | Cần dừng API rồi chạy lại test |
| web.run find invalid ref_id | 1 | Mở lại URL để lấy ref_id mới rồi tìm |
| web.run find invalid ref_id (lần 2) | 2 | Sẽ mở URL lại và dùng ref_id mới từ open |
| rg regex parse error (role="presentation".*onClick) | 1 | Escape regex đúng hoặc dùng Find-String đơn giản |
| Add-Content thất bại do quoting | 1 | Dùng single quotes/escape khi value có ký tự đặc biệt |
| Add-Content JSONL bead update lỗi escape | 1 | Dùng here-string để ghi JSONL |

## Notes
<!-- 
  REMINDERS:
  - Update phase status as you progress: pending → in_progress → complete
  - Re-read this plan before major decisions (attention manipulation)
  - Log ALL errors - they help avoid repetition
  - Never repeat a failed action - mutate your approach instead
-->
- Update phase status as you progress: pending → in_progress → complete
- Re-read this plan before major decisions (attention manipulation)
- Log ALL errors - they help avoid repetition

Update: queued TDD for ReceiptAutomationService N+1 fix and modal-backdrop refactor; gathered file locations.

Update: searched for existing ReceiptAutomationService tests; none found in Tests.Integration.

Update: reviewed ReportsValidationModal for backdrop refactor; will add scrim button + tests.

Update: inspected TestDatabaseFixture; no interceptor pattern found yet for query counting.

Update: located Entities folder; customer entity in MasterEntities.cs.

Update: reviewed Customer/Receipt entities to seed required fields for new tests.

Update: reviewed Invoice/Advance entities and ReportAgingTests seeding helpers for reuse.

Update: located ReceiptService open-item loader for reference; proceeding to batch logic in ReceiptAutomationService.

Update: inspected reports modal test; found receipts-modules.test.tsx for ReceiptCancelModal coverage.

Update: located modal-backdrop CSS; will split into scrim + modal layering.

Update: reviewed receipt modals markup to insert modal-scrim and remove backdrop click handlers.

Update: reviewed allocation/advanced receipt modals; preparing scrim insertion.

Update: reviewed ImportPreviewModal and NotificationToastHost for scrim/button refactor.

Update: reviewed customer modals; will insert scrim buttons for edit/invoice/advance/receipt modals.

Update: verified modal-backdrop/scrim occurrences after refactor.

Update: completed batch open-items loader + scrim refactor; tests passing for targeted suites.
Update: confirmed e2e script is `npm run test:e2e` (Playwright) from frontend package.json.
Update: e2e-testing-patterns skill not found under repo .codex; located skills in C:\Users\PC\.codex.
Update: completed e2e run (Playwright) per user request.
