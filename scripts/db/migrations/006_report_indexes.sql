SET search_path TO congno, public;

CREATE INDEX IF NOT EXISTS ix_invoices_issue_date
ON invoices(issue_date) WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_advances_advance_date
ON advances(advance_date) WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_receipts_approved_date
ON receipts(receipt_date) WHERE deleted_at IS NULL AND status = 'APPROVED';

CREATE INDEX IF NOT EXISTS ix_receipts_customer_date_approved
ON receipts(customer_tax_code, receipt_date) WHERE deleted_at IS NULL AND status = 'APPROVED';

CREATE INDEX IF NOT EXISTS ix_receipts_seller_date_approved
ON receipts(seller_tax_code, receipt_date) WHERE deleted_at IS NULL AND status = 'APPROVED';
