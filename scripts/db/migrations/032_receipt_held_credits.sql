SET search_path TO congno, public;

CREATE TABLE IF NOT EXISTS congno.receipt_held_credits (
    id uuid PRIMARY KEY,
    receipt_id uuid NOT NULL REFERENCES congno.receipts(id) ON DELETE CASCADE,
    original_invoice_id uuid NOT NULL REFERENCES congno.invoices(id),
    original_amount numeric(18,2) NOT NULL CHECK (original_amount >= 0),
    amount_remaining numeric(18,2) NOT NULL CHECK (amount_remaining >= 0),
    status varchar(20) NOT NULL,
    created_by uuid NULL REFERENCES congno.users(id),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    version integer NOT NULL DEFAULT 0
);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.table_constraints
        WHERE table_schema = 'congno'
          AND table_name = 'receipt_held_credits'
          AND constraint_name = 'ck_receipt_held_credits_status'
    ) THEN
        ALTER TABLE congno.receipt_held_credits
            ADD CONSTRAINT ck_receipt_held_credits_status
            CHECK (status IN ('HOLDING', 'PARTIAL', 'REAPPLIED', 'RELEASED'));
    END IF;
END $$;

ALTER TABLE congno.receipt_allocations
    ADD COLUMN IF NOT EXISTS held_credit_id uuid NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.table_constraints
        WHERE table_schema = 'congno'
          AND table_name = 'receipt_allocations'
          AND constraint_name = 'fk_receipt_allocations_held_credit'
    ) THEN
        ALTER TABLE congno.receipt_allocations
            ADD CONSTRAINT fk_receipt_allocations_held_credit
            FOREIGN KEY (held_credit_id)
            REFERENCES congno.receipt_held_credits(id)
            ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_receipt_allocations_held_credit
    ON congno.receipt_allocations(held_credit_id)
    WHERE held_credit_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_receipt_held_credits_receipt
    ON congno.receipt_held_credits(receipt_id);

CREATE INDEX IF NOT EXISTS ix_receipt_held_credits_invoice
    ON congno.receipt_held_credits(original_invoice_id);

CREATE INDEX IF NOT EXISTS ix_receipt_held_credits_status
    ON congno.receipt_held_credits(status, created_at DESC);

DROP TRIGGER IF EXISTS trg_receipt_held_credits_updated_at ON congno.receipt_held_credits;
CREATE TRIGGER trg_receipt_held_credits_updated_at
BEFORE UPDATE ON congno.receipt_held_credits
FOR EACH ROW EXECUTE FUNCTION congno.set_updated_at();
