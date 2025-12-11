# Bidirectional Database Synchronization Guide

## Overview

This system now supports **bidirectional synchronization** between multiple PCs through a cloud database. The system works offline using local database and automatically synchronizes when internet connection is restored.

## How It Works

### Architecture

```
PC1 (LocalDB) ←→ Cloud Database ←→ PC2 (LocalDB)
```

- **LocalDB**: Each PC has its own local SQL Server database
- **Cloud Database**: Central SQL Server database that acts as the sync hub
- **Bidirectional Sync**: Changes flow both ways (cloud → local AND local → cloud)

### Key Components

1. **PcIdentifierService**: Generates and tracks a unique identifier for each PC
2. **DatabaseSyncService**: Performs bidirectional synchronization
3. **AutoSyncService**: Monitors connectivity and automatically syncs when online
4. **ConnectivityService**: Checks if cloud database is accessible
5. **SyncQueueService**: Tracks operations that need syncing (optional, for tracking)

## Synchronization Process

### Step 1: Pull Changes from Cloud (Cloud → Local)

When sync runs, the system:
1. Checks when this PC last synced each table
2. Queries cloud database for records modified since last sync
3. Compares `modified_date` timestamps to detect changes
4. Downloads new/updated records from cloud to local database
5. Resolves conflicts: If both PC and cloud modified the same record, the newer version wins

### Step 2: Push Changes to Cloud (Local → Cloud)

After pulling, the system:
1. Checks when this PC last pushed changes
2. Queries local database for records modified since last push
3. Uploads new/updated records from local to cloud database
4. Resolves conflicts: If both PC and cloud modified the same record, the newer version wins

### Conflict Resolution

The system uses **last-write-wins** strategy based on `modified_date`:
- If cloud record has newer `modified_date` → cloud version wins (overwrites local)
- If local record has newer `modified_date` → local version wins (overwrites cloud)
- If no `modified_date` exists, uses `created_date` or syncs all records

## Offline Mode

### How Offline Mode Works

1. **All operations use LocalDB**: When you create/update/delete records, they're saved to your local database immediately
2. **No internet required**: You can work completely offline
3. **Changes are tracked**: The system tracks when records were last modified
4. **Auto-sync on reconnect**: When internet comes back, AutoSyncService detects it and syncs automatically

### Offline Workflow

```
1. User creates/updates record → Saved to LocalDB ✅
2. Internet disconnected → Continue working normally ✅
3. User creates more records → All saved to LocalDB ✅
4. Internet reconnected → AutoSyncService detects connection
5. Bidirectional sync runs automatically:
   - Pulls changes from cloud (PC2's changes)
   - Pushes local changes to cloud (PC1's changes)
6. Both PCs now have the same data ✅
```

## Multi-PC Synchronization

### Scenario: PC1 and PC2

