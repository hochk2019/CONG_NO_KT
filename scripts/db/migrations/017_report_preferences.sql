BEGIN;

CREATE TABLE IF NOT EXISTS congno.user_report_preferences (
    user_id uuid NOT NULL,
    report_key text NOT NULL DEFAULT 'reports',
    preferences jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_user_report_preferences PRIMARY KEY (user_id, report_key),
    CONSTRAINT fk_user_report_preferences_user FOREIGN KEY (user_id)
        REFERENCES congno.users(id) ON DELETE CASCADE
);

COMMIT;
