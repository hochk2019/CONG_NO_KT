# Task Plan - 2026-02-23

## Goal
Tự động xử lý các vấn đề chất lượng còn tồn tại sau lần rà soát gần nhất, cập nhật trạng thái công việc và đồng bộ tài liệu vận hành.

## Tasks
- [x] Áp dụng skill workflow (`planning-with-files`, `plan-writing`, `lint-and-validate`, `verification-before-completion`).
- [x] Xác thực trạng thái tracker (`task.md` + beads) để tìm việc còn mở.
- [x] Sửa lỗi chất lượng mã đang fail (`eslint` frontend).
- [x] Chạy lại verification đầy đủ cho backend/frontend.
- [x] Đóng bead mở còn lại nếu đã có đủ bằng chứng pass.
- [x] Cập nhật tài liệu liên quan (`task.md`, `QA_REPORT.md`, `progress.md`, `task_plan.md`, `findings.md`).

## Done When
- [x] Không còn bead `ready/in_progress` cho phạm vi đang xử lý.
- [x] Frontend lint pass.
- [x] Backend + frontend test/build pass.
- [x] Tài liệu trạng thái phản ánh đúng kết quả mới nhất.

---

## Goal (Update 2026-02-23 - Scale Planning)
Lập kế hoạch mở rộng hệ thống theo lộ trình có thể triển khai dần, có tracker bead + tài liệu cho người không chuyên theo dõi.

## Tasks (Update 2026-02-23 - Scale Planning)
- [x] Tạo epic và task con cho scale readiness trong bead.
- [x] Viết execution plan trong `docs/plans` với ngôn ngữ dễ hiểu.
- [x] Đồng bộ `task.md` để phản ánh roadmap mới và các chặng triển khai kế tiếp.

## Done When (Update 2026-02-23 - Scale Planning)
- [x] Có epic `cng-oiw` và task `cng-oiw.1` -> `cng-oiw.5`.
- [x] Có tài liệu kế hoạch: `docs/plans/2026-02-23-scale-readiness-roadmap.md`.
- [x] Có Phase 67 trong `task.md` để theo dõi tiến độ triển khai.

---

## Goal (Update 2026-02-23 - Scale Execution)
Triển khai toàn bộ roadmap scale readiness (`cng-oiw.1` -> `cng-oiw.5`) với code + verification + tài liệu vận hành.

## Tasks (Update 2026-02-23 - Scale Execution)
- [x] Hoàn tất baseline load test assets + SLO docs (`cng-oiw.1`).
- [x] Triển khai Redis read-model cache + invalidation (`cng-oiw.2`).
- [x] Triển khai queue/worker cho maintenance jobs + endpoint theo dõi (`cng-oiw.3`).
- [x] Hoàn tất read-replica routing cho read-heavy services (`cng-oiw.4`).
- [x] Hoàn tất tài liệu autoscaling + guardrails + rollback/game-day (`cng-oiw.5`).
- [x] Chạy verification backend/frontend trước khi cập nhật tracker.

## Done When (Update 2026-02-23 - Scale Execution)
- [x] Các bead con `cng-oiw.1` -> `cng-oiw.5` đều đóng.
- [x] Epic `cng-oiw` đóng.
- [x] Test/build/lint pass theo evidence mới nhất.
- [x] `task.md`, `findings.md`, `progress.md`, runbook và docs hiệu năng được cập nhật.

---

## Goal (Update 2026-02-24 - Opus V3 Validation Retry)
Hoàn tất vòng xác thực lại `Opus_review_v3.md`, đồng bộ tracker và đóng bead `cng-rlx*` với evidence test mới.

## Tasks (Update 2026-02-24 - Opus V3 Validation Retry)
- [x] Xác thực lại claim V3 theo code hiện tại và ghi matrix `OUTDATED/PARTIAL/CONFIRMED GAP`.
- [x] Chốt phạm vi thực thi: chỉ xử lý tài liệu/tracker cho các hạng mục đã có sẵn trong code.
- [x] Chạy lại verification backend/frontend trong cùng phiên để làm evidence đóng bead.
- [x] Cập nhật `task.md`, `task_plan.md`, `findings.md`, `progress.md`, `opus-review-v3-remediation.md`.
- [x] Đóng bead `cng-rlx.1` -> `cng-rlx.5` và epic `cng-rlx`.

