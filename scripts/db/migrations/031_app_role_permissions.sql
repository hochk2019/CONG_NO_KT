-- Ensure application role has stable permissions on business schema objects
-- and DbUp journal table. This prevents runtime 500 caused by missing grants.

GRANT USAGE ON SCHEMA congno TO congno_app;
GRANT USAGE ON SCHEMA public TO congno_app;

GRANT SELECT, INSERT, UPDATE, DELETE
ON ALL TABLES IN SCHEMA congno
TO congno_app;

GRANT USAGE, SELECT, UPDATE
ON ALL SEQUENCES IN SCHEMA congno
TO congno_app;

-- Use current migration role so this works in clusters where the superuser
-- is not named "postgres" (for example POSTGRES_USER=congno_app).
ALTER DEFAULT PRIVILEGES IN SCHEMA congno
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO congno_app;

ALTER DEFAULT PRIVILEGES IN SCHEMA congno
GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO congno_app;

GRANT SELECT, INSERT, UPDATE, DELETE
ON TABLE public.schemaversions
TO congno_app;
