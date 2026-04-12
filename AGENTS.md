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

## GitNexus (bắt buộc cho repo này)

- Repo này đã được GitNexus index với tên: **`CONG_NO_KT`**.
- Khi cần hiểu code, debug, đánh giá ảnh hưởng, hoặc refactor: **ưu tiên GitNexus trước** khi grep/đọc tay lan man.
- Bắt đầu bằng resource `gitnexus://repo/CONG_NO_KT/context` để kiểm tra context và độ mới của index.
- Khi code chưa quen: dùng `mcp__gitnexus__query` theo concept/luồng nghiệp vụ trước, rồi mới mở symbol/file cụ thể.
- Trước khi sửa **function/class/method** đang tồn tại: **phải** chạy `mcp__gitnexus__impact` với `direction: "upstream"` cho symbol đó để biết blast radius.
- Nếu `impact` trả về mức **HIGH** hoặc **CRITICAL**: phải báo rõ risk và phạm vi ảnh hưởng trước khi tiếp tục sửa.
- Khi cần xem caller/callee/process của một symbol: dùng `mcp__gitnexus__context`.
- Khi đổi tên symbol: dùng `mcp__gitnexus__rename` với `dry_run: true` trước; **không** rename bằng find/replace thuần text.
- Sau khi thay đổi code và trước khi kết thúc task có sửa code: chạy `mcp__gitnexus__detect_changes` để đối chiếu scope ảnh hưởng thực tế.
- Nếu index stale hoặc vừa có thay đổi lớn chưa được GitNexus cập nhật: chạy `npx -y gitnexus@latest analyze` tại root repo.

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

<!-- gitnexus:start -->
# GitNexus — Code Intelligence

This project is indexed by GitNexus as **CONG_NO_KT** (7262 symbols, 19201 relationships, 300 execution flows). Use the GitNexus MCP tools to understand code, assess impact, and navigate safely.

> If any GitNexus tool warns the index is stale, run `npx gitnexus analyze` in terminal first.

## Always Do

- **MUST run impact analysis before editing any symbol.** Before modifying a function, class, or method, run `gitnexus_impact({target: "symbolName", direction: "upstream"})` and report the blast radius (direct callers, affected processes, risk level) to the user.
- **MUST run `gitnexus_detect_changes()` before committing** to verify your changes only affect expected symbols and execution flows.
- **MUST warn the user** if impact analysis returns HIGH or CRITICAL risk before proceeding with edits.
- When exploring unfamiliar code, use `gitnexus_query({query: "concept"})` to find execution flows instead of grepping. It returns process-grouped results ranked by relevance.
- When you need full context on a specific symbol — callers, callees, which execution flows it participates in — use `gitnexus_context({name: "symbolName"})`.

## When Debugging

1. `gitnexus_query({query: "<error or symptom>"})` — find execution flows related to the issue
2. `gitnexus_context({name: "<suspect function>"})` — see all callers, callees, and process participation
3. `READ gitnexus://repo/CONG_NO_KT/process/{processName}` — trace the full execution flow step by step
4. For regressions: `gitnexus_detect_changes({scope: "compare", base_ref: "main"})` — see what your branch changed

## When Refactoring

- **Renaming**: MUST use `gitnexus_rename({symbol_name: "old", new_name: "new", dry_run: true})` first. Review the preview — graph edits are safe, text_search edits need manual review. Then run with `dry_run: false`.
- **Extracting/Splitting**: MUST run `gitnexus_context({name: "target"})` to see all incoming/outgoing refs, then `gitnexus_impact({target: "target", direction: "upstream"})` to find all external callers before moving code.
- After any refactor: run `gitnexus_detect_changes({scope: "all"})` to verify only expected files changed.

## Never Do

- NEVER edit a function, class, or method without first running `gitnexus_impact` on it.
- NEVER ignore HIGH or CRITICAL risk warnings from impact analysis.
- NEVER rename symbols with find-and-replace — use `gitnexus_rename` which understands the call graph.
- NEVER commit changes without running `gitnexus_detect_changes()` to check affected scope.

## Tools Quick Reference

| Tool | When to use | Command |
|------|-------------|---------|
| `query` | Find code by concept | `gitnexus_query({query: "auth validation"})` |
| `context` | 360-degree view of one symbol | `gitnexus_context({name: "validateUser"})` |
| `impact` | Blast radius before editing | `gitnexus_impact({target: "X", direction: "upstream"})` |
| `detect_changes` | Pre-commit scope check | `gitnexus_detect_changes({scope: "staged"})` |
| `rename` | Safe multi-file rename | `gitnexus_rename({symbol_name: "old", new_name: "new", dry_run: true})` |
| `cypher` | Custom graph queries | `gitnexus_cypher({query: "MATCH ..."})` |

## Impact Risk Levels

| Depth | Meaning | Action |
|-------|---------|--------|
| d=1 | WILL BREAK — direct callers/importers | MUST update these |
| d=2 | LIKELY AFFECTED — indirect deps | Should test |
| d=3 | MAY NEED TESTING — transitive | Test if critical path |

## Resources

| Resource | Use for |
|----------|---------|
| `gitnexus://repo/CONG_NO_KT/context` | Codebase overview, check index freshness |
| `gitnexus://repo/CONG_NO_KT/clusters` | All functional areas |
| `gitnexus://repo/CONG_NO_KT/processes` | All execution flows |
| `gitnexus://repo/CONG_NO_KT/process/{name}` | Step-by-step execution trace |

## Self-Check Before Finishing

Before completing any code modification task, verify:
1. `gitnexus_impact` was run for all modified symbols
2. No HIGH/CRITICAL risk warnings were ignored
3. `gitnexus_detect_changes()` confirms changes match expected scope
4. All d=1 (WILL BREAK) dependents were updated

## Keeping the Index Fresh

After committing code changes, the GitNexus index becomes stale. Re-run analyze to update it:

```bash
npx gitnexus analyze
```

If the index previously included embeddings, preserve them by adding `--embeddings`:

```bash
npx gitnexus analyze --embeddings
```

To check whether embeddings exist, inspect `.gitnexus/meta.json` — the `stats.embeddings` field shows the count (0 means no embeddings). **Running analyze without `--embeddings` will delete any previously generated embeddings.**

> Claude Code users: A PostToolUse hook handles this automatically after `git commit` and `git merge`.

## CLI

| Task | Read this skill file |
|------|---------------------|
| Understand architecture / "How does X work?" | `.claude/skills/gitnexus/gitnexus-exploring/SKILL.md` |
| Blast radius / "What breaks if I change X?" | `.claude/skills/gitnexus/gitnexus-impact-analysis/SKILL.md` |
| Trace bugs / "Why is X failing?" | `.claude/skills/gitnexus/gitnexus-debugging/SKILL.md` |
| Rename / extract / split / refactor | `.claude/skills/gitnexus/gitnexus-refactoring/SKILL.md` |
| Tools, resources, schema reference | `.claude/skills/gitnexus/gitnexus-guide/SKILL.md` |
| Index, status, clean, wiki CLI commands | `.claude/skills/gitnexus/gitnexus-cli/SKILL.md` |

<!-- gitnexus:end -->
