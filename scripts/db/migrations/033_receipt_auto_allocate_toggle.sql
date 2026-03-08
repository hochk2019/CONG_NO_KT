SET search_path TO congno, public;

ALTER TABLE congno.receipts
    ADD COLUMN IF NOT EXISTS auto_allocate_enabled boolean NOT NULL DEFAULT true;

CREATE INDEX IF NOT EXISTS ix_receipts_auto_allocate_credit
    ON congno.receipts(seller_tax_code, customer_tax_code, receipt_date, created_at)
    WHERE deleted_at IS NULL
      AND status = 'APPROVED'
      AND unallocated_amount > 0
      AND auto_allocate_enabled = true;
