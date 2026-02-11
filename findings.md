# Findings & Decisions
<!-- 
  WHAT: Your knowledge base for the task. Stores everything you discover and decide.
  WHY: Context windows are limited. This file is your "external memory" - persistent and unlimited.
  WHEN: Update after ANY discovery, especially after 2 view/browser/search operations (2-Action Rule).
-->

## Requirements
<!-- 
  WHAT: What the user asked for, broken down into specific requirements.
  WHY: Keeps requirements visible so you don't forget what you're building.
  WHEN: Fill this in during Phase 1 (Requirements & Discovery).
  EXAMPLE:
    - Command-line interface
    - Add tasks
    - List all tasks
    - Delete tasks
    - Python implementation
-->
<!-- Captured from user request -->
- Làm cả 3: (1) fix groupBy dashboard, (2) review BE services+migrations, (3) review UI pages chính.

## Research Findings
<!-- 
  WHAT: Key discoveries from web searches, documentation reading, or exploration.
  WHY: Multimodal content (images, browser results) doesn't persist. Write it down immediately.
  WHEN: After EVERY 2 view/browser/search operations, update this section (2-Action Rule).
  EXAMPLE:
    - Python's argparse module supports subcommands for clean CLI design
    - JSON module handles file persistence easily
    - Standard pattern: python script.py <command> [args]
-->
<!-- Key discoveries during exploration -->
- Dashboard overdue groupBy đang bỏ qua (SQL nhánh else trùng) tại `src/backend/Infrastructure/Services/DashboardService.cs:170`.
- Notification list dùng div onClick, cần đổi sang button/link theo guideline.
- Chưa có integration test cho Dashboard overdue groups trong `Tests.Integration`.
- Customer entity nằm trong `Infrastructure/Data/Entities/MasterEntities.cs`, có `PaymentTermsDays` (int, mặc định 0).
- DashboardService.Sql.cs có DashboardTopOverdueDaysSql trước DashboardOverdueByOwnerSql (vùng chèn SQL mới).
- Đã bổ sung groupBy=customer cho overdue groups và test tích hợp mới.
- Backend không có TODO/FIXME/HACK trong src/backend.
- ReceiptAutomationService: AutoAllocate/SendReminders chạy per-receipt queries (LoadOpenItems + Customer lookup) → potential N+1 nếu số phiếu thu lớn.

## Technical Decisions
<!-- 
  WHAT: Architecture and implementation choices you've made, with reasoning.
  WHY: You'll forget why you chose a technology or approach. This table preserves that knowledge.
  WHEN: Update whenever you make a significant technical choice.
  EXAMPLE:
    | Use JSON for storage | Simple, human-readable, built-in Python support |
    | argparse with subcommands | Clean CLI: python todo.py add "task" |
-->
<!-- Decisions made with rationale -->
| Decision | Rationale |
|----------|-----------|
| Fix groupBy trước | Bug ảnh hưởng dashboard và phạm vi nhỏ |
| Review BE rồi UI | Tránh chồng chéo fix frontend khi backend còn thay đổi |

## Issues Encountered
<!-- 
  WHAT: Problems you ran into and how you solved them.
  WHY: Similar to errors in task_plan.md, but focused on broader issues (not just code errors).
  WHEN: Document when you encounter blockers or unexpected challenges.
  EXAMPLE:
    | Empty file causes JSONDecodeError | Added explicit empty file check before json.load() |
-->
<!-- Errors and how they were resolved -->
| Issue | Resolution |
|-------|------------|
| CustomerEntities.cs không tồn tại | Cần tìm đúng file entity trong `Infrastructure/Data/Entities` |

## Resources
<!-- 
  WHAT: URLs, file paths, API references, documentation links you've found useful.
  WHY: Easy reference for later. Don't lose important links in context.
  WHEN: Add as you discover useful resources.
  EXAMPLE:
    - Python argparse docs: https://docs.python.org/3/library/argparse.html
    - Project structure: src/main.py, src/utils.py
-->
<!-- URLs, file paths, API references -->
-

## Visual/Browser Findings
<!-- 
  WHAT: Information you learned from viewing images, PDFs, or browser results.
  WHY: CRITICAL - Visual/multimodal content doesn't persist in context. Must be captured as text.
  WHEN: IMMEDIATELY after viewing images or browser results. Don't wait!
  EXAMPLE:
    - Screenshot shows login form has email and password fields
    - Browser shows API returns JSON with "status" and "data" keys
