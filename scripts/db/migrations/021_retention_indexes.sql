SET search_path TO congno, public;

CREATE INDEX IF NOT EXISTS ix_import_staging_rows_created_at
  ON import_staging_rows(created_at);

CREATE INDEX IF NOT EXISTS ix_refresh_tokens_revoked_at
  ON refresh_tokens(revoked_at);
