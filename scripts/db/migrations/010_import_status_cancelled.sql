ALTER TABLE congno.import_batches
DROP CONSTRAINT IF EXISTS ck_import_status;

ALTER TABLE congno.import_batches
ADD CONSTRAINT ck_import_status
CHECK (status IN ('STAGING','APPROVED','COMMITTED','ROLLED_BACK','CANCELLED'));
