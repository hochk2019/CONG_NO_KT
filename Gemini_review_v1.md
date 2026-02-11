# Gemini Review Report (v1) - Cong No Golden

## 1. Tổng quan dự án (Project Overview)
Sau khi tìm hiểu và "khám phá" toàn bộ mã nguồn, tôi nhận thấy đây là một dự án **.NET 8 (Backend)** và **React/Vite (Frontend)** có chất lượng tốt, tuân thủ cấu trúc **Clean Architecture** (Api, Application, Domain, Infrastructure).

Hệ thống được thiết kế với các quyết định kỹ thuật (Technical Decisions) rất bài bản:
-   **Database**: PostgreSQL với các extension hỗ trợ tốt cho tìm kiếm (`pg_trgm`, `unaccent`).
-   **Performance**: Sử dụng `JSONB` cho dữ liệu động (Audit, Import Staging) và Optimistic Concurrency (`version`).
-   **Architecture**: Tách biệt rõ ràng Business Logic và Infrastructure.

## 2. Kết quả Review Chi Tiết (Detailed Findings)

### 2.1. Các vấn đề đã được khắc phục (Resolved Issues)
Dựa trên các "Findings" cũ (trong `Tính năng APP sẽ là.txt`), tôi đã kiểm tra lại code hiện tại và xác nhận đa số đã được giải quyết:

1.  **Quyền hạn (Permissions/Roles)**: 
    -   **Vấn đề cũ**: Quyền Admin/Supervisor bị lệch giữa UI và Backend.
    -   **Code hiện tại**: Đã đồng bộ. Policy trong `Program.cs` và Route Guard trong `App.tsx` đều khớp nhau. Ví dụ: `AuditView` và `AdminHealthView` đều cho phép `Admin` hoặc `Supervisor`.

2.  **Lịch sử Nhập liệu (Import History)**:
    -   **Vấn đề cũ**: Accountant không xem được lịch sử nhập liệu.
    -   **Code hiện tại**: `Program.cs` định nghĩa policy `ImportHistory` bao gồm `Accountant`. UI `AppShell.tsx` cũng mở menu "Nhập liệu" cho `Accountant`.
    -   **Trạng thái**: ✅ Đã sửa (FIXED).

3.  **Tìm kiếm Khách hàng (Customer Search)**:
    -   **Vấn đề cũ**: Dùng `ILIKE` thông thường, chậm và không hỗ trợ tiếng Việt không dấu.
    -   **Code hiện tại**:
        -   **Backend**: `CustomerService.cs` sử dụng `Unaccent` kết hợp với cột `NameSearch`.
        -   **Database**: Đã có migration `005_customers_name_search.sql` tạo cột `name_search` (được chuẩn hóa `lower + unaccent`) và đánh Index `GIN Trigram`.
    -   **Trạng thái**: ✅ Đã sửa và Tối ưu (FIXED & OPTIMIZED).

### 2.2. Các vấn đề tồn tại & Đề xuất cải tiến (Current Issues & Suggestions)

Tuy dự án rất tốt, tôi vẫn phát hiện một số điểm cần bổ sung hoặc nâng cấp:

#### A. Frontend - Progressive Web App (PWA) (Mức độ: Medium)
-   **Vấn đề**: File `index.html` và thư mục `public/` hiện tại chưa có cấu hình `manifest.json` và Service Worker tối thiểu. Điều này được yêu cầu trong tiêu chí "PWA minimal" nhưng chưa thực hiện.
-   **Ảnh hưởng**: Người dùng không thể cài đặt app lên máy tính (Install to Desktop) hoặc ghim vào Taskbar, thiếu trải nghiệm "App-like".
-   **Đề xuất**: 
    1.  Tạo file `manifest.json` (tên, short_name, icons, theme_color).
    2.  Link manifest vào `index.html`.
    3.  Tối thiểu hóa icon (192, 512px).

#### B. Validation Logic (Mức độ: Low)
-   **Vấn đề**: Trong `ImportEndpoints.cs`, một số thông báo lỗi đang được hardcode tiếng Việt (ví dụ: "Không đọc được file import").
-   **Đề xuất**: Nên cân nhắc tách các chuỗi thông báo ra Resource file hoặc Constant để dễ quản lý và đa ngôn ngữ hóa (nếu cần mở rộng sau này). Tuy nhiên với dự án nội bộ thì chấp nhận được.

#### C. Testing Coverage (Mức độ: Improve)
-   **Vấn đề**: Số lượng Unit Test (`Tests.Unit`) và Integration Test (`Tests.Integration`) đã có nhưng có vẻ tập trung vào Business Core (`Allocation`, `PeriodLock`). Các module mới như **Risk** (Cảnh báo rủi ro), **Backup** có thể chưa được phủ test đầy đủ.
-   **Đề xuất**: Bổ sung Unit Test cho các quy tắc rủi ro (`RiskRule`) để đảm bảo logic cảnh báo hoạt động chính xác.

#### D. Database Migrations
-   **Quan sát**: Hệ thống dùng thư viện **DbUp** (`MigrationRunner.cs`) chạy script SQL thủ công thay vì EF Core Migrations tiêu chuẩn.
-   **Đánh giá**: Cách làm này tốt và an toàn cho Production DBA, nhưng cần đảm bảo file script `005...` và code C# `NameSearch` luôn đồng bộ logic. Hiện tại chúng ĐANG đồng bộ.

## 3. Kết luận & Hành động tiếp theo

Dự án đang ở trạng thái **RẤT TỐT (Stable & Clean)**. Các lỗi logic nghiêm trọng đã được xử lý.

**Yêu cầu hành động cho Agent Codex:**
1.  **Thực hiện PWA**: Bổ sung `manifest.json` và cấu hình liên quan.
2.  **Review lại Risk Feature**: Kiểm tra nhanh logic `Risk` trên UI xem data có hiển thị đúng không (Do không chạy được app nên tôi chỉ review tĩnh được code).

Không cần sửa đổi lớn về kiến trúc hay Database. Hệ thống đã sẵn sàng để Verify hoặc Deploy giai đoạn tiếp theo.
