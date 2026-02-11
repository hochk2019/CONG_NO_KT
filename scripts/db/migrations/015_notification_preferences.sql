BEGIN;

CREATE TABLE IF NOT EXISTS congno.notification_preferences (
    user_id uuid PRIMARY KEY,
    receive_notifications boolean NOT NULL DEFAULT true,
    popup_enabled boolean NOT NULL DEFAULT true,
    popup_severities jsonb NOT NULL DEFAULT '["WARN","CRITICAL"]'::jsonb,
    popup_sources jsonb NOT NULL DEFAULT '["RISK","RECEIPT","IMPORT","SYSTEM"]'::jsonb,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

COMMIT;
