SET search_path TO congno, public;

CREATE TABLE IF NOT EXISTS risk_score_snapshots (
    id uuid PRIMARY KEY,
    customer_tax_code varchar(32) NOT NULL REFERENCES customers(tax_code) ON DELETE CASCADE,
    as_of_date date NOT NULL,
    score numeric(10,4) NOT NULL,
    signal varchar(16) NOT NULL,
    model_version varchar(64) NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_risk_score_snapshots_signal
        CHECK (signal IN ('LOW', 'MEDIUM', 'HIGH', 'VERY_HIGH')),
    CONSTRAINT uq_risk_score_snapshots_customer_as_of
        UNIQUE (customer_tax_code, as_of_date)
);

CREATE INDEX IF NOT EXISTS ix_risk_score_snapshots_as_of_date
    ON risk_score_snapshots(as_of_date DESC);

CREATE INDEX IF NOT EXISTS ix_risk_score_snapshots_customer_created
    ON risk_score_snapshots(customer_tax_code, created_at DESC);

CREATE TABLE IF NOT EXISTS risk_delta_alerts (
    id uuid PRIMARY KEY,
    customer_tax_code varchar(32) NOT NULL REFERENCES customers(tax_code) ON DELETE CASCADE,
    as_of_date date NOT NULL,
    prev_score numeric(10,4) NOT NULL,
    curr_score numeric(10,4) NOT NULL,
    delta numeric(10,4) NOT NULL,
    threshold numeric(10,4) NOT NULL,
    status varchar(16) NOT NULL DEFAULT 'OPEN',
    detected_at timestamptz NOT NULL DEFAULT now(),
    resolved_at timestamptz NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_risk_delta_alerts_status
        CHECK (status IN ('OPEN', 'ACKED', 'RESOLVED')),
    CONSTRAINT uq_risk_delta_alerts_customer_as_of
        UNIQUE (customer_tax_code, as_of_date)
);

CREATE INDEX IF NOT EXISTS ix_risk_delta_alerts_status_detected
    ON risk_delta_alerts(status, detected_at DESC);

CREATE INDEX IF NOT EXISTS ix_risk_delta_alerts_customer_created
    ON risk_delta_alerts(customer_tax_code, created_at DESC);
