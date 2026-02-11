# NEW_PROJECT_SETUP.md

Hướng dẫn khởi tạo dự án mới với **task.md + Beads** (giúp AI không quên việc).

Mục tiêu:
- Có roadmap rõ ràng trong `task.md`
- Có hệ thống thực thi chi tiết bằng Beads
- AI luôn biết "đang làm gì" kể cả sau khi bị compact context

---

## A. Chuẩn bị môi trường (1 lần)

### 1) Cài Beads CLI
Windows (WSL hoặc Git Bash):
```
curl -fsSL https://raw.githubusercontent.com/steveyegge/beads/main/scripts/install.sh | bash
```

macOS/Linux:
```
curl -fsSL https://raw.githubusercontent.com/steveyegge/beads/main/scripts/install.sh | bash
```

Kiểm tra:
```
bd --version
```

### 2) (Tuỳ chọn) Cài Beads UI
```
npm i -g beads-ui
```

Chạy UI:
```
bdui start --open
```

---

## B. Khởi tạo dự án mới từ đầu

### 1) Tạo repo mới + cấu trúc cơ bản
- Tạo thư mục dự án
- Khởi tạo git (nếu chưa có)
- Copy template từ `docs/beads-starter/` (trong repo này)

Ví dụ:
```
mkdir my-project
cd my-project

git init

# Copy template (tuỳ theo OS)
# Windows PowerShell:
Copy-Item -Recurse -Force "<path>/docs/beads-starter/*" .
```

Sau khi copy, bạn sẽ có:
- `task.md`
- `worklog.md`
- `.beads/` (placeholder)
- `TASKS_GUIDE.md`

### 2) Khởi tạo Beads trong repo
Chọn prefix ngắn (2–5 ký tự) để ID gọn.
```
bd init --prefix myp
```

### 3) Thiết lập hooks cho agent (nếu dùng Claude/Codex)
```
bd setup claude
```

### 4) Commit khởi tạo
```
git add .
git commit -m "init task.md + beads"
```

---

## C. Cách dùng cho dự án mới (luồng chuẩn)

### 1) Roadmap ở `task.md`
- Chỉ ghi Phase / Epic / mục tiêu lớn.
- Không ghi từng task chi tiết ở đây.

### 2) Task thực thi → Beads
Ví dụ tạo Epic và task con:
```
bd create "Auth System" -t epic -p 1
bd create "Login API" -p 1 --parent <epic-id>
bd create "JWT Middleware" -p 1 --parent <epic-id>

bd dep add <jwt-task> <login-task>
```

### 3) AI/Agent workflow (bắt buộc)
```
# 1) Hỏi task sẵn sàng
bd ready --json

# 2) Claim task
bd update <id> --status in_progress

# 3) Ghi note tiến độ
bd update <id> --notes "
COMPLETED: ...
IN PROGRESS: ...
NEXT: ...
FILES: ...
DECISIONS: ...
BLOCKERS: ...
"

# 4) Xong việc
bd close <id> --reason "Done"
```

---

## D. Dặn AI ngay từ đầu (Prompt mẫu)

Bạn có thể copy đoạn dưới vào phần chỉ dẫn cho AI (system/developer prompt hoặc README):

```
QUY ƯỚC TASK:
- task.md chỉ giữ roadmap/phase/epic.
- Mọi task thực thi đều quản lý bằng Beads.
- Trước khi làm, luôn chạy: bd ready --json
- Nếu không nhớ bối cảnh, chạy: bd show <id>
- Luôn cập nhật notes trước khi kết thúc phiên.
- Không đoán, luôn query Beads.
```

---

## E. Cách giữ trí nhớ cho AI

### 1) Notes là bắt buộc
Mỗi task Beads phải có notes theo format:
```
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
```

### 2) worklog.md (tuỳ chọn)
- Ghi các quyết định lớn, vấn đề phát sinh quan trọng.
- Link đến bead ID.

---

## F. Ví dụ thực tế (mini)

1) task.md (Phase lớn)
```
## Phase 1 - Core
- [ ] Auth System
```

2) Beads
```
bd create "Auth System" -t epic -p 1
bd create "Login API" -p 1 --parent <epic>
bd create "JWT Middleware" -p 1 --parent <epic>
bd dep add <jwt> <login>
```

3) Agent làm việc
```
bd ready --json
bd update <login> --status in_progress
bd update <login> --notes "..."
bd close <login> --reason "Done"
```

---

## G. Gợi ý áp dụng theo mức dự án
- Dự án nhỏ → dùng phiên bản **NHẸ**
- Dự án trung bình → dùng phiên bản **VỪA**
- Dự án lớn/đa người → dùng phiên bản **NẶNG** + Beads UI

Chi tiết xem: `TASKS_GUIDE.md`
