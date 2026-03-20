SET search_path TO congno, public;

DROP INDEX IF EXISTS ix_advances_customer_no;
DROP INDEX IF EXISTS ix_receipts_customer_no;

DO $$
BEGIN
  IF EXISTS (
    SELECT 1
    FROM advances
    WHERE deleted_at IS NULL
      AND advance_no IS NOT NULL
      AND btrim(advance_no) <> ''
    GROUP BY seller_tax_code, customer_tax_code, btrim(advance_no)
    HAVING count(*) > 1
  ) THEN
    RAISE EXCEPTION
      'Không thể tạo uq_advances_dedup vì dữ liệu khoản trả hộ hiện có đang trùng seller_tax_code + customer_tax_code + advance_no.';
  END IF;
END $$;

DO $$
BEGIN
  IF EXISTS (
    SELECT 1
    FROM receipts
    WHERE deleted_at IS NULL
      AND receipt_no IS NOT NULL
      AND btrim(receipt_no) <> ''
    GROUP BY seller_tax_code, customer_tax_code, btrim(receipt_no)
    HAVING count(*) > 1
  ) THEN
    RAISE EXCEPTION
      'Không thể tạo uq_receipts_dedup vì dữ liệu phiếu thu hiện có đang trùng seller_tax_code + customer_tax_code + receipt_no.';
  END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS uq_advances_dedup
  ON advances(seller_tax_code, customer_tax_code, advance_no)
  WHERE deleted_at IS NULL
    AND advance_no IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_receipts_dedup
  ON receipts(seller_tax_code, customer_tax_code, receipt_no)
  WHERE deleted_at IS NULL
    AND receipt_no IS NOT NULL;