-->
<!-- CRITICAL: Update after every 2 view/browser operations -->
<!-- Multimodal content must be captured as text immediately -->
-

---
<!-- 
  REMINDER: The 2-Action Rule
  After every 2 view/browser/search operations, you MUST update this file.
  This prevents visual information from being lost when context resets.
-->
*Update this file after every 2 view/browser/search operations*
*This prevents visual information from being lost*

- 2026-01-29: Người dùng xác nhận phạm vi toàn hệ thống; cần chốt ưu tiên thực hiện.
- 2026-01-29: Áp dụng checklist code review cho phần quét BE/FE; chuẩn bị bước review toàn hệ thống.
- 2026-01-29: Dùng hướng dẫn Web Interface Guidelines khi review UI; scope audit toàn hệ thống không mặc định refactor toàn bộ (production-code-audit) trừ khi người dùng yêu cầu.
- 2026-01-29: Yêu cầu mới: nếu cần refactor lớn phải báo cáo xin duyệt trước khi thực hiện.
- 2026-01-29: Infrastructure/Services có ~50+ service files; Migrations chỉ có MigrationOptions.cs và MigrationRunner.cs (không thấy file migration riêng trong thư mục này).
- 2026-01-29: MigrationRunner áp dụng DbUp từ thư mục scripts (mặc định scripts/db/migrations), fallback connection string Default; nếu chạy từ cwd khác có thể không tìm thấy script path (đã có logic tìm lên thư mục cha).
- 2026-01-29: Có 17 migration SQL trong scripts/db/migrations. 017_report_preferences.sql tạo bảng congno.user_report_preferences với PK (user_id, report_key), chưa có trigger tự động cập nhật updated_at (nếu cần sẽ do ứng dụng xử lý).
- 2026-01-29: Migrations đã có indexes cho import/reminder logs và report (issue_date/advance_date/receipts approved theo customer/seller/date). Phù hợp cho quy mô vài nghìn bản ghi/năm.
- 2026-01-29: Template Excel nguồn nằm ở src/frontend/public/templates (advance/invoice/receipt). Backend cũng có bộ mẫu report ở src/backend/Infrastructure/Templates (TongQuan/TongHop/ChiTiet/Aging).
- 2026-01-29: Xlsx workflow áp dụng để đọc template; backend templates chính nằm ở src/backend/Infrastructure/Templates (5 file Mau_DoiSoat...).
- 2026-01-29: ImportTemplateParser/ImportInvoiceTemplateParser dùng normalize+token chứa để map header; receipt import tự default method=BANK nếu trống/invalid, applied_period bắt buộc ngày 1 trong tháng (auto sửa).
- 2026-01-29: Template import (frontend/public/templates): advance/receipt headers khớp parser (snake_case). invoice_template.xlsx có row1 'MaSoThue/0102030405' và header bắt đầu ở row5 (STT/BuyerName/TaxCode/RevenueExcludingVAT/VatAmount/Note), thiếu nhiều cột mà ImportInvoiceTemplateParser yêu cầu và không nằm ở hàng header đầu tiên → import invoice gần như chắc fail/thiếu map.
- 2026-01-29: invoice_template.xlsx ở root và public cùng cấu trúc: row1 có MaSoThue + MST mẫu, header trải ở row5-6 (STT/BuyerName/TaxCode/RevenueExcludingVAT/VatAmount/Note và KyHieuMau/SoHieuHoaDon/SoHoaDon/NgayThangNamPhatHanh). Parser hiện chỉ đọc header ở FirstRowUsed nên không map được các cột này → cần sửa template hoặc parser.
- 2026-01-29: ImportStagingService gọi cả ImportInvoiceParser.ParseReportDetail và ImportInvoiceTemplateParser.ParseSimpleTemplate; tức hỗ trợ 2 định dạng invoice. Template hiện tại giống định dạng report detail (STT/BuyerName/TaxCode...), nên ImportInvoiceParser phù hợp hơn.
- 2026-01-29: ImportStagingService ưu tiên ParseReportDetail (ImportInvoiceParser) rồi mới ParseSimpleTemplate. Template invoice hiện tại đúng kiểu report detail (STT/BuyerName + KyHieuMau/SoHoaDon) nên vẫn parse được; tuy nhiên chưa thấy template 'simple' tương ứng ImportInvoiceTemplateParser (SellerTaxCode/CustomerTaxCode/InvoiceNo...) → có thể gây nhầm lẫn cho người dùng nếu mong template đơn giản.
- 2026-01-29: ReportService.Aging (AgingSql) không lọc i.status <> 'VOID' và i.issue_date <= @asOf (trong khi ReportAgingBaseCte có). Có thể làm aging tính cả hóa đơn VOID/future.
- 2026-01-29: ReceiptAutomationService có nguy cơ N+1: LoadOpenItemsAsync và truy vấn ownerId trong NotifyAsync chạy theo từng receipt; với nhiều receipts DRAFT sẽ tốn nhiều query.
- 2026-01-29: PowerShell Get-Content không hỗ trợ -Skip trong môi trường này; dùng Select-Object -Skip/-First để xem đoạn file.
- 2026-01-29: ReceiptAutomationService LoadOpenItemsAsync chạy 2 query (invoices/advances) cho mỗi receipt sau khi load customer; nếu nhiều receipt DRAFT sẽ nhân số query đáng kể (cần batch theo seller/customer).
- 2026-01-29: NotificationService/ReminderService đọc có kiểm soát paging, validation; chưa thấy lỗi rõ ràng trong đoạn đã rà (cần rà tiếp SQL đoạn cuối khi cần).
- 2026-01-29: NotificationListSql dùng ILIKE '%query%' trên title/body; nếu dữ liệu thông báo lớn có thể cần trigram/full-text index để tránh scan.
- 2026-01-29: Không thấy test Aging trong Tests.Unit/Tests.Integration; cần bổ sung test integration cho aging (loại VOID/future) sau khi sửa SQL.
- 2026-01-29: Tests.Integration có pattern dùng TestDatabaseFixture + ResetAsync để seed; có thể theo mẫu này để thêm test cho ReportService.Aging.
- 2026-01-29: Có thể dùng pattern trong DashboardOverdueGroupTests (ResetAsync + SeedMaster/SeedInvoice) để tạo test Aging dựa trên congno schema có sẵn.
- 2026-01-29: Tests.Integration dùng EnsureCreated (không chạy migrations). Các test nên seed/truncate bảng congno.* giống DashboardOverdueGroupTests.
- 2026-01-29: ReportAgingRequest nhận AsOfDate/SellerTaxCode/CustomerTaxCode/OwnerId; test có thể truyền AsOfDate để kiểm tra lọc VOID/future.
- 2026-01-29: Entities nằm theo nhóm file (InvoiceEntities.cs, ReceiptEntities.cs, MasterEntities.cs...), không có file Advance riêng.
- 2026-01-29: Advance entity nằm trong InvoiceEntities.cs; có các field AdvanceDate/Amount/OutstandingAmount/Status. Customer có PaymentTermsDays và AccountantOwnerId dùng cho aging.
- 2026-01-29: ReportAgingRow gồm các bucket 0-30/31-60/... và Total/Overdue; test có thể assert Total/Overdue theo amount seeding.
- 2026-01-29: DapperTypeHandlers.Register cần gọi trước ReportService (DateOnly handler). Nên thêm vào ReportAgingTests tương tự Dashboard tests.
- 2026-01-29: g TODO/FIXME không thấy trong src/backend; src/frontend trả về nhiều do node_modules (cần exclude khi tìm).
- 2026-01-29: ImportsPage lưu tab vào localStorage và ép query param; chưa thấy lỗi UI rõ ràng trong phần này.
- 2026-01-29: ImportBatchSection/ImportPreviewModal: chưa thấy lỗi logic rõ ràng; preview modal hiển thị rawData JSON và validation messages ổn.
- 2026-01-29: ReceiptsPage/ReceiptListSection: lọc có validate date/amount; chưa thấy lỗi logic rõ ràng trong phần đã rà.
- 2026-01-29: CustomersPage/CustomerListSection: xử lý lỗi 401/403 rõ ràng; chưa thấy lỗi UI nghiêm trọng trong phần đã rà.
- 2026-01-29: AdminUsersPage/AdminPeriodLocksPage: xử lý load/error chuẩn; không thấy lỗi logic rõ ràng trong phần đã rà.
- 2026-01-29: UI guideline candidates: ImportPreviewModal dùng div onClick (modal-backdrop) và text '...' thay vì dấu ellipsis; AdminUsersPage có nhiều input cần kiểm tra label/name.
- 2026-01-29: AdminUsersPage inputs đều bọc trong <label> nên có label; cần đối chiếu Web Interface Guidelines để quyết định việc thêm name/autocomplete.
- 2026-01-29: Đã tải Web Interface Guidelines; sẽ đối chiếu các file UI core flow (ImportPreviewModal, AdminUsersPage) với các quy tắc về interactive elements, ellipsis, input name/autocomplete.
- 2026-01-29: task_plan.md hiện Current Phase = Phase 3; cần cập nhật sang Phase 4 và trạng thái các bước kiểm thử.
- 2026-01-29: Phase 3 trong task_plan.md đã hoàn tất các mục chính nhưng status vẫn in_progress; Phase 4 chưa cập nhật kết quả test (frontend OK, backend failed vì DLL lock).
- 2026-01-29: Phase 3 status line hiện là '- **Status:** in_progress' tại task_plan.md (khoảng dòng 65) cần đổi sang complete.
- 2026-01-29: task.md Phase 25 còn tick thiếu (BE services+migrations + UI pages). Cần cập nhật thêm các sửa đổi Aging SQL/test và UI guideline fixes.
- 2026-01-29: bead cng-fsr vẫn in_progress; cần cập nhật khi kết thúc Phase 25.
- 2026-01-29: AdminUsersPage còn input checkbox/search thiếu name/autocomplete (createIsActive, roles, search). Nên bổ sung để phù hợp guideline.
- 2026-01-29: Đã mở lại Web Interface Guidelines (ref mới) và xác nhận line modal-backdrop/ellipsis sau chỉnh sửa.
- 2026-01-29: Đã fetch guideline URL (ref mới) và xác nhận AdminUsersPage đã chuyển 'Đang tải…' + thêm name/autocomplete ở search.
- 2026-01-29: modal-backdrop là container flex (index.css ~753). Nếu chuyển sang button/scrim riêng sẽ cần chỉnh markup/CSS cho toàn bộ modal.
- 2026-01-29: Nhiều chuỗi UI còn dùng "..." ở nhiều page; cần cân nhắc thay thế toàn cục.
- 2026-01-29: Lỗi rg regex (role="presentation".*onClick) do escape sai.
- 2026-01-29: Đã truy vấn guideline về button/ellipsis/autocomplete; ghi nhận modal-backdrop còn role=\"presentation\" onClick ở ReceiptAllocationModal (line 140).
- 2026-01-29: Người dùng yêu cầu tự chọn và triển khai các vấn đề còn lại (UI guideline batch + tối ưu ReceiptAutomationService).

