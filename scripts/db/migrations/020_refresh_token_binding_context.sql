SET search_path TO congno, public;

ALTER TABLE refresh_tokens
ADD COLUMN IF NOT EXISTS device_fingerprint_hash char(64);

ALTER TABLE refresh_tokens
ADD COLUMN IF NOT EXISTS ip_prefix varchar(64);

CREATE INDEX IF NOT EXISTS ix_refresh_tokens_device_fingerprint_hash
  ON refresh_tokens(device_fingerprint_hash);

CREATE INDEX IF NOT EXISTS ix_refresh_tokens_ip_prefix
  ON refresh_tokens(ip_prefix);
