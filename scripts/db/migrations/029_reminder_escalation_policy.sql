SET search_path TO congno, public;

ALTER TABLE reminder_settings
    ADD COLUMN IF NOT EXISTS escalation_max_attempts integer NOT NULL DEFAULT 3;

ALTER TABLE reminder_settings
    ADD COLUMN IF NOT EXISTS escalation_cooldown_hours integer NOT NULL DEFAULT 24;

ALTER TABLE reminder_settings
    ADD COLUMN IF NOT EXISTS escalate_to_supervisor_after integer NOT NULL DEFAULT 2;

ALTER TABLE reminder_settings
    ADD COLUMN IF NOT EXISTS escalate_to_admin_after integer NOT NULL DEFAULT 3;

ALTER TABLE reminder_logs
    ADD COLUMN IF NOT EXISTS escalation_level integer NOT NULL DEFAULT 1;

ALTER TABLE reminder_logs
    ADD COLUMN IF NOT EXISTS escalation_reason text NULL;

CREATE INDEX IF NOT EXISTS ix_reminder_logs_customer_channel_created
    ON reminder_logs(customer_tax_code, channel, created_at DESC);
