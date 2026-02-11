SET search_path TO congno, public;

CREATE TABLE IF NOT EXISTS backup_settings (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  enabled boolean NOT NULL DEFAULT false,
  backup_path text NOT NULL,
  retention_count int NOT NULL DEFAULT 10,
  schedule_day_of_week int NOT NULL DEFAULT 1,
  schedule_time text NOT NULL DEFAULT '02:00',
  timezone text NOT NULL DEFAULT 'UTC',
  pg_bin_path text NOT NULL DEFAULT 'C:\\Program Files\\PostgreSQL\\16\\bin',
  last_run_at timestamptz,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TRIGGER trg_backup_settings_updated_at
BEFORE UPDATE ON backup_settings FOR EACH ROW EXECUTE FUNCTION set_updated_at();

CREATE TABLE IF NOT EXISTS backup_jobs (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  type text NOT NULL,
  status text NOT NULL,
  started_at timestamptz,
  finished_at timestamptz,
  file_name text,
  file_size bigint,
  file_path text,
  created_by uuid,
  error_message text,
  stdout_log text,
  stderr_log text,
  download_token text,
  download_token_expires_at timestamptz,
  created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_backup_jobs_created_at ON backup_jobs(created_at DESC);
CREATE INDEX IF NOT EXISTS ix_backup_jobs_status ON backup_jobs(status);

CREATE TABLE IF NOT EXISTS backup_audit (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  action text NOT NULL,
  actor_id uuid,
  result text NOT NULL,
  details jsonb,
  created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_backup_audit_created_at ON backup_audit(created_at DESC);

CREATE TABLE IF NOT EXISTS backup_uploads (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  file_name text NOT NULL,
  file_size bigint NOT NULL,
  file_path text NOT NULL,
  created_by uuid,
  created_at timestamptz NOT NULL DEFAULT now(),
  expires_at timestamptz NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_backup_uploads_expires_at ON backup_uploads(expires_at);
