-- ============================================
-- Create Sync Tracking Table
-- ============================================
-- This table tracks when each PC last synced
-- Used for bidirectional synchronization
-- ============================================

PRINT '========================================';
PRINT 'Creating Sync Tracking Table';
PRINT '========================================';
PRINT '';

-- ============================================
-- Create tbl_sync_tracking (for LocalDB)
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[tbl_sync_tracking]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[tbl_sync_tracking] (
        [tracking_id] INT IDENTITY(1,1) PRIMARY KEY,
        [pc_identifier] NVARCHAR(100) NOT NULL, -- Unique identifier for this PC
        [table_name] NVARCHAR(100) NOT NULL,
        [last_sync_timestamp] DATETIME NOT NULL DEFAULT GETDATE(),
        [last_sync_direction] NVARCHAR(20) NULL, -- 'Push' (local->cloud) or 'Pull' (cloud->local)
        [records_synced] INT NOT NULL DEFAULT 0,
        [created_date] DATETIME NOT NULL DEFAULT GETDATE(),
        [modified_date] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [UQ_sync_tracking_pc_table] UNIQUE ([pc_identifier], [table_name])
    );
    
    CREATE INDEX [IX_sync_tracking_pc] ON [dbo].[tbl_sync_tracking] ([pc_identifier]);
    CREATE INDEX [IX_sync_tracking_table] ON [dbo].[tbl_sync_tracking] ([table_name]);
    CREATE INDEX [IX_sync_tracking_timestamp] ON [dbo].[tbl_sync_tracking] ([last_sync_timestamp]);
    PRINT '✓ Created tbl_sync_tracking';
END
ELSE
BEGIN
    PRINT '⚠ tbl_sync_tracking already exists';
END
GO

-- ============================================
-- Create tbl_sync_tracking (for Cloud DB)
-- ============================================
-- Note: Run this script on cloud database as well
PRINT '';
PRINT 'Note: Run this script on your cloud database too!';
PRINT '';


