SET search_path TO congno, public;

CREATE TABLE IF NOT EXISTS report_delivery_schedules (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    report_kind varchar(32) NOT NULL,
    report_format varchar(16) NOT NULL,
    cron_expression varchar(128) NOT NULL,
    timezone_id varchar(128) NOT NULL DEFAULT 'UTC',
    recipients jsonb NOT NULL DEFAULT '[]'::jsonb,
    filter_payload jsonb NOT NULL DEFAULT '{}'::jsonb,
    enabled boolean NOT NULL DEFAULT true,
    last_run_at timestamptz NULL,
    next_run_at timestamptz NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_report_delivery_schedules_kind
        CHECK (report_kind IN ('FULL', 'OVERVIEW', 'SUMMARY', 'STATEMENT', 'AGING')),
    CONSTRAINT ck_report_delivery_schedules_format
        CHECK (report_format IN ('XLSX', 'PDF'))
);

CREATE INDEX IF NOT EXISTS ix_report_delivery_schedules_user
    ON report_delivery_schedules(user_id);

CREATE INDEX IF NOT EXISTS ix_report_delivery_schedules_enabled_next_run
    ON report_delivery_schedules(enabled, next_run_at);

CREATE TABLE IF NOT EXISTS report_delivery_runs (
    id uuid PRIMARY KEY,
    schedule_id uuid NOT NULL REFERENCES report_delivery_schedules(id) ON DELETE CASCADE,
    status varchar(16) NOT NULL,
    started_at timestamptz NOT NULL DEFAULT now(),
    finished_at timestamptz NULL,
    error_detail text NULL,
    artifact_meta jsonb NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_report_delivery_runs_status
        CHECK (status IN ('RUNNING', 'SUCCEEDED', 'FAILED', 'SKIPPED'))
);

CREATE INDEX IF NOT EXISTS ix_report_delivery_runs_schedule_started
    ON report_delivery_runs(schedule_id, started_at DESC);
