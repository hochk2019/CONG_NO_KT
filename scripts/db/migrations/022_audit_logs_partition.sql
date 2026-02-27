SET search_path TO congno, public;

CREATE OR REPLACE FUNCTION ensure_audit_logs_partition(p_month date)
RETURNS void
LANGUAGE plpgsql
AS $$
DECLARE
    month_start date := date_trunc('month', p_month)::date;
    next_month date := (date_trunc('month', p_month) + INTERVAL '1 month')::date;
    partition_name text := format('audit_logs_%s', to_char(month_start, 'YYYYMM'));
    is_partitioned boolean;
BEGIN
    SELECT EXISTS (
        SELECT 1
        FROM pg_partitioned_table pt
        JOIN pg_class c ON c.oid = pt.partrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'congno'
          AND c.relname = 'audit_logs'
    )
    INTO is_partitioned;

    IF NOT is_partitioned THEN
        RETURN;
    END IF;

    IF to_regclass(format('congno.%I', partition_name)) IS NULL THEN
        EXECUTE format(
            'CREATE TABLE congno.%I PARTITION OF congno.audit_logs FOR VALUES FROM (%L) TO (%L)',
            partition_name,
            month_start::timestamptz,
            next_month::timestamptz
        );
    END IF;
END;
$$;

DO $$
DECLARE
    has_audit_table boolean;
    already_partitioned boolean;
    min_month date;
    max_month date;
    cursor_month date;
BEGIN
    SELECT EXISTS (
        SELECT 1
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'congno'
          AND c.relname = 'audit_logs'
          AND c.relkind IN ('r', 'p')
    )
    INTO has_audit_table;

    IF NOT has_audit_table THEN
        RETURN;
    END IF;

    SELECT EXISTS (
        SELECT 1
        FROM pg_partitioned_table pt
        JOIN pg_class c ON c.oid = pt.partrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'congno'
          AND c.relname = 'audit_logs'
    )
    INTO already_partitioned;

    IF already_partitioned THEN
        PERFORM ensure_audit_logs_partition(date_trunc('month', now())::date);
        PERFORM ensure_audit_logs_partition((date_trunc('month', now()) + INTERVAL '1 month')::date);
        PERFORM ensure_audit_logs_partition((date_trunc('month', now()) + INTERVAL '2 month')::date);
        RETURN;
    END IF;

    ALTER TABLE audit_logs RENAME TO audit_logs_legacy;

    CREATE TABLE audit_logs (
      id uuid NOT NULL,
      user_id uuid NULL REFERENCES users(id),
      action varchar(32) NOT NULL,
      entity_type varchar(64) NOT NULL,
      entity_id varchar(64) NOT NULL,
      before_data jsonb,
      after_data jsonb,
      ip_address varchar(64),
      created_at timestamptz NOT NULL DEFAULT now(),
      CONSTRAINT pk_audit_logs PRIMARY KEY (id, created_at)
    ) PARTITION BY RANGE (created_at);

    CREATE TABLE audit_logs_default PARTITION OF audit_logs DEFAULT;

    SELECT
        date_trunc('month', min(created_at))::date,
        date_trunc('month', max(created_at))::date
    INTO min_month, max_month
    FROM audit_logs_legacy;

    IF min_month IS NULL THEN
        min_month := date_trunc('month', now())::date;
        max_month := min_month;
    END IF;

    cursor_month := min_month;
    WHILE cursor_month <= max_month LOOP
        PERFORM ensure_audit_logs_partition(cursor_month);
        cursor_month := (cursor_month + INTERVAL '1 month')::date;
    END LOOP;

    PERFORM ensure_audit_logs_partition((date_trunc('month', now()) + INTERVAL '1 month')::date);
    PERFORM ensure_audit_logs_partition((date_trunc('month', now()) + INTERVAL '2 month')::date);

    INSERT INTO audit_logs (
        id,
        user_id,
        action,
        entity_type,
        entity_id,
        before_data,
        after_data,
        ip_address,
        created_at
    )
    SELECT
        id,
        user_id,
        action,
        entity_type,
        entity_id,
        before_data,
        after_data,
        ip_address,
        created_at
    FROM audit_logs_legacy
    ORDER BY created_at, id;

    DROP TABLE audit_logs_legacy;
END
$$;

CREATE INDEX IF NOT EXISTS ix_audit_logs_created_at
  ON audit_logs(created_at);

CREATE INDEX IF NOT EXISTS ix_audit_logs_entity
  ON audit_logs(entity_type, entity_id);

CREATE INDEX IF NOT EXISTS ix_audit_logs_id
  ON audit_logs(id);
