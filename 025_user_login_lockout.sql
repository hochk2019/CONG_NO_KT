SET search_path TO congno, public;

ALTER TABLE users
    ADD COLUMN IF NOT EXISTS failed_login_count int NOT NULL DEFAULT 0;

ALTER TABLE users
    ADD COLUMN IF NOT EXISTS last_failed_login_at timestamptz NULL;

ALTER TABLE users
    ADD COLUMN IF NOT EXISTS lockout_end_at timestamptz NULL;

CREATE INDEX IF NOT EXISTS ix_users_lockout_end_at ON users(lockout_end_at);
