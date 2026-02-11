ALTER TABLE congno.receipts
    ADD COLUMN IF NOT EXISTS allocation_status TEXT NOT NULL DEFAULT 'UNALLOCATED',
    ADD COLUMN IF NOT EXISTS allocation_priority TEXT NOT NULL DEFAULT 'ISSUE_DATE',
    ADD COLUMN IF NOT EXISTS allocation_targets JSONB NULL,
    ADD COLUMN IF NOT EXISTS allocation_source TEXT NULL,
    ADD COLUMN IF NOT EXISTS allocation_suggested_at TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS last_reminder_at TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS reminder_disabled_at TIMESTAMPTZ NULL;

ALTER TABLE congno.receipts
    DROP CONSTRAINT IF EXISTS ck_receipts_allocation_status;

ALTER TABLE congno.receipts
    ADD CONSTRAINT ck_receipts_allocation_status
    CHECK (allocation_status IN (
        'UNALLOCATED',
        'SELECTED',
        'SUGGESTED',
        'ALLOCATED',
        'PARTIAL',
        'VOID'
    ));

ALTER TABLE congno.receipts
    DROP CONSTRAINT IF EXISTS ck_receipts_allocation_priority;

ALTER TABLE congno.receipts
    ADD CONSTRAINT ck_receipts_allocation_priority
    CHECK (allocation_priority IN ('ISSUE_DATE', 'DUE_DATE'));

UPDATE congno.receipts
SET allocation_status = CASE
    WHEN status = 'APPROVED' AND COALESCE(unallocated_amount, 0) > 0 THEN 'PARTIAL'
    WHEN status = 'APPROVED' THEN 'ALLOCATED'
    WHEN status = 'VOID' THEN 'VOID'
    ELSE 'UNALLOCATED'
END;
