SET search_path TO congno, public;

ALTER TABLE risk_rules
    ADD COLUMN IF NOT EXISTS match_mode text;

UPDATE risk_rules
SET match_mode = 'ANY'
WHERE match_mode IS NULL
   OR btrim(match_mode) = '';

ALTER TABLE risk_rules
    ALTER COLUMN match_mode SET DEFAULT 'ANY';

ALTER TABLE risk_rules
    ALTER COLUMN match_mode SET NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_risk_rules_match_mode'
          AND conrelid = 'risk_rules'::regclass
    ) THEN
        ALTER TABLE risk_rules
            ADD CONSTRAINT ck_risk_rules_match_mode
                CHECK (match_mode IN ('ANY', 'ALL'));
    END IF;
END $$;
