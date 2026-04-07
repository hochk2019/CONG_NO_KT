SET search_path TO congno, public;

CREATE TABLE IF NOT EXISTS permissions (
    id serial PRIMARY KEY,
    code varchar(128) NOT NULL UNIQUE,
    name varchar(256) NOT NULL
);

CREATE TABLE IF NOT EXISTS role_permissions (
    role_id integer NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    permission_id integer NOT NULL REFERENCES permissions(id) ON DELETE CASCADE,
    PRIMARY KEY (role_id, permission_id)
);

CREATE INDEX IF NOT EXISTS ix_role_permissions_permission_id
    ON role_permissions(permission_id);
