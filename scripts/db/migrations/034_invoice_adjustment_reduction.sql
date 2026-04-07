SET search_path TO congno, public;

ALTER TABLE invoices
  ALTER COLUMN invoice_type TYPE varchar(32);

ALTER TABLE invoices
  DROP CONSTRAINT IF EXISTS ck_invoice_type;

ALTER TABLE invoices
  ADD CONSTRAINT ck_invoice_type
  CHECK (invoice_type IN ('NORMAL','REPLACE','ADJUST','ADJUSTMENT_REDUCTION'));

ALTER TABLE invoices
  DROP CONSTRAINT IF EXISTS ck_invoice_adjust_negative;

ALTER TABLE invoices
  ADD CONSTRAINT ck_invoice_adjust_negative
  CHECK (
    invoice_type NOT IN ('ADJUST','ADJUSTMENT_REDUCTION')
    OR (revenue_excl_vat <= 0 AND vat_amount <= 0 AND total_amount <= 0)
  );
