SET search_path TO congno, public;

ALTER TABLE refresh_tokens
ADD COLUMN IF NOT EXISTS absolute_expires_at timestamptz;

UPDATE refresh_tokens
SET absolute_expires_at = COALESCE(
  absolute_expires_at,
  created_at + INTERVAL '90 days'
)
WHERE absolute_expires_at IS NULL;

ALTER TABLE refresh_tokens
ALTER COLUMN absolute_expires_at SET NOT NULL;

CREATE INDEX IF NOT EXISTS ix_refresh_tokens_absolute_expires_at
  ON refresh_tokens(absolute_expires_at);
