# DEPLOYMENT_GUIDE_DOCKER - Cong No Golden

Hướng dẫn chạy hệ thống bằng Docker Compose (Backend + Frontend + PostgreSQL).
Đây là phương thức deploy mặc định của hệ thống hiện tại.

## 1) Chuẩn bị
- Docker Engine + Docker Compose v2.
- Mở port cần dùng: `WEB_PORT` (mặc định 8081), `API_PORT` (mặc định 8080 nếu cần gọi trực tiếp), `POSTGRES_PORT` (mặc định 5432 nếu cần truy cập DB ngoài container).

## 2) Tạo file biến môi trường
Từ thư mục root project:

```powershell
copy .env.example .env
```

`.env.docker.example` chỉ giữ lại cho mục đích tương thích/legacy.

Cập nhật tối thiểu:
- `POSTGRES_PASSWORD`
- `JWT_SECRET`
- `SEED_ADMIN_PASSWORD`
- `BACKUP_HOST_PATH` (Windows staging: `C:/apps/congno/backup/dumps`)
- `REDIS_CONNECTION` (mac dinh `redis:6379`)
- `READ_REPLICA_CONNECTION` (de trong neu chua co replica)
- (Nếu dùng Zalo OA) cập nhật đầy đủ:
  - `ZALO_ENABLED=true`
  - `ZALO_OA_ID`
  - `ZALO_ACCESS_TOKEN`
  - `ZALO_WEBHOOK_TOKEN`
  - `ZALO_API_BASE_URL` (mặc định endpoint OA)
- Giữ mặc định cho auth cookie path:
  - `JWT_REFRESH_COOKIE_PATH=/` (tương thích cả truy cập trực tiếp API `/auth/*` và qua proxy `/api/auth/*`)
- Log backend trong container:
  - `SERILOG_FILE_PATH=/var/lib/congno/logs/api.log`

## 3) Build + chạy stack

```powershell
docker compose up -d --build
```

Kiểm tra trạng thái:

```powershell
docker compose ps
```

## 4) Endpoints
- Frontend: `http://localhost:<WEB_PORT>` (mặc định `http://localhost:8081`)
- Backend health: `http://localhost:<API_PORT>/health` (mặc định `http://localhost:8080/health`)
- Backend readiness: `http://localhost:<API_PORT>/health/ready` (mặc định `http://localhost:8080/health/ready`)

Frontend container dùng Nginx và reverse proxy `/api/*` sang backend container.

## 5) Nâng cấp phiên bản

```powershell
docker compose pull
docker compose up -d --build
```

## 6) Dừng hệ thống

```powershell
docker compose down
```

Xóa luôn volume DB:

```powershell
docker compose down -v
```

## 7) Ghi chú vận hành
- Compose đã bật `Migrations__Enabled=true`, API sẽ tự apply script trong `scripts/db/migrations` khi khởi động.
- Redis duoc bat san trong compose va dung cho read-model cache.
- Neu chua co read replica, de `READ_REPLICA_CONNECTION` rong de API fallback ve primary.
- Với môi trường production có HTTPS, đặt:
  - `JWT_REFRESH_COOKIE_SECURE=true`
  - `JWT_REFRESH_COOKIE_SAMESITE=Strict`
- Nếu frontend truy cập backend qua Nginx `/api`, `JWT_REFRESH_COOKIE_PATH=/` vẫn hoạt động đúng và đồng thời giữ tương thích cho truy cập API trực tiếp khi cần.
- Nếu cần chỉ expose web ra ngoài, có thể bỏ map port `API_PORT` và truy cập API qua `/api` từ frontend.
- Bắt buộc mount migrations vào API container: `./scripts/db/migrations:/app/scripts/db/migrations:ro`.

## 8) Rollout checklist (staging/prod)

1. Tạo backup trước rollout.
2. Chạy `docker compose up -d --build`.
3. Kiểm tra container:
   - `docker compose ps`
   - `docker compose logs api --tail=200`
4. Kiểm tra endpoint:
   - `GET /health`
   - `GET /health/ready`
   - Frontend home page
5. (Nếu bật ML risk) smoke admin endpoints:
   - `GET /admin/risk-ml/models`
   - `GET /admin/risk-ml/runs`
   - `GET /admin/risk-ml/active`
   - `POST /admin/risk-ml/train`
   - `POST /admin/risk-ml/models/{id}/activate`
