# Agent Instructions

## Quy tắc dự án (bắt buộc)

- Giữ trí nhớ xuyên suốt nhiều lượt trao đổi.
- Duy trì trạng thái công việc trong một “sổ tay” thống nhất.
- Tránh cảnh AI bị quên ngữ cảnh khi context bị nén hay reset.
- Không nhồi quá nhiều code vào trong một module (mỗi module không quá 800 dòng nếu có thể).
- Mỗi module mới đều cần tạo bản test. Nếu code chính thay đổi thì cũng cập nhật test cho phù hợp và ngược lại.
- Chủ động tìm hiểu các nội dung tương tự ở dự án khác tương tự, để đề xuất cải tiến mong muốn của người dùng

## Quy tắc dùng Skills

- Nếu người dùng nhắc tên skill hoặc yêu cầu khớp mô tả skill thì **phải dùng skill** đó.
- Mở `SKILL.md` của skill và làm theo workflow; chỉ load file cần thiết.
- Ưu tiên dùng scripts/assets/template của skill nếu có.

## Beads + task.md (theo dõi công việc)

Dự án dùng **bd (beads)** kết hợp `task.md`.

- Prefix chuẩn: **`cng`**.
- Khi tạo task mới: **thêm `task.md` + tạo bead tương ứng**.
- Khi hoàn thành: **đánh dấu ở cả `task.md` và bead**.

### Quick Reference

```bash
bd ready                              # Find available work
bd show <id>                          # View issue details
bd update <id> --status in_progress   # Claim work
bd close <id>                         # Complete work
bd sync                               # Sync with git
```

## Kết thúc phiên làm việc (tóm tắt)

- Nếu có thay đổi code: chạy test/lint phù hợp và báo kết quả.
- Cập nhật trạng thái bead + `task.md`.
- **Chỉ commit/push khi được người dùng yêu cầu**.
# Beads CLI (WSL)
# - Beads is installed inside WSL (Ubuntu-2204, user sam).
# - Use the Windows wrapper (bd.cmd) or call via WSL directly.
#
# Examples:
#   bd ready --json
#   bd show <id>
#   wsl -d Ubuntu-2204 -u sam -- bd ready --json
#
# If bd is not recognized in Windows, restart the shell after PATH update.
