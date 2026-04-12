SET search_path TO congno, public;

ALTER TABLE backup_settings
  ADD COLUMN IF NOT EXISTS offsite_enabled boolean NOT NULL DEFAULT false,
  ADD COLUMN IF NOT EXISTS offsite_provider text,
  ADD COLUMN IF NOT EXISTS google_drive_folder_id text,
  ADD COLUMN IF NOT EXISTS offsite_retention_count int NOT NULL DEFAULT 10,
  ADD COLUMN IF NOT EXISTS upload_after_backup boolean NOT NULL DEFAULT false;

CREATE TABLE IF NOT EXISTS backup_offsite_connections (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  provider text NOT NULL,
  status text NOT NULL DEFAULT 'disconnected',
  encrypted_refresh_token text,
  google_drive_folder_id text,
  connected_at timestamptz,
  last_validated_at timestamptz,
  last_error text,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_backup_offsite_connections_provider
  ON backup_offsite_connections(provider);

DROP TRIGGER IF EXISTS trg_backup_offsite_connections_updated_at ON backup_offsite_connections;
CREATE TRIGGER trg_backup_offsite_connections_updated_at
BEFORE UPDATE ON backup_offsite_connections FOR EACH ROW EXECUTE FUNCTION set_updated_at();

CREATE TABLE IF NOT EXISTS backup_offsite_uploads (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  backup_job_id uuid REFERENCES backup_jobs(id) ON DELETE CASCADE,
  provider text NOT NULL,
  status text NOT NULL DEFAULT 'queued',
  remote_file_id text,
  remote_checksum text,
  remote_file_size bigint,
  attempt_count int NOT NULL DEFAULT 0,
  error_message text,
  created_at timestamptz NOT NULL DEFAULT now(),
  queued_at timestamptz,
  completed_at timestamptz,
  updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_backup_offsite_uploads_backup_job_id
  ON backup_offsite_uploads(backup_job_id);

CREATE INDEX IF NOT EXISTS ix_backup_offsite_uploads_status_created_at
  ON backup_offsite_uploads(status, created_at DESC);

DROP TRIGGER IF EXISTS trg_backup_offsite_uploads_updated_at ON backup_offsite_uploads;
CREATE TRIGGER trg_backup_offsite_uploads_updated_at
BEFORE UPDATE ON backup_offsite_uploads FOR EACH ROW EXECUTE FUNCTION set_updated_at();
