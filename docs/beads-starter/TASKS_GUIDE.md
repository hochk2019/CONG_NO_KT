# TASKS_GUIDE.md

Mục tiêu: Chuẩn hóa cách kết hợp task.md (roadmap) và Beads (thực thi) để tránh quên/ngắt mạch khi agent bị compact context.

## 1) Nguyên tắc chung
- task.md: chỉ dùng cho roadmap/phase/epic ở mức cao.
- Beads: dùng cho công việc thực thi, có trạng thái và ghi chú chi tiết.
- Không đoán trạng thái; luôn truy vấn Beads trước khi làm.

## 2) Ba mức áp dụng (chọn 1)
### 2.1 Phiên bản NHẸ (khuyến nghị cho dự án nhỏ)
- task.md: giữ đầy đủ mục tiêu lớn + checklist chính.
- Beads: chỉ tạo cho việc đang làm (in_progress) và việc phát sinh quan trọng.
- Notes bắt buộc: COMPLETED / IN PROGRESS / NEXT / FILES.

Quy trình:
1) bd ready --json
2) bd update <id> --status in_progress
3) bd update <id> --notes "COMPLETED: ...\nIN PROGRESS: ...\nNEXT: ...\nFILES: ..."
4) bd close <id> --reason "Done"
5) Cập nhật task.md nếu liên quan phase.

### 2.2 Phiên bản VỪA (dự án trung bình)
- task.md: chỉ giữ Phase + Epic.
- Beads: tạo hết task con (implementation tasks), có dependency.
- Mỗi task có priority rõ (P0..P4).

Quy trình:
1) bd create "Epic" -t epic -p 1
2) bd create "Task A" -p 1 --parent <epic>
3) bd dep add <task> <blocker>
4) bd ready --json => làm theo priority.

### 2.3 Phiên bản NẶNG (dự án lớn/đa người)
- task.md: chỉ là roadmap tổng quan (milestones).
- Beads: quản lý toàn bộ task + dependency + notes chi tiết.
- Bắt buộc dùng Beads UI để theo dõi (bdui start).
- Mỗi task có owner/assignee (ghi trong notes) và audit rõ.

Khuyến nghị thêm:
- Dùng discovered-from dependency khi phát hiện bug trong lúc làm.
- Quy định format notes chuẩn hóa để dễ audit.

## 3) Mẫu notes chuẩn
COMPLETED:
- ...
IN PROGRESS:
- ...
NEXT:
- ...
FILES:
- ...
DECISIONS:
- ...
BLOCKERS:
- ...

## 4) Checklist nhanh cho Agent
- Nếu quên bối cảnh: bd ready --json -> bd show <id>
- Không sửa task.md khi chưa close bead.
- Luôn ghi notes trước khi dừng phiên.

## 5) Hướng dẫn khởi tạo dự án mới
Xem chi tiết tại: `NEW_PROJECT_SETUP.md`
