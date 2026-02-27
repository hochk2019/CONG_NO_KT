# Codex Prompt — Opus Review V5: Đánh giá & Triển khai

## Bối cảnh

File `Opus_review_v5.md` trong repo root chứa 16 vấn đề được phát hiện bởi một AI reviewer khác (Antigravity). Các vấn đề chia thành 4 nhóm: UI Density (VĐ-D1→D5), Code Quality (VĐ-Q1→Q4), Security (VĐ-S1→S3), UX Polish (VĐ-U1→U4).

## QUAN TRỌNG: Thực hiện theo 3 giai đoạn tuần tự

---

### Giai đoạn 1 — RE-EVALUATE (Đánh giá lại)

**MỤC TIÊU:** Đọc `Opus_review_v5.md`, sau đó tự kiểm tra source code thực tế để xác minh từng vấn đề. Ghi kết quả vào `codex_v5_tasks.md`.

Với MỖI vấn đề (VĐ-D1 đến VĐ-U4), hãy:

1. **Đọc mô tả vấn đề** trong `Opus_review_v5.md`
2. **Kiểm tra source code thực tế** tại file được chỉ ra — xác minh giá trị CSS/code hiện tại có đúng như review mô tả không
3. **Đánh giá mức độ nghiêm trọng** — vấn đề có thực sự tồn tại không? Mức ưu tiên có phù hợp không?
4. **Đánh giá giải pháp đề xuất** — giải pháp before/after có hợp lý không? Có cách tốt hơn không? Có rủi ro gì khi áp dụng không?
5. **Ghi kết quả** vào `codex_v5_tasks.md` theo format:

```markdown
## Re-evaluation Log

### VĐ-D1: [Tên vấn đề]
- **Review đúng hay sai:** ✅ Đúng / ⚠️ Đúng một phần / ❌ Sai
- **Giá trị thực tế trong code:** [giá trị tìm thấy]
- **Reviewer nói:** [giá trị reviewer claim]
- **Đánh giá giải pháp:** Hợp lý / Cần điều chỉnh / Không nên làm
- **Ghi chú:** [lý do nếu khác reviewer, rủi ro nếu có, đề xuất thay thế nếu cần]
```

**KHÔNG triển khai bất kỳ thay đổi nào trong giai đoạn này.**

---

### Giai đoạn 2 — PLAN (Lên kế hoạch)

**MỤC TIÊU:** Dựa trên kết quả re-evaluate, lập kế hoạch triển khai chỉ những vấn đề xác nhận là hợp lệ.

Trong `codex_v5_tasks.md`, thêm section:

```markdown
## Implementation Plan

### Sẽ triển khai (đã xác nhận)
- [ ] VĐ-XX: [mô tả ngắn] — [file cần sửa]
...

### Điều chỉnh so với review gốc
- VĐ-XX: [lý do điều chỉnh, giá trị thay thế]
...

### Bỏ qua (không hợp lệ hoặc rủi ro cao)
- VĐ-XX: [lý do bỏ qua]
...
```

---

### Giai đoạn 3 — IMPLEMENT (Triển khai)

**MỤC TIÊU:** Triển khai tất cả items trong danh sách "Sẽ triển khai" theo thứ tự:

1. **UI Density** (VĐ-D*) — ảnh hưởng toàn bộ UI nên làm trước
2. **Security** (VĐ-S*) — hardening trước production
3. **Code Quality** (VĐ-Q*) — refactor
4. **UX Polish** (VĐ-U*) — nice-to-have

Sau mỗi nhóm:
- Chạy `cd src/frontend && npm run build` để verify không lỗi
- Cập nhật `codex_v5_tasks.md`: đánh dấu `[x]` cho items hoàn thành

## Quy tắc chung

1. Giữ nguyên tất cả chức năng hiện có — chỉ thay đổi visual/structural
2. Dark mode (`data-theme='dark'`) phải được kiểm tra khi thay đổi CSS tokens
3. Responsive breakpoints (480px, 768px, 1024px) phải hoạt động đúng sau thay đổi
4. Khi tách file (refactor), đảm bảo import paths chính xác
5. Type safety — không dùng `any`, props phải typed đầy đủ
6. Không sửa tests hiện có trừ khi import path thay đổi do refactor