## Done When (Update 2026-02-24 - Opus V3 Validation Retry)
- [x] `Opus_review_v3.md` có validation addendum với bằng chứng file-level.
- [x] Verification suite pass (`dotnet build/test`, `npm lint/test/build`).
- [x] Tracker và beads đồng bộ trạng thái hoàn tất.

---

## Goal (Update 2026-02-26 - cng-d3e.2)
Hoàn tất bead `cng-d3e.2`: refactor reminder escalation theo response state và chốt bằng test targeted.

## Tasks (Update 2026-02-26 - cng-d3e.2)
- [x] Xử lý compile blocker `EnsureUser` trong `ReminderService.ResponseState.cs`.
- [x] Chạy lại backend reminder integration tests.
- [x] Chạy frontend test cho Risk Alerts tabs sau cập nhật payload settings.
- [x] Đóng bead `cng-d3e.2` và đồng bộ tracker files.

## Done When (Update 2026-02-26 - cng-d3e.2)
- [x] `dotnet test ... --filter "FullyQualifiedName~Reminder"` pass.
- [x] `npm --prefix src/frontend run test -- risk-alerts-page-tabs` pass.
- [x] `bd show cng-d3e.2` trạng thái `CLOSED`.

---

## Goal (Update 2026-02-26 - cng-d3e.3)
Hoàn tất residual closure của Opus V3 cho reminder escalation: bổ sung test transition còn thiếu và đồng bộ lại review/tracker để đóng epic `cng-d3e`.

## Tasks (Update 2026-02-26 - cng-d3e.3)
- [x] Bổ sung integration tests cho transition `DISPUTED` và `ESCALATION_LOCKED`.
- [x] Chạy lại targeted backend tests cho `ReminderEscalationPolicyTests`.
- [x] Chạy lại frontend targeted test `risk-alerts-page-tabs`.
- [x] Cập nhật `Opus_review_v3.md` (claim reminder escalation từ `PARTIAL` -> đã có).
- [x] Đồng bộ `task.md`, `task_plan.md`, `findings.md`, `progress.md`.
- [x] Đóng beads `cng-d3e.1`, `cng-d3e.3` và epic `cng-d3e`.

## Done When (Update 2026-02-26 - cng-d3e.3)
- [x] `dotnet test src/backend/Tests.Integration/CongNoGolden.Tests.Integration.csproj --filter "FullyQualifiedName~ReminderEscalationPolicyTests"` pass (`6/6`).
- [x] `npm --prefix src/frontend run test -- risk-alerts-page-tabs` pass (`1/1`).
- [x] `Opus_review_v3.md` phản ánh đúng trạng thái reminder escalation.
- [x] `bd show cng-d3e` hiển thị epic và tasks con ở trạng thái `CLOSED`.

---

## Goal (Update 2026-02-26 - cng-9y1)
Hoàn tất UX validation cho Import dropzone theo bead `cng-9y1`: báo lỗi sớm cho file sai định dạng/kích thước, bổ sung test và đồng bộ tracker.

## Tasks (Update 2026-02-26 - cng-9y1)
- [x] Thêm trạng thái visual lỗi cho dropzone (`upload-dropzone--error`).
- [x] Bổ sung test cho các case invalid:
  - [x] reject non-`.xlsx` khi drag-drop.
  - [x] reject file `.xlsx` vượt `20MB` qua input.
  - [x] reject file `.xlsx` vượt `20MB` qua drag-drop.
- [x] Chạy verification phù hợp cho frontend thay đổi.
- [x] Cập nhật `task.md`, `progress.md`, `findings.md` và đóng bead `cng-9y1`.

## Done When (Update 2026-02-26 - cng-9y1)
- [x] `npm run test -- --run src/pages/imports/__tests__/importBatchSection.dragdrop.test.tsx` pass (`4/4`).
- [x] `npm run lint` pass.
- [x] `bd show cng-9y1` ở trạng thái `CLOSED`.