**Initial State:**
- PC1 has Product A, B, C
- PC2 has Product D, E, F
- Cloud has Product A, B, C (from PC1's last sync)

**PC1 goes offline:**
- PC1 creates Product G, H
- PC2 creates Product I, J (synced to cloud)

**PC1 comes back online:**
1. **Pull Phase**: PC1 downloads Product I, J from cloud → PC1 now has A, B, C, G, H, I, J
2. **Push Phase**: PC1 uploads Product G, H to cloud → Cloud now has A, B, C, G, H, I, J

**PC2 syncs:**
1. **Pull Phase**: PC2 downloads Product G, H from cloud → PC2 now has A, B, C, D, E, F, G, H, I, J
2. **Push Phase**: PC2 uploads Product I, J (already in cloud, but syncs anyway)

**Result**: Both PCs have identical data: A, B, C, D, E, F, G, H, I, J

## Setup Instructions

### 1. Run SQL Scripts

Run these scripts on **both LocalDB and Cloud Database**:

```sql
-- Create sync tracking table
-- File: MauiApp2/Scripts/CreateSyncTrackingTable.sql
```

### 2. Verify Services Are Registered

Check `MauiProgram.cs` - these services should be registered:
- `IPcIdentifierService` → `PcIdentifierService`
- `IDatabaseSyncService` → `DatabaseSyncService`
- `IAutoSyncService` → `AutoSyncService`
- `IConnectivityService` → `ConnectivityService`

### 3. Configure Connection Strings

Ensure `App.config` has:
- `DefaultConnection`: Local database connection string
- `CloudConnection`: Cloud database connection string

### 4. Test the System

1. **Test Offline Mode:**
   - Disconnect internet
   - Create/update records
   - Verify they're saved to LocalDB
   - Check sync status indicator shows "Offline"

2. **Test Auto-Sync:**
   - Reconnect internet
   - Wait 30 seconds (or trigger manual sync)
   - Verify sync status indicator shows "Online"
   - Check that records synced to cloud

3. **Test Multi-PC Sync:**
   - On PC1: Create records offline, then sync
   - On PC2: Verify records appear after sync
   - On PC2: Create records, sync
   - On PC1: Verify records appear after sync

## Sync Tracking

The system uses `tbl_sync_tracking` table to track:
- `pc_identifier`: Unique ID for each PC
- `table_name`: Which table was synced
- `last_sync_timestamp`: When last synced
- `last_sync_direction`: "Pull" or "Push"
- `records_synced`: How many records were synced

This allows the system to only sync changes since last sync, making it efficient.

## Troubleshooting

### Sync not working?

1. **Check connectivity:**
   - Verify cloud connection string is correct
   - Test connection using Database Sync page
   - Check firewall/network settings

2. **Check sync tracking table:**
   ```sql
   SELECT * FROM tbl_sync_tracking
   WHERE pc_identifier = 'YOUR_PC_ID'
   ```

3. **Check for errors:**
   - Look at sync result messages
   - Check sync history table
   - Review application logs

### Records not syncing?

1. **Verify tables have `modified_date` column:**
   ```sql
   SELECT COLUMN_NAME 
   FROM INFORMATION_SCHEMA.COLUMNS 
   WHERE TABLE_NAME = 'tbl_product' 
   AND COLUMN_NAME = 'modified_date'
   ```

2. **Check if records have been modified:**
   - Records only sync if `modified_date` is newer than last sync
   - Or if `modified_date` is NULL

3. **Force full sync:**
   - Delete sync tracking entries to force full sync
   - Or manually trigger sync from Database Sync page

### Conflicts not resolving?

- The system uses `modified_date` for conflict resolution
- If both records have same `modified_date`, the one that syncs first wins
- Ensure `modified_date` is updated when records are modified

## Best Practices

1. **Always update `modified_date`** when modifying records
2. **Let auto-sync run** - don't manually sync unless needed
3. **Monitor sync status** - check the sync status indicator
4. **Handle conflicts carefully** - review sync results for warnings
5. **Backup regularly** - both local and cloud databases

## Technical Details

### Tables Synced

The following tables are synchronized:
- `tbl_roles`
- `tbl_users`
- `tbl_category`
- `tbl_brand`
- `tbl_tax`
- `tbl_product`
- `tbl_supplier`
- `tbl_purchase_order`
- `tbl_purchase_order_items`
- `tbl_stock_in`
- `tbl_stock_in_items`
- `tbl_sales_order`
- `tbl_sales_order_items`
- `tbl_stock_out`
- `tbl_stock_out_items`

### Sync Frequency

- **Auto-sync**: Checks every 30 seconds when online
- **Manual sync**: Can be triggered from Database Sync page
- **On reconnect**: Syncs immediately when connection is restored

### Performance

- Only changed records are synced (based on timestamps)
- Sync is incremental (not full database copy)
- Uses transactions for data integrity
- Handles large datasets efficiently

## Summary

✅ **Offline Mode**: Works completely offline using LocalDB  
✅ **Auto-Sync**: Automatically syncs when internet is restored  
✅ **Bidirectional**: Pulls from cloud AND pushes to cloud  
✅ **Multi-PC**: Multiple PCs can sync through cloud database  
✅ **Conflict Resolution**: Last-write-wins based on timestamps  
✅ **Efficient**: Only syncs changed records  

The system is now ready for production use with full offline support and multi-PC synchronization!


