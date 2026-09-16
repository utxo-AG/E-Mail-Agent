-- Migration: Add per-agent maximum mail age column
-- Date: 2026-06-18
-- Description: Adds a column that limits how old (in days) an incoming email may be
--              to still be answered by the agent. NULL or 0 = no limit.

ALTER TABLE agents ADD COLUMN maximum_mail_age_days INT NULL;

-- Optional: e.g. Störungs-Agenten auf 1 Tag begrenzen (IDs anpassen)
-- UPDATE agents SET maximum_mail_age_days = 1 WHERE id IN (3, 8);

-- Note: Run this migration before deploying the updated application
