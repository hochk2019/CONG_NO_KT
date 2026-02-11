-- Risk alerts + reminders
SET search_path TO congno, public;

CREATE TABLE IF NOT EXISTS risk_rules (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  level varchar(16) NOT NULL,
  min_overdue_days int NOT NULL DEFAULT 0,
  min_overdue_ratio numeric(9,6) NOT NULL DEFAULT 0,
  min_late_count int NOT NULL DEFAULT 0,
  is_active boolean NOT NULL DEFAULT true,
  sort_order int NOT NULL DEFAULT 0,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT ck_risk_rules_level CHECK (level IN ('VERY_HIGH','HIGH','MEDIUM','LOW'))
);
CREATE TRIGGER trg_risk_rules_updated_at
BEFORE UPDATE ON risk_rules FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE UNIQUE INDEX IF NOT EXISTS uq_risk_rules_level ON risk_rules(level);

CREATE TABLE IF NOT EXISTS reminder_settings (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  singleton boolean NOT NULL DEFAULT true,
  enabled boolean NOT NULL DEFAULT true,
  frequency_days int NOT NULL DEFAULT 7,
  channels jsonb NOT NULL DEFAULT '["IN_APP","ZALO"]'::jsonb,
  target_levels jsonb NOT NULL DEFAULT '["VERY_HIGH","HIGH","MEDIUM"]'::jsonb,
  last_run_at timestamptz,
  next_run_at timestamptz,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT uq_reminder_settings_singleton UNIQUE (singleton)
);
CREATE TRIGGER trg_reminder_settings_updated_at
BEFORE UPDATE ON reminder_settings FOR EACH ROW EXECUTE FUNCTION set_updated_at();

CREATE TABLE IF NOT EXISTS reminder_logs (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  customer_tax_code varchar(20) NOT NULL REFERENCES customers(tax_code),
  owner_user_id uuid REFERENCES users(id),
  risk_level varchar(16) NOT NULL,
  channel varchar(16) NOT NULL,
  status varchar(16) NOT NULL,
  message varchar(512),
  error_detail varchar(512),
  sent_at timestamptz,
  created_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT ck_reminder_logs_level CHECK (risk_level IN ('VERY_HIGH','HIGH','MEDIUM','LOW')),
  CONSTRAINT ck_reminder_logs_channel CHECK (channel IN ('IN_APP','ZALO')),
  CONSTRAINT ck_reminder_logs_status CHECK (status IN ('SENT','FAILED','SKIPPED'))
);
CREATE INDEX IF NOT EXISTS ix_reminder_logs_created ON reminder_logs(created_at);
CREATE INDEX IF NOT EXISTS ix_reminder_logs_owner ON reminder_logs(owner_user_id);
CREATE INDEX IF NOT EXISTS ix_reminder_logs_customer ON reminder_logs(customer_tax_code);

CREATE TABLE IF NOT EXISTS notifications (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  title varchar(128) NOT NULL,
  body varchar(512),
  severity varchar(16) NOT NULL DEFAULT 'INFO',
  source varchar(32) NOT NULL DEFAULT 'RISK',
  metadata jsonb,
  created_at timestamptz NOT NULL DEFAULT now(),
  read_at timestamptz,
  CONSTRAINT ck_notifications_severity CHECK (severity IN ('INFO','WARN','ALERT'))
);
CREATE INDEX IF NOT EXISTS ix_notifications_user ON notifications(user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_notifications_unread ON notifications(user_id) WHERE read_at IS NULL;

INSERT INTO risk_rules (level, min_overdue_days, min_overdue_ratio, min_late_count, sort_order)
VALUES
  ('VERY_HIGH', 90, 0.60, 4, 1),
  ('HIGH', 60, 0.40, 3, 2),
  ('MEDIUM', 30, 0.20, 2, 3),
  ('LOW', 0, 0.00, 0, 4)
ON CONFLICT (level) DO NOTHING;

INSERT INTO reminder_settings (enabled, frequency_days, channels, target_levels, next_run_at)
VALUES (true, 7, '["IN_APP","ZALO"]'::jsonb, '["VERY_HIGH","HIGH","MEDIUM"]'::jsonb, now() + interval '7 days')
ON CONFLICT (singleton) DO NOTHING;