---

## Goal (Update 2026-03-17 - cng-huj)
Hoàn tất feature import hóa đơn điều chỉnh giảm từ dòng âm: staging chấp nhận dòng âm hợp lệ, commit tạo chứng từ `ADJUSTMENT_REDUCTION`, phân bổ theo unique direct match hoặc FIFO theo root MST, và phần giảm trừ vượt công nợ mở được chuyển sang held credit.

## Tasks (Update 2026-03-17 - cng-huj)
- [x] Mở bead `cng-huj`, chuyển `in_progress`, và đồng bộ tracker root files.
- [x] Viết failing tests cho parser/staging/preview với dòng âm `Hóa đơn điều chỉnh giảm` và dòng `0` chỉ mang tính thông tin.
- [x] Viết failing tests cho commit `ADJUSTMENT_REDUCTION`, root-MST grouping, FIFO fallback, và residual held credit.
- [x] Implement thay đổi backend import/allocation/reconcile để đáp ứng nghiệp vụ mới.
- [x] Nếu preview UI cần thay đổi, cập nhật frontend regression cho skip reason và hiển thị document type hợp lệ.
- [x] Chạy targeted verification và cập nhật lại bead/task sau khi hoàn tất.

## Done When (Update 2026-03-17 - cng-huj)
- [x] Negative adjustment rows không còn bị reject ở staging.
- [x] Commit không tạo open negative invoice; phần vượt chuyển held credit/carry-forward credit.
- [x] Root MST grouping xử lý đúng case `0310226744-003` với customer gốc `0310226744`.
- [x] Preview hiển thị lý do skip cho các dòng giá trị `0`.
- [x] Test suite targeted cho import/allocate/held-credit pass và bead `cng-huj` được cập nhật đúng trạng thái.

---

## Goal (Update 2026-03-18 - cng-z39)
Đóng bead fix ADVANCE import missing-customer sau khi runtime Docker đang chạy được rebuild lại và có bằng chứng E2E cho đúng luồng nghiệp vụ accountant import -> preview -> commit -> tra cứu ở Advances.

## Tasks (Update 2026-03-18 - cng-z39)
- [x] Thêm Playwright E2E tạo file `ADVANCE` tạm từ template với customer mới và description duy nhất.
- [x] Verify preview modal, commit success alert, và dòng dữ liệu vừa import xuất hiện trong `/advances` với filter `Nguồn dữ liệu = IMPORT`.
- [x] Rebuild/redeploy `api` từ snapshot `HEAD` sạch để tránh kéo theo thay đổi ngoài phạm vi bead.
- [x] Chạy backend targeted verification + health check runtime + Playwright E2E sau rebuild.

## Done When (Update 2026-03-18 - cng-z39)
- [x] Runtime `congno-api` restart thành công trên stack hiện hành.
- [x] `/health` và `/health/ready` đều `ok`.
- [x] Targeted backend tests pass.
- [x] E2E business flow import `ADVANCE` với customer mới pass.

---

## Goal (Update 2026-03-18 - cng-p9o)
Hoàn tất quick fix cho import recovery UX khi batch `STAGING` còn lỗi: kế toán phải nhìn thấy trạng thái bị chặn rõ ràng, biết bước tiếp theo cần làm gì, và không thể bấm commit trong trạng thái dead-end.

## Tasks (Update 2026-03-18 - cng-p9o)
- [x] Reproduce luồng stuck hiện tại từ UI thật và chốt pain points của kế toán.
- [x] Tạo batch recovery state dùng chung để đổi CTA/copy cho `blocked/review/ready`.
- [x] Chặn commit khi còn dòng lỗi và dẫn người dùng sang preview để xem lỗi thay vì cho commit tiếp.
- [x] Bổ sung banner/alert hướng dẫn tiếng Việt trong workspace + preview + history.
- [x] Chạy targeted verification frontend và đồng bộ tracker/notebook.

