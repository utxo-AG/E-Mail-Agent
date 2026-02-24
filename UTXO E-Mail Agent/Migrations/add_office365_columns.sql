-- Migration: Add Office 365 OAuth columns to agents table
-- Date: 2026-02-24
-- Description: Adds columns for storing Office 365 OAuth tokens

-- Add new ENUM value to emailprovider column
ALTER TABLE agents MODIFY COLUMN emailprovider ENUM('inbound','imap','pop3','exchange','office365') NOT NULL;

-- Add Office 365 OAuth token columns
ALTER TABLE agents ADD COLUMN office365_access_token TEXT NULL;
ALTER TABLE agents ADD COLUMN office365_refresh_token TEXT NULL;
ALTER TABLE agents ADD COLUMN office365_token_expires_at DATETIME NULL;
ALTER TABLE agents ADD COLUMN office365_user_id VARCHAR(255) NULL;

-- Note: Run this migration before deploying the updated application
