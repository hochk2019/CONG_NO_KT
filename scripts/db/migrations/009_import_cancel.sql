ALTER TABLE congno.import_batches
ADD COLUMN IF NOT EXISTS cancelled_at timestamptz NULL;

ALTER TABLE congno.import_batches
ADD COLUMN IF NOT EXISTS cancelled_by uuid NULL;

ALTER TABLE congno.import_batches
ADD COLUMN IF NOT EXISTS cancel_reason text NULL;