## Done When (Update 2026-03-18 - cng-p9o)
- [x] Batch lỗi hiển thị trạng thái bị chặn rõ ràng ngay trong workspace/history.
- [x] Commit không còn khả dụng khi batch vẫn có dòng lỗi.
- [x] Người dùng thấy rõ recovery path tối thiểu: xem lỗi, rà soát, hoặc hủy lô.
- [x] Test/frontend build pass và bead `cng-p9o` đủ điều kiện đóng.

---

## Goal (Planned 2026-03-18 - cng-0ye)
Thiết kế lại import recovery workflow theo hướng accountant-first để history/workspace thể hiện đúng mức độ nghiêm trọng và hành động ưu tiên mà không phụ thuộc quá nhiều vào preview modal.

## Tasks (Planned 2026-03-18 - cng-0ye)
- [ ] Mở rộng API/history với validation counts hoặc staging sub-status đủ giàu.
- [ ] Thiết kế lại history/workspace theo nhóm trạng thái `Có lỗi cần sửa` / `Cần rà soát` / `Sẵn sàng ghi`.
- [ ] Đưa next-best-action lên primary CTA theo từng trạng thái.
- [ ] Bổ sung regression/e2e coverage cho workflow mới và lộ trình rollout từ quick fix hiện tại.

## Done When (Planned 2026-03-18 - cng-0ye)
- [ ] Kế toán có thể hiểu tình trạng lô và bước tiếp theo ngay từ history/workspace.
- [ ] Commit chỉ là CTA chính khi batch thực sự sẵn sàng ghi.
- [ ] UI/API/tests phản ánh nhất quán staging sub-status mới.

---

## Goal (Update 2026-03-18 - cng-z25)
Đóng regression của import INVOICE dạng template: dòng âm có ghi chú `Hóa đơn điều chỉnh giảm` phải được stage như `ADJUSTMENT_REDUCTION`, không còn bị skip với lỗi `Số tiền âm không hợp lệ`.

## Tasks (Update 2026-03-18 - cng-z25)
- [x] Xác định root cause bằng cách đối chiếu parser `ReportDetail` và parser `template`.
- [x] Viết failing test cho `ImportInvoiceTemplateParser` với dòng âm adjustment.
- [x] Cập nhật parser template để mirror rule của `cng-huj`: chấp nhận dòng âm hợp lệ, preserve branch MST, thêm root matching MST và `invoice_type`.
- [x] Chạy lại targeted unit tests cho cả hai parser và đồng bộ tracker/notebook.

## Done When (Update 2026-03-18 - cng-z25)
- [x] Template import không còn gắn `NEGATIVE_AMOUNT` cho dòng `Hóa đơn điều chỉnh giảm`.
- [x] RawData chứa `invoice_type = ADJUSTMENT_REDUCTION` và `customer_tax_code_matching`.
- [x] Targeted parser tests pass và bead `cng-z25` đủ điều kiện đóng.

---

## Goal (Update 2026-03-18 - cng-h1d)
Tiếp tục Phase 110 theo hướng permission-first: loại dần các heuristic hardcode theo role ở frontend/backend, ưu tiên những điểm gây lệch hành vi thực tế cho accountant khi auth state đã có `permissions`.

## Tasks (Update 2026-03-18 - cng-h1d)
- [x] Xác định slice frontend còn lệch: `pageLoaders.ts`/`AppShell.tsx` vẫn prefetch theo `roles` dù gating đã dựa trên `permissions`.
- [x] Viết regression test cho permission-only users ở prefetch target selection và prefetch plan.
- [x] Refactor prefetch planner để suy ra effective role từ `roles + permissions`, đồng thời truyền `state.permissions` từ `AppShell`.
- [ ] Tiếp tục rà các gate còn hardcode role trong customer edit, import commit, và admin permission management.
- [ ] Chạy broader verification cho các slice tiếp theo rồi mới cập nhật bead/task trạng thái hoàn tất.

## Done When (Update 2026-03-18 - cng-h1d)
- [x] Permission-only accountant-like users không còn rơi về generic prefetch fallback.
- [x] App shell truyền đủ context `permissions` cho page prefetch planner.
- [ ] Các flow business chính của Phase 110 đều chạy theo permission matrix thay vì role cứng, có test backend/frontend tương ứng.
