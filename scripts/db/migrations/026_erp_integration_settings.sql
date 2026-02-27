SET search_path TO congno, public;

CREATE TABLE IF NOT EXISTS congno.erp_integration_settings (
    id uuid PRIMARY KEY,
    enabled boolean NOT NULL DEFAULT FALSE,
    provider varchar(32) NOT NULL DEFAULT 'MISA',
    base_url text NULL,
    api_key text NULL,
    company_code varchar(64) NULL,
    timeout_seconds integer NOT NULL DEFAULT 15,
    updated_by varchar(64) NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_erp_integration_settings_timeout
        CHECK (timeout_seconds BETWEEN 5 AND 120)
);

CREATE INDEX IF NOT EXISTS ix_erp_integration_settings_updated_at
    ON congno.erp_integration_settings (updated_at DESC);
