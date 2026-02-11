-- PostgreSQL schema - Cong No Golden
-- Target: PostgreSQL 16+ (works on 13+)
-- Notes:
-- - UUID PK with gen_random_uuid()
-- - JSONB for audit logs + staging raw_data
-- - "version" int for optimistic concurrency
-- - Soft delete via deleted_at/deleted_by
-- - Optional pg_trgm for fast name search

CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE EXTENSION IF NOT EXISTS unaccent;

CREATE SCHEMA IF NOT EXISTS congno;
SET search_path TO congno, public;

CREATE OR REPLACE FUNCTION set_updated_at() RETURNS trigger AS $$
BEGIN
  NEW.updated_at = now();
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- USERS & ROLES
CREATE TABLE IF NOT EXISTS users (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  username varchar(64) NOT NULL UNIQUE,
  password_hash varchar(255) NOT NULL,
  full_name varchar(128),
  email varchar(128),
  phone varchar(32),
  is_active boolean NOT NULL DEFAULT true,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  version int NOT NULL DEFAULT 0
);
CREATE TRIGGER trg_users_updated_at
BEFORE UPDATE ON users FOR EACH ROW EXECUTE FUNCTION set_updated_at();

CREATE TABLE IF NOT EXISTS roles (
  id serial PRIMARY KEY,
  code varchar(32) NOT NULL UNIQUE,
  name varchar(64) NOT NULL
);

CREATE TABLE IF NOT EXISTS user_roles (
  user_id uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  role_id int NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
  PRIMARY KEY (user_id, role_id)
);

