-- Add upcoming due days setting for reminders
SET search_path TO congno, public;

ALTER TABLE reminder_settings
ADD COLUMN IF NOT EXISTS upcoming_due_days int NOT NULL DEFAULT 7;

UPDATE reminder_settings
SET upcoming_due_days = 7
WHERE upcoming_due_days IS NULL;
