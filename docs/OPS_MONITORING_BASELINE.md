# Monitoring Baseline (Docker Compose)

## Thành phần
- `prometheus`: scrape metrics từ `api:8080/metrics`
- `alertmanager`: nhận alert từ Prometheus
- `loki`: lưu log
- `promtail`: đẩy Docker logs lên Loki
- `grafana`: dashboard + datasource provisioning

## Bật monitoring profile
```bash
docker compose --profile monitoring up -d
```

## Kiểm tra nhanh
```bash
docker compose --profile monitoring ps
docker compose --profile monitoring config
```

## Truy cập nhanh
- Grafana: `http://localhost:3001`
- Prometheus: `http://localhost:9090`
- Alertmanager: `http://localhost:9093`
- Loki API: `http://localhost:3100`

## Dashboard mặc định
- `CongNo Observability`
  - API up
  - Reminder outcomes (30m increase)
  - Receipt approval outcomes (30m increase)
  - Import commit rows (30m increase)
  - Maintenance queue depth + maintenance job duration

## Alerts mặc định
- `CongNoApiDown`: API không scrape được > 2 phút
- `CongNoReminderFailuresDetected`: phát hiện reminder failed trong 15 phút gần nhất

## Metrics mở rộng cho scale readiness
- `congno_maintenance_queue_depth`
- `congno_maintenance_queue_delay_ms`
- `congno_maintenance_job_duration_ms`
- `congno_maintenance_job_total`

## Lưu ý
- `promtail` đọc log từ `/var/lib/docker/containers/*/*-json.log`, phù hợp Linux Docker host.
- Trên Windows Desktop/WSL, cần chỉnh lại path log nếu host không expose thư mục này.
- Nếu host đã dùng cổng `3100`, đặt `LOKI_PORT` khác trước khi chạy, ví dụ:
  - PowerShell: `$env:LOKI_PORT=13100; docker compose --profile monitoring up -d`
  - Linux/macOS: `LOKI_PORT=13100 docker compose --profile monitoring up -d`
- Nếu host đã dùng cổng `3001`, đặt `GRAFANA_PORT` khác trước khi chạy, ví dụ:
  - PowerShell: `$env:GRAFANA_PORT=13001; docker compose --profile monitoring up -d`
  - Linux/macOS: `GRAFANA_PORT=13001 docker compose --profile monitoring up -d`
- Có thể kết hợp cả hai:
  - PowerShell: `$env:LOKI_PORT=13100; $env:GRAFANA_PORT=13001; docker compose --profile monitoring up -d`
