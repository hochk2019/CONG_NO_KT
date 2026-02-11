CREATE INDEX IF NOT EXISTS ix_import_batches_status_created_at
ON congno.import_batches (status, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_reminder_logs_owner_created_status_channel
ON congno.reminder_logs (owner_user_id, created_at DESC, status, channel);

CREATE INDEX IF NOT EXISTS ix_import_staging_batch_created
ON congno.import_staging_rows (batch_id, created_at DESC);
