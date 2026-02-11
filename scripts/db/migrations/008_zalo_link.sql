ALTER TABLE congno.users
  ADD COLUMN IF NOT EXISTS zalo_user_id varchar(64),
  ADD COLUMN IF NOT EXISTS zalo_linked_at timestamptz;

CREATE TABLE IF NOT EXISTS congno.zalo_link_tokens (
  id uuid PRIMARY KEY,
  user_id uuid NOT NULL REFERENCES congno.users(id),
  code varchar(20) NOT NULL,
  expires_at timestamptz NOT NULL,
  consumed_at timestamptz NULL,
  created_at timestamptz NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_zalo_link_tokens_user_id
  ON congno.zalo_link_tokens(user_id);

CREATE UNIQUE INDEX IF NOT EXISTS uq_zalo_link_tokens_code
  ON congno.zalo_link_tokens(code);

CREATE UNIQUE INDEX IF NOT EXISTS uq_users_zalo_user_id
  ON congno.users(zalo_user_id)
  WHERE zalo_user_id IS NOT NULL;
