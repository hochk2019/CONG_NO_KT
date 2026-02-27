CREATE TABLE IF NOT EXISTS congno.reminder_response_states (
    id uuid PRIMARY KEY,
    customer_tax_code text NOT NULL,
    channel text NOT NULL,
    response_status text NOT NULL DEFAULT 'NO_RESPONSE',
    latest_response_at timestamptz NULL,
    escalation_locked boolean NOT NULL DEFAULT false,
    attempt_count integer NOT NULL DEFAULT 0,
    current_escalation_level integer NOT NULL DEFAULT 1,
    last_sent_at timestamptz NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_reminder_response_states_customer_channel
    ON congno.reminder_response_states (customer_tax_code, channel);

CREATE INDEX IF NOT EXISTS ix_reminder_response_states_response_status
    ON congno.reminder_response_states (response_status);

CREATE INDEX IF NOT EXISTS ix_reminder_response_states_latest_response_at
    ON congno.reminder_response_states (latest_response_at);
