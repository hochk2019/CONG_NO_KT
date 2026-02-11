# Dashboard "Luồng tiền thu theo kỳ" - UI/UX Redesign (2026-02-05)

## Understanding summary
- Tái cấu trúc khu vực "Luồng tiền thu theo kỳ" để trực quan, dễ quan sát, tăng tương phản và phân tách rõ dữ liệu.
- Mục tiêu chính: hiển thị rõ Doanh thu, Trả hộ và Tiền thu được theo kỳ (tuần/tháng).
- Đối tượng: người dùng nghiệp vụ theo dõi dòng tiền và công nợ hằng kỳ trên Dashboard.
- Ràng buộc: dùng chung bộ lọc thời gian của Dashboard; không thêm dropdown "Số kỳ".
- Loại bỏ khu vực "Tuổi nợ" trên Dashboard để mở rộng biểu đồ dòng tiền.
- Không thay đổi logic backend ngoài các tham số đã có cho trend; chỉ tổ chức lại hiển thị, màu sắc, layout.

## Assumptions
- Tuần được hiển thị bằng nhãn khoảng ngày (vd: 03–09/02) để trực quan nhất cho người dùng Việt.
- Tiền thu được tính theo ngày duyệt phiếu thu (approved_at), đã có ở backend.
- Đơn vị Triệu/Tỷ chỉ là format hiển thị, không thay đổi dữ liệu gốc.
- Empty-state chuẩn: "Không có dữ liệu trong kỳ đã chọn."

## Decision log
- Chọn phương án A: mở rộng biểu đồ chiếm toàn bộ hàng (loại bỏ "Tuổi nợ") để tăng không gian hiển thị.
- Sử dụng stacked bar cho "Doanh thu + Trả hộ" và bar riêng cho "Tiền thu được" để so sánh trong cùng kỳ.
- Bỏ dropdown "Số kỳ", lấy số kỳ từ bộ lọc thời gian của Dashboard để tránh trùng lặp.
- Nhãn tuần theo khoảng ngày; tooltip hiển thị đầy đủ khoảng ngày và số liệu chi tiết.

## Design details
### Layout & controls
- Card "Luồng tiền thu theo kỳ" chiếm toàn bộ hàng dashboard.
- Toolbar đặt trên cùng: trái là tiêu đề + mô tả ngắn, phải là cụm toggle "Theo tuần/Theo tháng" và "Triệu/Tỷ".
- Legend đặt ngay dưới toolbar (3 màu: Doanh thu, Trả hộ, Tiền thu được).

### Chart
- Stacked bar: Doanh thu + Trả hộ trong cùng một cột, có đường phân cách mảnh để dễ đọc.
- Bar riêng: Tiền thu được đứng cạnh cùng kỳ.
- Gridline mảnh, màu nhạt để tránh nhiễu.
- Nhãn trục X giản lược khi nhiều kỳ (mỗi 2–3 kỳ); tooltip hiển thị đầy đủ.

### Color system (gợi ý)
- Doanh thu: xanh đậm (#2F6BFF)
- Trả hộ: cam ấm (#F59E0B)
- Tiền thu được: xanh lá (#22A06B)

### Data handling
- Kỳ tuần/tháng được chia dựa trên filter thời gian dashboard.
- Mọi kỳ vẫn hiển thị cột 0 để giữ continuity.

## Implementation scope
- Frontend: refactor layout + chart controls + colors/legend/tooltip + remove section "Tuổi nợ".
- Remove dead code liên quan đến "Tuổi nợ" trên Dashboard.
- Không thay đổi backend logic ngoài việc dùng dữ liệu trend hiện có.
