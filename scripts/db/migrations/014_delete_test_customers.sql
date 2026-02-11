-- Delete seeded test customers and related data
SET search_path TO congno, public;

DO $$
BEGIN
  IF current_setting('app.env', true) IS DISTINCT FROM 'dev' THEN
    RAISE NOTICE 'Skip delete_test_customers: app.env is not dev.';
    RETURN;
  END IF;

  WITH targets AS (
    SELECT unnest(ARRAY['0101000001','0101000002','0101000003']) AS tax_code
  )
  DELETE FROM reminder_logs rl
  USING targets t
  WHERE rl.customer_tax_code = t.tax_code;

  WITH targets AS (
    SELECT unnest(ARRAY['0101000001','0101000002','0101000003']) AS tax_code
  )
  DELETE FROM receipt_allocations ra
  USING receipts r, targets t
  WHERE ra.receipt_id = r.id AND r.customer_tax_code = t.tax_code;

  WITH targets AS (
    SELECT unnest(ARRAY['0101000001','0101000002','0101000003']) AS tax_code
  )
  DELETE FROM receipt_allocations ra
  USING invoices i, targets t
  WHERE ra.invoice_id = i.id AND i.customer_tax_code = t.tax_code;

  WITH targets AS (
    SELECT unnest(ARRAY['0101000001','0101000002','0101000003']) AS tax_code
  )
  DELETE FROM receipt_allocations ra
  USING advances a, targets t
  WHERE ra.advance_id = a.id AND a.customer_tax_code = t.tax_code;

  WITH targets AS (
    SELECT unnest(ARRAY['0101000001','0101000002','0101000003']) AS tax_code
  )
  DELETE FROM receipts r
  USING targets t
  WHERE r.customer_tax_code = t.tax_code;

  WITH targets AS (
    SELECT unnest(ARRAY['0101000001','0101000002','0101000003']) AS tax_code
  )
  DELETE FROM advances a
  USING targets t
  WHERE a.customer_tax_code = t.tax_code;

  WITH targets AS (
    SELECT unnest(ARRAY['0101000001','0101000002','0101000003']) AS tax_code
  )
  DELETE FROM invoices i
  USING targets t
  WHERE i.customer_tax_code = t.tax_code;

  WITH targets AS (
    SELECT unnest(ARRAY['0101000001','0101000002','0101000003']) AS tax_code
  )
  DELETE FROM customers c
  USING targets t
  WHERE c.tax_code = t.tax_code;
END $$;
