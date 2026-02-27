SET search_path TO congno, public;

CREATE TABLE IF NOT EXISTS risk_ml_models (
    id uuid PRIMARY KEY,
    model_key varchar(64) NOT NULL,
    version int NOT NULL,
    algorithm varchar(64) NOT NULL,
    horizon_days int NOT NULL,
    feature_schema jsonb NOT NULL DEFAULT '{}'::jsonb,
    parameters jsonb NOT NULL DEFAULT '{}'::jsonb,
    metrics jsonb NOT NULL DEFAULT '{}'::jsonb,
    train_sample_count int NOT NULL DEFAULT 0,
    validation_sample_count int NOT NULL DEFAULT 0,
    positive_ratio numeric(8,6) NOT NULL DEFAULT 0,
    is_active boolean NOT NULL DEFAULT false,
    status varchar(32) NOT NULL DEFAULT 'TRAINED',
    trained_at timestamptz NOT NULL DEFAULT now(),
    created_by uuid NULL REFERENCES users(id),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_risk_ml_models_status CHECK (status IN ('TRAINED', 'ACTIVE', 'FAILED', 'ARCHIVED')),
    CONSTRAINT ck_risk_ml_models_horizon CHECK (horizon_days > 0),
    CONSTRAINT uq_risk_ml_models_key_version UNIQUE (model_key, version)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_risk_ml_models_active_per_key
    ON risk_ml_models(model_key)
    WHERE is_active = true;

CREATE INDEX IF NOT EXISTS ix_risk_ml_models_trained_at
    ON risk_ml_models(trained_at DESC);

CREATE INDEX IF NOT EXISTS ix_risk_ml_models_model_key
    ON risk_ml_models(model_key);

CREATE TABLE IF NOT EXISTS risk_ml_training_runs (
    id uuid PRIMARY KEY,
    model_key varchar(64) NOT NULL,
    status varchar(32) NOT NULL,
    started_at timestamptz NOT NULL DEFAULT now(),
    finished_at timestamptz NULL,
    lookback_months int NOT NULL,
    horizon_days int NOT NULL,
    sample_count int NOT NULL DEFAULT 0,
    validation_sample_count int NOT NULL DEFAULT 0,
    positive_ratio numeric(8,6) NOT NULL DEFAULT 0,
    metrics jsonb NULL,
    message text NULL,
    model_id uuid NULL REFERENCES risk_ml_models(id) ON DELETE SET NULL,
    created_by uuid NULL REFERENCES users(id),
    CONSTRAINT ck_risk_ml_training_runs_status CHECK (status IN ('RUNNING', 'SUCCEEDED', 'FAILED', 'SKIPPED')),
    CONSTRAINT ck_risk_ml_training_runs_lookback CHECK (lookback_months > 0),
    CONSTRAINT ck_risk_ml_training_runs_horizon CHECK (horizon_days > 0)
);

CREATE INDEX IF NOT EXISTS ix_risk_ml_training_runs_started_at
    ON risk_ml_training_runs(started_at DESC);

CREATE INDEX IF NOT EXISTS ix_risk_ml_training_runs_model_key
    ON risk_ml_training_runs(model_key);

