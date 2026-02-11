SET search_path TO congno, public;

ALTER TABLE customers ADD COLUMN IF NOT EXISTS name_search text;

UPDATE customers
SET name_search = lower(unaccent(name))
WHERE name_search IS NULL;

CREATE OR REPLACE FUNCTION set_customer_name_search() RETURNS trigger AS $$
BEGIN
  NEW.name_search = lower(unaccent(COALESCE(NEW.name, '')));
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_customers_name_search ON customers;
CREATE TRIGGER trg_customers_name_search
BEFORE INSERT OR UPDATE OF name ON customers
FOR EACH ROW EXECUTE FUNCTION set_customer_name_search();

CREATE INDEX IF NOT EXISTS ix_customers_name_search_trgm
ON customers USING gin (name_search gin_trgm_ops);
