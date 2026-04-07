SET search_path TO congno, public;

CREATE TABLE IF NOT EXISTS invoice_reduction_applications (
  id uuid PRIMARY KEY,
  reduction_invoice_id uuid NOT NULL,
  applied_invoice_id uuid NOT NULL,
  amount numeric(18,2) NOT NULL,
  created_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT invoice_reduction_applications_reduction_invoice_id_fkey
    FOREIGN KEY (reduction_invoice_id) REFERENCES invoices(id) ON DELETE CASCADE,
  CONSTRAINT invoice_reduction_applications_applied_invoice_id_fkey
    FOREIGN KEY (applied_invoice_id) REFERENCES invoices(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_invoice_reduction_applications_reduction_invoice_id
  ON invoice_reduction_applications (reduction_invoice_id);

CREATE INDEX IF NOT EXISTS idx_invoice_reduction_applications_applied_invoice_id
  ON invoice_reduction_applications (applied_invoice_id);