Update: confirmed modal-backdrop instances across UI; ReceiptAutomationService loads customer/open items per receipt (N+1 risk).

Update: no existing ReceiptAutomationService tests in Tests.Integration; will add new coverage.
Update: ReportsValidationModal uses backdrop div with role="presentation" and no click handler; needs accessible scrim.

Update: no existing DbCommandInterceptor/AddInterceptors usage in repo (need custom if counting queries).
Update: Entities are grouped in MasterEntities.cs; will read customer fields from there.

Update: Customer.PaymentTermsDays required for due-date calc; Receipt has ReminderDisabledAt to skip notifications.

Update: can reuse ReportAgingTests seed patterns for invoices/advances in ReceiptAutomationService tests.

Update: ReceiptService.OpenItems uses per-customer load; automation service can reuse logic but batch across pairs.

Update: ReportsValidationModal already has RTL test; will extend for scrim close behavior.

Update: modal-backdrop currently owns background; will move to .modal-scrim button.

Update: receipt modals rely on backdrop click + stopPropagation; will shift to scrim button.

Update: receipt allocation/advanced modals use backdrop click + stopPropagation; will convert to scrim.

Update: ImportPreviewModal uses role=button backdrop; NotificationToastHost uses backdrop click; both will migrate to modal-scrim.

Update: customer modals still use role=presentation backdrop; need scrim for accessibility.

Update: modal-scrim inserted across all modal-backdrop occurrences in frontend.
Update: ReceiptAutomationService now batches open-item loads per seller/customer pair; modal backdrops refactored to scrim buttons for a11y.
Update: located Playwright specs under src/frontend/e2e; will run test:e2e.
Update: search for SKILL.md showed e2e-testing-patterns under C:\Users\PC\.codex; will read from there.
