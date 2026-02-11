SET search_path TO congno, public;

-- Seller short name for compact display
ALTER TABLE sellers
  ADD COLUMN IF NOT EXISTS short_name varchar(128);

UPDATE sellers
SET short_name = 'Hoàng Minh'
WHERE seller_tax_code = '2301098313'
  AND (short_name IS NULL OR short_name = '');

UPDATE sellers
SET short_name = 'Hoàng Kim'
WHERE seller_tax_code = '2300328765'
  AND (short_name IS NULL OR short_name = '');

-- Document numbers for advances/receipts
ALTER TABLE advances
  ADD COLUMN IF NOT EXISTS advance_no varchar(64);

ALTER TABLE receipts
  ADD COLUMN IF NOT EXISTS receipt_no varchar(64);

-- Search indexes
CREATE INDEX IF NOT EXISTS ix_invoices_customer_no
  ON invoices(customer_tax_code, invoice_no)
  WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_advances_customer_no
  ON advances(customer_tax_code, advance_no)
  WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_receipts_customer_no
  ON receipts(customer_tax_code, receipt_no)
  WHERE deleted_at IS NULL;
