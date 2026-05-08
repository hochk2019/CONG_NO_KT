SET search_path TO congno, public;

ALTER TABLE backup_offsite_uploads
  ADD COLUMN IF NOT EXISTS resumable_session_uri text,
  ADD COLUMN IF NOT EXISTS next_retry_at timestamptz,
  ADD COLUMN IF NOT EXISTS max_attempts int NOT NULL DEFAULT 5;

CREATE INDEX IF NOT EXISTS ix_backup_offsite_uploads_retry
  ON backup_offsite_uploads(status, next_retry_at)
  WHERE status = 'queued';