CREATE TABLE IF NOT EXISTS audit_logs (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id uuid NULL REFERENCES users(id),
  action varchar(32) NOT NULL,
  entity_type varchar(64) NOT NULL,
  entity_id varchar(64) NOT NULL,
  before_data jsonb,
  after_data jsonb,
  ip_address varchar(64),
  created_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_audit_logs_created_at ON audit_logs(created_at);
CREATE INDEX IF NOT EXISTS ix_audit_logs_entity ON audit_logs(entity_type, entity_id);

-- SELLERS
CREATE TABLE IF NOT EXISTS sellers (
  seller_tax_code varchar(20) PRIMARY KEY,
  name varchar(256) NOT NULL,
  address varchar(512),
  status varchar(16) NOT NULL DEFAULT 'ACTIVE',
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  version int NOT NULL DEFAULT 0,
  CONSTRAINT ck_sellers_status CHECK (status IN ('ACTIVE','INACTIVE'))
);
CREATE TRIGGER trg_sellers_updated_at
BEFORE UPDATE ON sellers FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- CUSTOMERS
CREATE TABLE IF NOT EXISTS customers (
  tax_code varchar(20) PRIMARY KEY,
  name varchar(256) NOT NULL,
  address varchar(512),
  email varchar(128),
  phone varchar(32),
  accountant_owner_id uuid REFERENCES users(id),
  manager_user_id uuid REFERENCES users(id),
  credit_limit numeric(18,2),
  current_balance numeric(18,2) NOT NULL DEFAULT 0, -- cached; update on commit/approve/void
  payment_terms_days int NOT NULL DEFAULT 30,
  status varchar(16) NOT NULL DEFAULT 'ACTIVE',
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  version int NOT NULL DEFAULT 0,
  CONSTRAINT ck_customers_status CHECK (status IN ('ACTIVE','INACTIVE'))
);
CREATE TRIGGER trg_customers_updated_at
BEFORE UPDATE ON customers FOR EACH ROW EXECUTE FUNCTION set_updated_at();

CREATE INDEX IF NOT EXISTS ix_customers_owner ON customers(accountant_owner_id);
CREATE INDEX IF NOT EXISTS ix_customers_manager ON customers(manager_user_id);
CREATE INDEX IF NOT EXISTS ix_customers_name_trgm ON customers USING gin (name gin_trgm_ops);

-- IMPORT
CREATE TABLE IF NOT EXISTS import_batches (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  type varchar(16) NOT NULL,     -- INVOICE/ADVANCE/RECEIPT
  source varchar(16) NOT NULL,   -- UPLOAD/EASYINVOICE
  period_from date,
  period_to date,
  file_name varchar(260),
  file_hash char(64),
  idempotency_key uuid,          -- client-generated; used to make commit idempotent
  status varchar(16) NOT NULL DEFAULT 'STAGING', -- STAGING/APPROVED/COMMITTED/ROLLED_BACK
  summary_data jsonb,
  created_by uuid REFERENCES users(id),
  created_at timestamptz NOT NULL DEFAULT now(),
  approved_by uuid REFERENCES users(id),
  approved_at timestamptz,
  committed_at timestamptz,
  CONSTRAINT ck_import_type CHECK (type IN ('INVOICE','ADVANCE','RECEIPT')),
  CONSTRAINT ck_import_source CHECK (source IN ('UPLOAD','EASYINVOICE')),
  CONSTRAINT ck_import_status CHECK (status IN ('STAGING','APPROVED','COMMITTED','ROLLED_BACK','CANCELLED'))
);
CREATE INDEX IF NOT EXISTS ix_import_batches_created_at ON import_batches(created_at);
CREATE INDEX IF NOT EXISTS ix_import_batches_filehash ON import_batches(file_hash);
CREATE UNIQUE INDEX IF NOT EXISTS uq_import_batches_idempotency
ON import_batches(idempotency_key) WHERE idempotency_key IS NOT NULL;

CREATE TABLE IF NOT EXISTS import_staging_rows (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  batch_id uuid NOT NULL REFERENCES import_batches(id) ON DELETE CASCADE,
  row_no int NOT NULL,
  raw_data jsonb NOT NULL,
  validation_status varchar(8) NOT NULL, -- OK/WARN/ERROR
  validation_messages jsonb,
  dedup_key varchar(256),
  action_suggestion varchar(16), -- INSERT/SKIP/UPDATE
  mapped_entity_id uuid,
  created_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT ck_validation_status CHECK (validation_status IN ('OK','WARN','ERROR'))
);
CREATE INDEX IF NOT EXISTS ix_staging_batch ON import_staging_rows(batch_id);
CREATE INDEX IF NOT EXISTS ix_staging_status ON import_staging_rows(batch_id, validation_status);

-- INVOICES
CREATE TABLE IF NOT EXISTS invoices (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  seller_tax_code varchar(20) NOT NULL REFERENCES sellers(seller_tax_code),
  customer_tax_code varchar(20) NOT NULL REFERENCES customers(tax_code),
  invoice_template_code varchar(32),
  invoice_series varchar(32),
  invoice_no varchar(64) NOT NULL,
  issue_date date NOT NULL,
  revenue_excl_vat numeric(18,2) NOT NULL DEFAULT 0,
  vat_amount numeric(18,2) NOT NULL DEFAULT 0,
  total_amount numeric(18,2) NOT NULL DEFAULT 0,
  outstanding_amount numeric(18,2) NOT NULL DEFAULT 0, -- cached; update on allocations/voids
  note varchar(512),
  invoice_type varchar(16) NOT NULL DEFAULT 'NORMAL', -- NORMAL/REPLACE/ADJUST
  status varchar(16) NOT NULL DEFAULT 'OPEN',         -- OPEN/PARTIAL/PAID/VOID/DISPUTE
  source_batch_id uuid REFERENCES import_batches(id),
  deleted_at timestamptz,
  deleted_by uuid REFERENCES users(id),
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  version int NOT NULL DEFAULT 0,
  CONSTRAINT ck_invoice_type CHECK (invoice_type IN ('NORMAL','REPLACE','ADJUST')),
  CONSTRAINT ck_invoice_status CHECK (status IN ('OPEN','PARTIAL','PAID','VOID','DISPUTE')),
  CONSTRAINT ck_invoice_adjust_negative CHECK (
    invoice_type <> 'ADJUST'
    OR (revenue_excl_vat <= 0 AND vat_amount <= 0 AND total_amount <= 0)
  )
);
CREATE TRIGGER trg_invoices_updated_at
BEFORE UPDATE ON invoices FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- Unique (dedup) for non-deleted invoices
CREATE UNIQUE INDEX IF NOT EXISTS uq_invoices_dedup
ON invoices(seller_tax_code, customer_tax_code, invoice_series, invoice_no, issue_date)
WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_invoices_customer_date
ON invoices(customer_tax_code, issue_date) WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_invoices_seller_date
ON invoices(seller_tax_code, issue_date) WHERE deleted_at IS NULL;

-- ADVANCES (TRẢ HỘ)
CREATE TABLE IF NOT EXISTS advances (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  seller_tax_code varchar(20) NOT NULL REFERENCES sellers(seller_tax_code),
  customer_tax_code varchar(20) NOT NULL REFERENCES customers(tax_code),
  advance_date date NOT NULL,
  amount numeric(18,2) NOT NULL,
  outstanding_amount numeric(18,2) NOT NULL DEFAULT 0, -- cached; update on allocations/voids
  description varchar(512),
  status varchar(16) NOT NULL DEFAULT 'DRAFT', -- DRAFT/APPROVED/PAID/VOID
  approved_by uuid REFERENCES users(id),
  approved_at timestamptz,
  source_batch_id uuid REFERENCES import_batches(id),
  deleted_at timestamptz,
  deleted_by uuid REFERENCES users(id),
  created_by uuid REFERENCES users(id),
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  version int NOT NULL DEFAULT 0,
  CONSTRAINT ck_advance_status CHECK (status IN ('DRAFT','APPROVED','PAID','VOID'))
);
CREATE TRIGGER trg_advances_updated_at
BEFORE UPDATE ON advances FOR EACH ROW EXECUTE FUNCTION set_updated_at();

CREATE INDEX IF NOT EXISTS ix_advances_customer_date
ON advances(customer_tax_code, advance_date) WHERE deleted_at IS NULL;

-- RECEIPTS (THU TIỀN)
CREATE TABLE IF NOT EXISTS receipts (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  seller_tax_code varchar(20) NOT NULL REFERENCES sellers(seller_tax_code),
  customer_tax_code varchar(20) NOT NULL REFERENCES customers(tax_code),
  receipt_date date NOT NULL,
  applied_period_start date, -- first day of month (e.g. 2025-12-01)
  amount numeric(18,2) NOT NULL,
  method varchar(16) NOT NULL DEFAULT 'BANK', -- BANK/CASH/OTHER
  description varchar(512),
  allocation_mode varchar(16) NOT NULL DEFAULT 'FIFO', -- BY_INVOICE/BY_PERIOD/FIFO/MANUAL
  unallocated_amount numeric(18,2) NOT NULL DEFAULT 0,
  status varchar(16) NOT NULL DEFAULT 'DRAFT', -- DRAFT/APPROVED/VOID
  approved_by uuid REFERENCES users(id),
  approved_at timestamptz,
  source_batch_id uuid REFERENCES import_batches(id),
  deleted_at timestamptz,
  deleted_by uuid REFERENCES users(id),
  created_by uuid REFERENCES users(id),
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  version int NOT NULL DEFAULT 0,
  CONSTRAINT ck_receipt_method CHECK (method IN ('BANK','CASH','OTHER')),
  CONSTRAINT ck_receipt_alloc_mode CHECK (allocation_mode IN ('BY_INVOICE','BY_PERIOD','FIFO','MANUAL')),
  CONSTRAINT ck_receipt_status CHECK (status IN ('DRAFT','APPROVED','VOID'))
);
CREATE TRIGGER trg_receipts_updated_at
BEFORE UPDATE ON receipts FOR EACH ROW EXECUTE FUNCTION set_updated_at();

CREATE INDEX IF NOT EXISTS ix_receipts_customer_date
ON receipts(customer_tax_code, receipt_date) WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_receipts_applied_period
ON receipts(applied_period_start) WHERE deleted_at IS NULL;

CREATE TABLE IF NOT EXISTS receipt_allocations (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  receipt_id uuid NOT NULL REFERENCES receipts(id) ON DELETE CASCADE,
  target_type varchar(16) NOT NULL, -- INVOICE/ADVANCE
  invoice_id uuid REFERENCES invoices(id),
  advance_id uuid REFERENCES advances(id),
  amount numeric(18,2) NOT NULL,
  created_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT ck_alloc_target_type CHECK (target_type IN ('INVOICE','ADVANCE')),
  CONSTRAINT ck_alloc_target_ref CHECK (
    (target_type = 'INVOICE' AND invoice_id IS NOT NULL AND advance_id IS NULL)
    OR
    (target_type = 'ADVANCE' AND advance_id IS NOT NULL AND invoice_id IS NULL)
  )
);
CREATE INDEX IF NOT EXISTS ix_alloc_receipt ON receipt_allocations(receipt_id);
CREATE INDEX IF NOT EXISTS ix_alloc_invoice ON receipt_allocations(invoice_id);
CREATE INDEX IF NOT EXISTS ix_alloc_advance ON receipt_allocations(advance_id);

-- PERIOD LOCKS
CREATE TABLE IF NOT EXISTS period_locks (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  period_type varchar(16) NOT NULL, -- MONTH/QUARTER/YEAR
  period_key varchar(16) NOT NULL,  -- 2025-12 / 2025-Q4 / 2025
  locked_at timestamptz NOT NULL DEFAULT now(),
  locked_by uuid REFERENCES users(id),
  note varchar(256),
  CONSTRAINT ck_period_type CHECK (period_type IN ('MONTH','QUARTER','YEAR')),
  CONSTRAINT uq_period_lock UNIQUE (period_type, period_key)
);

