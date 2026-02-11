# OpenAPI types cho Frontend

## Mục tiêu
- Sinh type từ Swagger để giảm drift giữa FE/BE.
- Sử dụng `openapi-typescript` để tạo `src/frontend/src/api/openapi.d.ts`.

## Cách dùng (Dev)
1) Chạy backend (có swagger):
   - URL mặc định: `http://localhost:8080/swagger/v1/swagger.json`
2) Tạo types:
   - `npm --prefix src/frontend run openapi:gen`

## Ghi chú
- File sinh tự động: `src/frontend/src/api/openapi.d.ts`
- Nếu đổi base URL swagger, cập nhật script `openapi:gen` trong `src/frontend/package.json`.
