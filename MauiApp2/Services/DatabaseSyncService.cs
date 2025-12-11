using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace MauiApp2.Services
{
    public interface IDatabaseSyncService
    {
        Task<SyncResult> SyncDatabaseAsync(string localConnectionString, string cloudConnectionString);
        Task<SyncResult> SyncBidirectionalAsync(string localConnectionString, string cloudConnectionString, string pcIdentifier);
        Task<bool> TestConnectionAsync(string connectionString);
    }

    public class DatabaseSyncService : IDatabaseSyncService
    {
        // Tables in order (respecting foreign key dependencies)
        private static readonly List<string> Tables = new List<string>
        {
            // User & Role Management
            "tbl_roles",
            "tbl_users",
            
            // Product Master Data
            "tbl_category",
            "tbl_brand",
            "tbl_tax",
            "tbl_product",
            
            // Supplier & Customer
            "tbl_supplier",
            "tbl_customer",
            
            // Purchase Operations
            "tbl_purchase_order",
            "tbl_purchase_order_items",
            "tbl_stock_in",
            "tbl_stock_in_items",
            
            // Sales Operations
            "tbl_sales_order",
            "tbl_sales_order_items",
            "tbl_stock_out",
            "tbl_stock_out_items",
            
            // Accounting
            "tbl_chart_of_accounts",
            "tbl_accounts_payable",
            "tbl_payments",
            "tbl_expenses",
            "tbl_general_ledger"
        };

        public async Task<bool> TestConnectionAsync(string connectionString)
        {
            try
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new Exception("Connection string is empty");
                }
                
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                
                // Test a simple query to ensure connection is fully working
                using var cmd = new SqlCommand("SELECT 1", connection);
                await cmd.ExecuteScalarAsync();
                
                return true;
            }
            catch (Exception ex)
            {
                // Re-throw to get error message in the UI
                throw new Exception($"Connection failed: {ex.Message}", ex);
            }
        }

        public async Task<SyncResult> SyncDatabaseAsync(string localConnectionString, string cloudConnectionString)
        {
            var result = new SyncResult();
            result.StartTime = DateTime.Now;

            try
            {
                // Test connections
                result.Messages.Add("Testing local database connection...");
                if (!await TestConnectionAsync(localConnectionString))
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = "Failed to connect to local database";
                    return result;
                }
                result.Messages.Add("✓ Local database connection successful");

                result.Messages.Add("Testing cloud database connection...");
                if (!await TestConnectionAsync(cloudConnectionString))
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = "Failed to connect to cloud database";
                    return result;
                }
                result.Messages.Add("✓ Cloud database connection successful");

                result.Messages.Add("");
                result.Messages.Add("Starting data synchronization...");

                // Sync each table with retry logic
                foreach (var tableName in Tables)
                {
                    var tableResult = await SyncTableWithRetryAsync(localConnectionString, cloudConnectionString, tableName, maxRetries: 3);
                    result.TotalTablesProcessed++;
                    result.TotalRowsCopied += tableResult.RowsCopied;
                    
                    if (tableResult.IsSuccess)
                    {
                        result.Messages.Add($"✓ {tableName}: {tableResult.RowsCopied} rows copied");
                    }
                    else
                    {
                        result.Messages.Add($"✗ {tableName}: {tableResult.ErrorMessage}");
                        result.HasWarnings = true;
                    }
                }

                result.IsSuccess = true;
                result.Messages.Add("");
                result.Messages.Add($"=== Sync Complete ===");
                result.Messages.Add($"Total tables processed: {result.TotalTablesProcessed}");
                result.Messages.Add($"Total rows copied: {result.TotalRowsCopied}");
                
                // Reset identity seeds after sync to prevent ID jumps
                result.Messages.Add("");
                result.Messages.Add("Resetting identity seeds in cloud database...");
                await ResetIdentitySeedsAsync(cloudConnectionString);
                result.Messages.Add("✓ Identity seeds reset successfully");
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.Messages.Add($"ERROR: {ex.Message}");
            }
            finally
            {
                result.EndTime = DateTime.Now;
                result.Duration = result.EndTime - result.StartTime;
            }

            return result;
        }

        /// <summary>
        /// Performs bidirectional synchronization:
        /// 1. Pulls changes from cloud to local (cloud → local)
        /// 2. Pushes changes from local to cloud (local → cloud)
        /// Uses timestamps to detect changes and resolve conflicts
        /// </summary>
        public async Task<SyncResult> SyncBidirectionalAsync(string localConnectionString, string cloudConnectionString, string pcIdentifier)
        {
            var result = new SyncResult();
            result.StartTime = DateTime.Now;

            try
            {
                // Test connections
                result.Messages.Add("Testing local database connection...");
                if (!await TestConnectionAsync(localConnectionString))
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = "Failed to connect to local database";
                    return result;
                }
                result.Messages.Add("✓ Local database connection successful");

                result.Messages.Add("Testing cloud database connection...");
                if (!await TestConnectionAsync(cloudConnectionString))
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = "Failed to connect to cloud database";
                    return result;
                }
                result.Messages.Add("✓ Cloud database connection successful");

                result.Messages.Add("");
                result.Messages.Add("Starting bidirectional synchronization...");
                result.Messages.Add($"PC Identifier: {pcIdentifier}");

                // Step 1: Pull changes from cloud to local (cloud → local)
                result.Messages.Add("");
                result.Messages.Add("=== Step 1: Pulling changes from cloud to local ===");
                foreach (var tableName in Tables)
                {
                    var pullResult = await PullTableFromCloudAsync(localConnectionString, cloudConnectionString, tableName, pcIdentifier);
                    result.TotalTablesProcessed++;
                    result.TotalRowsCopied += pullResult.RowsCopied;
                    
                    if (pullResult.IsSuccess)
                    {
                        result.Messages.Add($"✓ Pulled {tableName}: {pullResult.RowsCopied} rows");
                    }
                    else
                    {
                        result.Messages.Add($"✗ Pull {tableName}: {pullResult.ErrorMessage}");
                        result.HasWarnings = true;
                    }
                }

                // Step 2: Push changes from local to cloud (local → cloud)
                result.Messages.Add("");
                result.Messages.Add("=== Step 2: Pushing changes from local to cloud ===");
                foreach (var tableName in Tables)
                {
                    var pushResult = await PushTableToCloudAsync(localConnectionString, cloudConnectionString, tableName, pcIdentifier);
                    result.TotalTablesProcessed++;
                    result.TotalRowsCopied += pushResult.RowsCopied;
                    
                    if (pushResult.IsSuccess)
                    {
                        result.Messages.Add($"✓ Pushed {tableName}: {pushResult.RowsCopied} rows");
                    }
                    else
                    {
                        result.Messages.Add($"✗ Push {tableName}: {pushResult.ErrorMessage}");
                        result.HasWarnings = true;
                    }
                }

                result.IsSuccess = true;
                result.Messages.Add("");
                result.Messages.Add($"=== Bidirectional Sync Complete ===");
                result.Messages.Add($"Total tables processed: {result.TotalTablesProcessed}");
                result.Messages.Add($"Total rows synced: {result.TotalRowsCopied}");
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.Messages.Add($"ERROR: {ex.Message}");
            }
            finally
            {
                result.EndTime = DateTime.Now;
                result.Duration = result.EndTime - result.StartTime;
            }

            return result;
        }

        /// <summary>
        /// Pulls changes from cloud database to local database
        /// Only pulls records that have been modified since last sync
        /// </summary>
        private async Task<TableSyncResult> PullTableFromCloudAsync(string localConnectionString, string cloudConnectionString, string tableName, string pcIdentifier)
        {
            var result = new TableSyncResult();
            SqlConnection? localConn = null;
            SqlConnection? cloudConn = null;

            try
            {
                localConn = new SqlConnection(localConnectionString);
                var cloudConnBuilder = new SqlConnectionStringBuilder(cloudConnectionString)
                {
                    ConnectTimeout = 60,
                    CommandTimeout = 300
                };
                cloudConn = new SqlConnection(cloudConnBuilder.ConnectionString);

                await localConn.OpenAsync();
                await cloudConn.OpenAsync();

                // Get last sync timestamp for this table
                var lastSyncTime = await GetLastSyncTimestampAsync(localConn, pcIdentifier, tableName, "Pull");
                
                // Get columns
                var cloudColumns = await GetTableColumnsAsync(cloudConn, tableName);
                var localColumns = await GetTableColumnsAsync(localConn, tableName);
                
                // Get computed columns to exclude them from INSERT/UPDATE
                var computedColumns = await GetComputedColumnsAsync(localConn, tableName);
                
                // Filter columns: must exist in both, and not be computed
                var columns = cloudColumns
                    .Where(c => localColumns.Contains(c, StringComparer.OrdinalIgnoreCase))
                    .Where(c => !computedColumns.Contains(c, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                if (!columns.Any())
                {
                    result.ErrorMessage = "No matching columns found";
                    return result;
                }

                // Check for identity column and ensure it's included when IDENTITY_INSERT will be used
                var hasIdentity = await HasIdentityColumnAsync(localConn, tableName);
                string? identityColumnName = null;
                if (hasIdentity)
                {
                    identityColumnName = await GetIdentityColumnNameAsync(localConn, tableName);
                    // Find matching identity column in cloud (case-insensitive)
                    var cloudIdentityColumn = cloudColumns.FirstOrDefault(c => 
                        string.Equals(c, identityColumnName, StringComparison.OrdinalIgnoreCase));
                    
                    // If identity column exists in both but not in our list, add it
                    if (!string.IsNullOrEmpty(cloudIdentityColumn) && 
                        !columns.Any(c => string.Equals(c, cloudIdentityColumn, StringComparison.OrdinalIgnoreCase)))
                    {
                        columns.Insert(0, cloudIdentityColumn);
                    }
                }

                // Check if table has modified_date column
                var hasModifiedDate = columns.Any(c => string.Equals(c, "modified_date", StringComparison.OrdinalIgnoreCase));
                var pkColumn = await GetPrimaryKeyColumnAsync(cloudConn, tableName);

                // Build query to get changed records from cloud
                var whereClause = "";
                if (hasModifiedDate && lastSyncTime.HasValue)
                {
                    whereClause = "WHERE modified_date > @lastSyncTime OR modified_date IS NULL";
                }
                else if (lastSyncTime.HasValue)
                {
                    // If no modified_date, use created_date or just get all records
                    var hasCreatedDate = columns.Any(c => string.Equals(c, "created_date", StringComparison.OrdinalIgnoreCase));
                    if (hasCreatedDate)
                    {
                        whereClause = "WHERE created_date > @lastSyncTime";
                    }
                }

                var selectQuery = $"SELECT * FROM [{tableName}] {whereClause}";
                using var cloudCmd = new SqlCommand(selectQuery, cloudConn);
                if (lastSyncTime.HasValue)
                {
                    cloudCmd.Parameters.AddWithValue("@lastSyncTime", lastSyncTime.Value);
                }

                using var reader = await cloudCmd.ExecuteReaderAsync();
                int rowCount = 0;
                int errorCount = 0;
                var errors = new List<string>();

                // Enable IDENTITY_INSERT if needed (must be done before any inserts)
                if (hasIdentity && !string.IsNullOrEmpty(identityColumnName))
                {
                    // Verify identity column is in our columns list
                    var hasIdentityInList = columns.Any(c => 
                        string.Equals(c, identityColumnName, StringComparison.OrdinalIgnoreCase));
                    
                    if (hasIdentityInList)
                    {
                        var identityQuery = $"SET IDENTITY_INSERT [{tableName}] ON";
                        using var identityCmd = new SqlCommand(identityQuery, localConn);
                        await identityCmd.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        // Identity column not in list, don't use IDENTITY_INSERT
                        hasIdentity = false;
                    }
                }

                while (await reader.ReadAsync())
                {
                    try
                    {
                        // Check if row exists in local
                        bool rowExists = false;
                        object? pkValue = null;
                        
                        if (!string.IsNullOrEmpty(pkColumn))
                        {
                            // Find matching cloud column name (case-insensitive)
                            var cloudPkColumn = cloudColumns.FirstOrDefault(c => 
                                string.Equals(c, pkColumn, StringComparison.OrdinalIgnoreCase));
                            
                            if (!string.IsNullOrEmpty(cloudPkColumn) && 
                                columns.Any(c => string.Equals(c, cloudPkColumn, StringComparison.OrdinalIgnoreCase)))
                            {
                                pkValue = reader[cloudPkColumn];
                                if (pkValue != null && pkValue != DBNull.Value)
                                {
                                    var rowExistsQuery = $"SELECT COUNT(*) FROM [{tableName}] WHERE [{pkColumn}] = @pkValue";
                                    using var rowExistsCmd = new SqlCommand(rowExistsQuery, localConn);
                                    rowExistsCmd.Parameters.AddWithValue("@pkValue", pkValue);
                                    rowExists = (int)await rowExistsCmd.ExecuteScalarAsync() > 0;
                                }
                            }
                        }

                        if (rowExists && !string.IsNullOrEmpty(pkColumn) && pkValue != null)
                        {
                            // Update existing row (conflict resolution: cloud wins if it's newer)
                            var updateColumns = columns
                                .Where(c => !string.Equals(c, pkColumn, StringComparison.OrdinalIgnoreCase))
                                .Where(c => !computedColumns.Contains(c, StringComparer.OrdinalIgnoreCase))
                                .ToList();
                            
                            if (updateColumns.Any())
                            {
                                // Check if cloud version is newer (if modified_date exists)
                                bool shouldUpdate = true;
                                if (hasModifiedDate)
                                {
                                    var cloudModifiedDate = reader["modified_date"] as DateTime?;
                                    if (cloudModifiedDate.HasValue)
                                    {
                                        var localModifiedDate = await GetLocalModifiedDateAsync(localConn, tableName, pkColumn, pkValue);
                                        if (localModifiedDate.HasValue && localModifiedDate.Value >= cloudModifiedDate.Value)
                                        {
                                            shouldUpdate = false; // Local is newer, don't overwrite
                                        }
                                    }
                                }

                                if (shouldUpdate)
                                {
                                    var setClause = string.Join(", ", updateColumns.Select(c => $"[{c}] = @{c}"));
                                    var updateQuery = $"UPDATE [{tableName}] SET {setClause} WHERE [{pkColumn}] = @pkValue";
                                    
                                    using var updateCmd = new SqlCommand(updateQuery, localConn);
                                    foreach (var column in updateColumns)
                                    {
                                        var value = reader[column];
                                        updateCmd.Parameters.AddWithValue($"@{column}", value == DBNull.Value ? DBNull.Value : value);
                                    }
                                    updateCmd.Parameters.AddWithValue("@pkValue", pkValue);
                                    await updateCmd.ExecuteNonQueryAsync();
                                    rowCount++;
                                }
                            }
                        }
                        else
                        {
                            // Insert new row - validate data first
                            var insertColumns = columns.ToList();
                            
                            // Validate required foreign keys and handle NULLs
                            var validationErrors = await ValidateRowDataAsync(localConn, tableName, reader, insertColumns, cloudColumns);
                            if (validationErrors.Any())
                            {
                                errorCount++;
                                errors.AddRange(validationErrors);
                                continue; // Skip this row
                            }
                            
                            var columnList = string.Join(", ", insertColumns.Select(c => $"[{c}]"));
                            var valueList = string.Join(", ", insertColumns.Select(c => $"@{c}"));
                            var insertQuery = $"INSERT INTO [{tableName}] ({columnList}) VALUES ({valueList})";
                            
                            using var insertCmd = new SqlCommand(insertQuery, localConn);
                            foreach (var column in insertColumns)
                            {
                                var value = reader[column];
                                insertCmd.Parameters.AddWithValue($"@{column}", value == DBNull.Value ? DBNull.Value : value);
                            }
                            await insertCmd.ExecuteNonQueryAsync();
                            rowCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        errors.Add($"Row error: {ex.Message}");
                        // Continue with next row instead of failing entire sync
                        continue;
                    }
                }
                
                // Log errors if any
                if (errorCount > 0)
                {
                    result.ErrorMessage = $"{errorCount} row(s) failed: {string.Join("; ", errors.Take(5))}";
                    if (errors.Count > 5)
                    {
                        result.ErrorMessage += $" (and {errors.Count - 5} more)";
                    }
                }

                // Turn off IDENTITY_INSERT if it was enabled
                if (hasIdentity && !string.IsNullOrEmpty(identityColumnName))
                {
                    try
                    {
                        var identityQuery = $"SET IDENTITY_INSERT [{tableName}] OFF";
                        using var identityCmd = new SqlCommand(identityQuery, localConn);
                        await identityCmd.ExecuteNonQueryAsync();
                    }
                    catch
                    {
                        // Ignore errors when turning off IDENTITY_INSERT
                    }
                }

                // Update sync timestamp
                await UpdateSyncTimestampAsync(localConn, pcIdentifier, tableName, "Pull", rowCount);

                result.RowsCopied = rowCount;
                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }
            finally
            {
                localConn?.Close();
                localConn?.Dispose();
                cloudConn?.Close();
                cloudConn?.Dispose();
            }

            return result;
        }

        /// <summary>
        /// Pushes changes from local database to cloud database
        /// Only pushes records that have been modified since last sync
        /// </summary>
        private async Task<TableSyncResult> PushTableToCloudAsync(string localConnectionString, string cloudConnectionString, string tableName, string pcIdentifier)
        {
            var result = new TableSyncResult();
            SqlConnection? localConn = null;
            SqlConnection? cloudConn = null;

            try
            {
                localConn = new SqlConnection(localConnectionString);
                var cloudConnBuilder = new SqlConnectionStringBuilder(cloudConnectionString)
                {
                    ConnectTimeout = 60,
                    CommandTimeout = 300
                };
                cloudConn = new SqlConnection(cloudConnBuilder.ConnectionString);

                await localConn.OpenAsync();
                await cloudConn.OpenAsync();

                // Get last sync timestamp for this table
                var lastSyncTime = await GetLastSyncTimestampAsync(localConn, pcIdentifier, tableName, "Push");
                
                // Get columns
                var localColumns = await GetTableColumnsAsync(localConn, tableName);
                var cloudColumns = await GetTableColumnsAsync(cloudConn, tableName);
                
                // Get computed columns to exclude them from INSERT/UPDATE
                var computedColumns = await GetComputedColumnsAsync(cloudConn, tableName);
                
                // Filter columns: must exist in both, and not be computed
                var columns = localColumns
                    .Where(c => cloudColumns.Contains(c, StringComparer.OrdinalIgnoreCase))
                    .Where(c => !computedColumns.Contains(c, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                if (!columns.Any())
                {
                    result.ErrorMessage = "No matching columns found";
                    return result;
                }

                // Check for identity column and ensure it's included when IDENTITY_INSERT will be used
                var hasIdentity = await HasIdentityColumnAsync(cloudConn, tableName);
                string? identityColumnName = null;
                if (hasIdentity)
                {
                    identityColumnName = await GetIdentityColumnNameAsync(cloudConn, tableName);
                    // Find matching identity column in local (case-insensitive)
                    var localIdentityColumn = localColumns.FirstOrDefault(c => 
                        string.Equals(c, identityColumnName, StringComparison.OrdinalIgnoreCase));
                    
                    // If identity column exists in both but not in our list, add it
                    if (!string.IsNullOrEmpty(localIdentityColumn) && 
                        !columns.Any(c => string.Equals(c, localIdentityColumn, StringComparison.OrdinalIgnoreCase)))
                    {
                        columns.Insert(0, localIdentityColumn);
                    }
                }

                // Check if table has modified_date column
                var hasModifiedDate = columns.Any(c => string.Equals(c, "modified_date", StringComparison.OrdinalIgnoreCase));
                var pkColumn = await GetPrimaryKeyColumnAsync(cloudConn, tableName);

                // Build query to get changed records from local
                var whereClause = "";
                if (hasModifiedDate && lastSyncTime.HasValue)
                {
                    whereClause = "WHERE modified_date > @lastSyncTime OR modified_date IS NULL";
                }
                else if (lastSyncTime.HasValue)
                {
                    var hasCreatedDate = columns.Any(c => string.Equals(c, "created_date", StringComparison.OrdinalIgnoreCase));
                    if (hasCreatedDate)
                    {
                        whereClause = "WHERE created_date > @lastSyncTime";
                    }
                }

                var selectQuery = $"SELECT * FROM [{tableName}] {whereClause}";
                using var localCmd = new SqlCommand(selectQuery, localConn);
                if (lastSyncTime.HasValue)
                {
                    localCmd.Parameters.AddWithValue("@lastSyncTime", lastSyncTime.Value);
                }

                using var reader = await localCmd.ExecuteReaderAsync();
                int rowCount = 0;
                int errorCount = 0;
                var errors = new List<string>();

                // Enable IDENTITY_INSERT if needed (must be done before any inserts)
                if (hasIdentity && !string.IsNullOrEmpty(identityColumnName))
                {
                    // Verify identity column is in our columns list
                    var hasIdentityInList = columns.Any(c => 
                        string.Equals(c, identityColumnName, StringComparison.OrdinalIgnoreCase));
                    
                    if (hasIdentityInList)
                    {
                        var identityQuery = $"SET IDENTITY_INSERT [{tableName}] ON";
                        using var identityCmd = new SqlCommand(identityQuery, cloudConn);
                        await identityCmd.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        // Identity column not in list, don't use IDENTITY_INSERT
                        hasIdentity = false;
                    }
                }

                while (await reader.ReadAsync())
                {
                    try
                    {
                        // Check if row exists in cloud
                        bool rowExists = false;
                        object? pkValue = null;
                        
                        if (!string.IsNullOrEmpty(pkColumn))
                        {
                            // Find matching local column name (case-insensitive)
                            var localPkColumn = localColumns.FirstOrDefault(c => 
                                string.Equals(c, pkColumn, StringComparison.OrdinalIgnoreCase));
                            
                            if (!string.IsNullOrEmpty(localPkColumn) && 
                                columns.Any(c => string.Equals(c, localPkColumn, StringComparison.OrdinalIgnoreCase)))
                            {
                                pkValue = reader[localPkColumn];
                                if (pkValue != null && pkValue != DBNull.Value)
                                {
                                    var rowExistsQuery = $"SELECT COUNT(*) FROM [{tableName}] WHERE [{pkColumn}] = @pkValue";
                                    using var rowExistsCmd = new SqlCommand(rowExistsQuery, cloudConn);
                                    rowExistsCmd.Parameters.AddWithValue("@pkValue", pkValue);
                                    rowExists = (int)await rowExistsCmd.ExecuteScalarAsync() > 0;
                                }
                            }
                        }

                        if (rowExists && !string.IsNullOrEmpty(pkColumn) && pkValue != null)
                        {
                            // Update existing row (conflict resolution: local wins if it's newer)
                            var updateColumns = columns
                                .Where(c => !string.Equals(c, pkColumn, StringComparison.OrdinalIgnoreCase))
                                .Where(c => !computedColumns.Contains(c, StringComparer.OrdinalIgnoreCase))
                                .ToList();
                            
                            if (updateColumns.Any())
                            {
                                bool shouldUpdate = true;
                                if (hasModifiedDate)
                                {
                                    var localModifiedDate = reader["modified_date"] as DateTime?;
                                    if (localModifiedDate.HasValue)
                                    {
                                        var cloudModifiedDate = await GetLocalModifiedDateAsync(cloudConn, tableName, pkColumn, pkValue);
                                        if (cloudModifiedDate.HasValue && cloudModifiedDate.Value >= localModifiedDate.Value)
                                        {
                                            shouldUpdate = false; // Cloud is newer, don't overwrite
                                        }
                                    }
                                }

                                if (shouldUpdate)
                                {
                                    var setClause = string.Join(", ", updateColumns.Select(c => $"[{c}] = @{c}"));
                                    var updateQuery = $"UPDATE [{tableName}] SET {setClause} WHERE [{pkColumn}] = @pkValue";
                                    
                                    using var updateCmd = new SqlCommand(updateQuery, cloudConn);
                                    foreach (var column in updateColumns)
                                    {
                                        var value = reader[column];
                                        updateCmd.Parameters.AddWithValue($"@{column}", value == DBNull.Value ? DBNull.Value : value);
                                    }
                                    updateCmd.Parameters.AddWithValue("@pkValue", pkValue);
                                    await updateCmd.ExecuteNonQueryAsync();
                                    rowCount++;
                                }
                            }
                        }
                        else
                        {
                            // Insert new row - validate data first
                            var insertColumns = columns.ToList();
                            
                            // Validate required foreign keys and handle NULLs
                            var validationErrors = await ValidateRowDataAsync(cloudConn, tableName, reader, insertColumns, localColumns);
                            if (validationErrors.Any())
                            {
                                errorCount++;
                                errors.AddRange(validationErrors);
                                continue; // Skip this row
                            }
                            
                            var columnList = string.Join(", ", insertColumns.Select(c => $"[{c}]"));
                            var valueList = string.Join(", ", insertColumns.Select(c => $"@{c}"));
                            var insertQuery = $"INSERT INTO [{tableName}] ({columnList}) VALUES ({valueList})";
                            
                            using var insertCmd = new SqlCommand(insertQuery, cloudConn);
                            foreach (var column in insertColumns)
                            {
                                var value = reader[column];
                                insertCmd.Parameters.AddWithValue($"@{column}", value == DBNull.Value ? DBNull.Value : value);
                            }
                            await insertCmd.ExecuteNonQueryAsync();
                            rowCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        errors.Add($"Row error: {ex.Message}");
                        // Continue with next row instead of failing entire sync
                        continue;
                    }
                }
                
                // Log errors if any
                if (errorCount > 0)
                {
                    result.ErrorMessage = $"{errorCount} row(s) failed: {string.Join("; ", errors.Take(5))}";
                    if (errors.Count > 5)
                    {
                        result.ErrorMessage += $" (and {errors.Count - 5} more)";
                    }
                }

                // Turn off IDENTITY_INSERT if it was enabled
                if (hasIdentity && !string.IsNullOrEmpty(identityColumnName))
                {
                    try
                    {
                        var identityQuery = $"SET IDENTITY_INSERT [{tableName}] OFF";
                        using var identityCmd = new SqlCommand(identityQuery, cloudConn);
                        await identityCmd.ExecuteNonQueryAsync();
                    }
                    catch
                    {
                        // Ignore errors when turning off IDENTITY_INSERT
                    }
                }

                // Update sync timestamp
                await UpdateSyncTimestampAsync(localConn, pcIdentifier, tableName, "Push", rowCount);

                result.RowsCopied = rowCount;
                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }
            finally
            {
                localConn?.Close();
                localConn?.Dispose();
                cloudConn?.Close();
                cloudConn?.Dispose();
            }

            return result;
        }

        private async Task<DateTime?> GetLastSyncTimestampAsync(SqlConnection connection, string pcIdentifier, string tableName, string direction)
        {
            try
            {
                var query = @"
                    SELECT last_sync_timestamp 
                    FROM tbl_sync_tracking 
                    WHERE pc_identifier = @pcIdentifier 
                    AND table_name = @tableName 
                    AND (last_sync_direction = @direction OR last_sync_direction IS NULL)";
                
                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@pcIdentifier", pcIdentifier);
                cmd.Parameters.AddWithValue("@tableName", tableName);
                cmd.Parameters.AddWithValue("@direction", direction);
                
                var result = await cmd.ExecuteScalarAsync();
                return result as DateTime?;
            }
            catch
            {
                return null;
            }
        }

        private async Task UpdateSyncTimestampAsync(SqlConnection connection, string pcIdentifier, string tableName, string direction, int recordsSynced)
        {
            try
            {
                var query = @"
                    IF EXISTS (SELECT 1 FROM tbl_sync_tracking WHERE pc_identifier = @pcIdentifier AND table_name = @tableName)
                    BEGIN
                        UPDATE tbl_sync_tracking 
                        SET last_sync_timestamp = GETDATE(),
                            last_sync_direction = @direction,
                            records_synced = @recordsSynced,
                            modified_date = GETDATE()
                        WHERE pc_identifier = @pcIdentifier AND table_name = @tableName
                    END
                    ELSE
                    BEGIN
                        INSERT INTO tbl_sync_tracking (pc_identifier, table_name, last_sync_timestamp, last_sync_direction, records_synced)
                        VALUES (@pcIdentifier, @tableName, GETDATE(), @direction, @recordsSynced)
                    END";
                
                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@pcIdentifier", pcIdentifier);
                cmd.Parameters.AddWithValue("@tableName", tableName);
                cmd.Parameters.AddWithValue("@direction", direction);
                cmd.Parameters.AddWithValue("@recordsSynced", recordsSynced);
                
                await cmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // If table doesn't exist, that's okay
            }
        }

        private async Task<DateTime?> GetLocalModifiedDateAsync(SqlConnection connection, string tableName, string pkColumn, object pkValue)
        {
            try
            {
                var query = $"SELECT modified_date FROM [{tableName}] WHERE [{pkColumn}] = @pkValue";
                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@pkValue", pkValue);
                var result = await cmd.ExecuteScalarAsync();
                return result as DateTime?;
            }
            catch
            {
                return null;
            }
        }
        
        private async Task ResetIdentitySeedsAsync(string connectionString)
        {
            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                
                foreach (var tableName in Tables)
                {
                    try
                    {
                        var pkColumn = await GetPrimaryKeyColumnAsync(connection, tableName);
                        if (!string.IsNullOrEmpty(pkColumn))
                        {
                            // Get max ID
                            var maxIdQuery = $"SELECT ISNULL(MAX([{pkColumn}]), 0) FROM [{tableName}]";
                            using var maxCmd = new SqlCommand(maxIdQuery, connection);
                            var maxId = Convert.ToInt32(await maxCmd.ExecuteScalarAsync());
                            
                            // Reset identity seed
                            var reseedQuery = $"DBCC CHECKIDENT ('[{tableName}]', RESEED, {maxId})";
                            using var reseedCmd = new SqlCommand(reseedQuery, connection);
                            await reseedCmd.ExecuteNonQueryAsync();
                        }
                    }
                    catch
                    {
                        // Skip if table doesn't exist or has no identity column
                        continue;
                    }
                }
            }
            catch
            {
                // If reset fails, continue - it's not critical
            }
        }

        private async Task<TableSyncResult> SyncTableWithRetryAsync(string localConnectionString, string cloudConnectionString, string tableName, int maxRetries = 3)
        {
            TableSyncResult? lastResult = null;
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    lastResult = await SyncTableAsync(localConnectionString, cloudConnectionString, tableName);
                    if (lastResult.IsSuccess)
                    {
                        return lastResult;
                    }
                    
                    // If it's a connection error, wait and retry
                    if (lastResult.ErrorMessage.Contains("connection") || 
                        lastResult.ErrorMessage.Contains("network") ||
                        lastResult.ErrorMessage.Contains("transport"))
                    {
                        if (attempt < maxRetries)
                        {
                            await Task.Delay(2000 * attempt); // Exponential backoff: 2s, 4s, 6s
                            continue;
                        }
                    }
                    
                    // If it's not a connection error, don't retry
                    return lastResult;
                }
                catch (Exception ex)
                {
                    lastResult = new TableSyncResult
                    {
                        IsSuccess = false,
                        ErrorMessage = ex.Message
                    };
                    
                    if (attempt < maxRetries && (ex.Message.Contains("connection") || ex.Message.Contains("network")))
                    {
                        await Task.Delay(2000 * attempt);
                        continue;
                    }
                    
                    return lastResult;
                }
            }
            
            return lastResult ?? new TableSyncResult { IsSuccess = false, ErrorMessage = "Max retries exceeded" };
        }

        private async Task<TableSyncResult> SyncTableAsync(string localConnectionString, string cloudConnectionString, string tableName)
        {
            var result = new TableSyncResult();
            SqlConnection? localConn = null;
            SqlConnection? cloudConn = null;

            try
            {
                // Create connections with longer timeout for cloud
                localConn = new SqlConnection(localConnectionString);
                var cloudConnBuilder = new SqlConnectionStringBuilder(cloudConnectionString)
                {
                    ConnectTimeout = 60, // 60 seconds connection timeout
                    CommandTimeout = 300 // 5 minutes command timeout
                };
                cloudConn = new SqlConnection(cloudConnBuilder.ConnectionString);

                await localConn.OpenAsync();
                await cloudConn.OpenAsync();

                // Check if table exists in local database
                var tableExistsQuery = @"
                    SELECT COUNT(*) 
                    FROM INFORMATION_SCHEMA.TABLES 
                    WHERE TABLE_NAME = @tableName";

                using var checkCmd = new SqlCommand(tableExistsQuery, localConn);
                checkCmd.Parameters.AddWithValue("@tableName", tableName);
                var tableExists = (int)await checkCmd.ExecuteScalarAsync() > 0;

                if (!tableExists)
                {
                    result.ErrorMessage = "Table doesn't exist in local database";
                    return result;
                }

                // Check if table exists in cloud database
                using var checkCloudCmd = new SqlCommand(tableExistsQuery, cloudConn);
                checkCloudCmd.Parameters.AddWithValue("@tableName", tableName);
                var cloudTableExists = (int)await checkCloudCmd.ExecuteScalarAsync() > 0;

                if (!cloudTableExists)
                {
                    result.ErrorMessage = "Table doesn't exist in cloud database. Please create the table first.";
                    return result;
                }

                // Get all data from local database
                var selectQuery = $"SELECT * FROM [{tableName}]";
                using var localCmd = new SqlCommand(selectQuery, localConn);
                using var reader = await localCmd.ExecuteReaderAsync();

                if (!reader.HasRows)
                {
                    result.RowsCopied = 0;
                    result.IsSuccess = true;
                    return result;
                }

                // Get column names from local database
                var localColumns = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    localColumns.Add(reader.GetName(i));
                }

                // Get column names from cloud database
                var cloudColumns = await GetTableColumnsAsync(cloudConn, tableName);
                
                // Check if table has IDENTITY column and get its name
                var hasIdentity = await HasIdentityColumnAsync(cloudConn, tableName);
                string? identityColumnName = null;
                if (hasIdentity)
                {
                    identityColumnName = await GetIdentityColumnNameAsync(cloudConn, tableName);
                }
                
                // Filter to only include columns that exist in both databases
                var columns = localColumns.Where(c => cloudColumns.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
                
                // If identity column exists and IDENTITY_INSERT will be ON, ensure it's in the columns list
                bool identityColumnInLocal = false;
                string? localIdentityColumnName = null;
                if (hasIdentity && !string.IsNullOrEmpty(identityColumnName))
                {
                    // Find matching identity column name (case-insensitive) in local columns
                    localIdentityColumnName = localColumns.FirstOrDefault(c => 
                        string.Equals(c, identityColumnName, StringComparison.OrdinalIgnoreCase));
                    identityColumnInLocal = !string.IsNullOrEmpty(localIdentityColumnName);
                    
                    // If identity column exists in both databases but not in our list, add it
                    if (identityColumnInLocal && 
                        cloudColumns.Contains(identityColumnName, StringComparer.OrdinalIgnoreCase) &&
                        !columns.Any(c => string.Equals(c, localIdentityColumnName, StringComparison.OrdinalIgnoreCase)))
                    {
                        columns.Insert(0, localIdentityColumnName); // Add at beginning to ensure it's included
                    }
                }
                
                // Check for missing columns and log warnings
                var missingColumns = localColumns.Where(c => !cloudColumns.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
                if (missingColumns.Any())
                {
                    result.ErrorMessage = $"Missing columns in cloud database: {string.Join(", ", missingColumns)}";
                    // Continue anyway with available columns
                }

                if (!columns.Any())
                {
                    result.ErrorMessage = "No matching columns found between local and cloud databases";
                    return result;
                }

                // Set IDENTITY_INSERT ON only if identity column exists in both databases
                // This must be done BEFORE any inserts
                if (hasIdentity && !string.IsNullOrEmpty(identityColumnName) && identityColumnInLocal && !string.IsNullOrEmpty(localIdentityColumnName))
                {
                    // Verify identity column is in our columns list
                    var hasIdentityInList = columns.Any(c => 
                        string.Equals(c, localIdentityColumnName, StringComparison.OrdinalIgnoreCase));
                    
                    if (!hasIdentityInList)
                    {
                        result.ErrorMessage = $"Identity column '{identityColumnName}' must be included when IDENTITY_INSERT is ON";
                        return result;
                    }
                    
                    var identityQuery = $"SET IDENTITY_INSERT [{tableName}] ON";
                    using var identityCmd = new SqlCommand(identityQuery, cloudConn);
                    await identityCmd.ExecuteNonQueryAsync();
                }
                else if (hasIdentity && !identityColumnInLocal)
                {
                    // Identity column exists in cloud but not in local - don't use IDENTITY_INSERT
                    // Let SQL Server auto-generate the identity values
                    hasIdentity = false;
                }

                int rowCount = 0;
                while (await reader.ReadAsync())
                {
                    // Build INSERT statement
                    var columnList = string.Join(", ", columns.Select(c => $"[{c}]"));
                    var valueList = string.Join(", ", columns.Select(c => $"@{c}"));

                    var insertQuery = $"INSERT INTO [{tableName}] ({columnList}) VALUES ({valueList})";

                    // Check if row already exists (by primary key)
                    var cloudPkColumn = await GetPrimaryKeyColumnAsync(cloudConn, tableName);
                    bool rowExists = false;
                    object? pkValue = null;
                    
                    if (!string.IsNullOrEmpty(cloudPkColumn) && columns.Contains(cloudPkColumn, StringComparer.OrdinalIgnoreCase))
                    {
                        // Find the matching local column name (case-insensitive)
                        var localPkColumn = localColumns.FirstOrDefault(c => 
                            string.Equals(c, cloudPkColumn, StringComparison.OrdinalIgnoreCase));
                        
                        if (!string.IsNullOrEmpty(localPkColumn))
                        {
                            pkValue = reader[localPkColumn];
                            var rowExistsQuery = $"SELECT COUNT(*) FROM [{tableName}] WHERE [{cloudPkColumn}] = @pkValue";
                            using var rowExistsCmd = new SqlCommand(rowExistsQuery, cloudConn);
                            rowExistsCmd.Parameters.AddWithValue("@pkValue", pkValue);
                            rowExists = (int)await rowExistsCmd.ExecuteScalarAsync() > 0;
                        }
                    }

                    if (rowExists && !string.IsNullOrEmpty(cloudPkColumn) && pkValue != null)
                    {
                        // Update existing row
                        var updateColumns = columns.Where(c => !string.Equals(c, cloudPkColumn, StringComparison.OrdinalIgnoreCase)).ToList();
                        if (updateColumns.Any())
                        {
                            var setClause = string.Join(", ", updateColumns.Select(c => $"[{c}] = @{c}"));
                            var updateQuery = $"UPDATE [{tableName}] SET {setClause} WHERE [{cloudPkColumn}] = @pkValue";
                            
                            using var updateCmd = new SqlCommand(updateQuery, cloudConn);
                            
                            // Add parameters for update
                            foreach (var column in updateColumns)
                            {
                                var value = reader[column];
                                if (value == DBNull.Value)
                                {
                                    updateCmd.Parameters.AddWithValue($"@{column}", DBNull.Value);
                                }
                                else
                                {
                                    updateCmd.Parameters.AddWithValue($"@{column}", value);
                                }
                            }
                            
                            updateCmd.Parameters.AddWithValue("@pkValue", pkValue);
                            await updateCmd.ExecuteNonQueryAsync();
                            rowCount++;
                        }
                        continue; // Skip insert after update
                    }

                    using var insertCmd = new SqlCommand(insertQuery, cloudConn);

                    // Add parameters
                    foreach (var column in columns)
                    {
                        var value = reader[column];
                        if (value == DBNull.Value)
                        {
                            insertCmd.Parameters.AddWithValue($"@{column}", DBNull.Value);
                        }
                        else
                        {
                            insertCmd.Parameters.AddWithValue($"@{column}", value);
                        }
                    }

                    await insertCmd.ExecuteNonQueryAsync();
                    rowCount++;
                }

                if (hasIdentity)
                {
                    var identityQuery = $"SET IDENTITY_INSERT [{tableName}] OFF";
                    using var identityCmd = new SqlCommand(identityQuery, cloudConn);
                    await identityCmd.ExecuteNonQueryAsync();
                }

                result.RowsCopied = rowCount;
                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }
            finally
            {
                // Ensure connections are properly closed
                try
                {
                    if (localConn != null && localConn.State != System.Data.ConnectionState.Closed)
                    {
                        localConn.Close();
                        localConn.Dispose();
                    }
                }
                catch { }
                
                try
                {
                    if (cloudConn != null && cloudConn.State != System.Data.ConnectionState.Closed)
                    {
                        cloudConn.Close();
                        cloudConn.Dispose();
                    }
                }
                catch { }
            }

            return result;
        }

        private async Task<bool> HasIdentityColumnAsync(SqlConnection connection, string tableName)
        {
            try
            {
                var query = @"
                    SELECT COUNT(*) 
                    FROM sys.columns c
                    INNER JOIN sys.tables t ON c.object_id = t.object_id
                    WHERE t.name = @tableName 
                    AND c.is_identity = 1";

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@tableName", tableName);
                var count = (int)await cmd.ExecuteScalarAsync();
                return count > 0;
            }
            catch
            {
                return false;
            }
        }

        private async Task<string?> GetIdentityColumnNameAsync(SqlConnection connection, string tableName)
        {
            try
            {
                var query = @"
                    SELECT c.name
                    FROM sys.columns c
                    INNER JOIN sys.tables t ON c.object_id = t.object_id
                    WHERE t.name = @tableName 
                    AND c.is_identity = 1";

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@tableName", tableName);
                var result = await cmd.ExecuteScalarAsync();
                return result?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private async Task<string> GetPrimaryKeyColumnAsync(SqlConnection connection, string tableName)
        {
            try
            {
                var query = @"
                    SELECT c.name
                    FROM sys.key_constraints kc
                    INNER JOIN sys.index_columns ic ON kc.parent_object_id = ic.object_id AND kc.unique_index_id = ic.index_id
                    INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                    INNER JOIN sys.tables t ON kc.parent_object_id = t.object_id
                    WHERE t.name = @tableName
                    AND kc.type = 'PK'";

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@tableName", tableName);
                var result = await cmd.ExecuteScalarAsync();
                return result?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private async Task<List<string>> GetTableColumnsAsync(SqlConnection connection, string tableName)
        {
            var columns = new List<string>();
            try
            {
                var query = @"
                    SELECT c.name
                    FROM sys.columns c
                    INNER JOIN sys.tables t ON c.object_id = t.object_id
                    WHERE t.name = @tableName
                    ORDER BY c.column_id";

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@tableName", tableName);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    columns.Add(reader.GetString(0));
                }
            }
            catch
            {
                // Return empty list if table doesn't exist
            }
            return columns;
        }

        /// <summary>
        /// Gets list of computed columns for a table (columns that cannot be inserted/updated)
        /// </summary>
        private async Task<List<string>> GetComputedColumnsAsync(SqlConnection connection, string tableName)
        {
            var computedColumns = new List<string>();
            try
            {
                var query = @"
                    SELECT c.name
                    FROM sys.columns c
                    INNER JOIN sys.tables t ON c.object_id = t.object_id
                    WHERE t.name = @tableName
                    AND c.is_computed = 1";

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@tableName", tableName);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    computedColumns.Add(reader.GetString(0));
                }
            }
            catch
            {
                // Return empty list if table doesn't exist
            }
            return computedColumns;
        }

        /// <summary>
        /// Validates row data before insertion to prevent constraint violations
        /// </summary>
        private async Task<List<string>> ValidateRowDataAsync(SqlConnection connection, string tableName, SqlDataReader reader, List<string> columns, List<string> sourceColumns)
        {
            var errors = new List<string>();
            
            try
            {
                // Get NOT NULL columns that don't have defaults
                var query = @"
                    SELECT c.name, c.is_nullable, c.default_object_id
                    FROM sys.columns c
                    INNER JOIN sys.tables t ON c.object_id = t.object_id
                    WHERE t.name = @tableName
                    AND c.is_computed = 0
                    AND c.is_nullable = 0
                    AND c.default_object_id = 0";
                
                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@tableName", tableName);
                using var nullableReader = await cmd.ExecuteReaderAsync();
                
                while (await nullableReader.ReadAsync())
                {
                    var columnName = nullableReader.GetString(0);
                    // Find matching column in source (case-insensitive)
                    var sourceColumn = sourceColumns.FirstOrDefault(c => 
                        string.Equals(c, columnName, StringComparison.OrdinalIgnoreCase));
                    
                    // Also check if it's in our columns list (might have different name)
                    var targetColumn = columns.FirstOrDefault(c => 
                        string.Equals(c, columnName, StringComparison.OrdinalIgnoreCase));
                    
                    if (!string.IsNullOrEmpty(targetColumn) && columns.Contains(targetColumn, StringComparer.OrdinalIgnoreCase))
                    {
                        try
                        {
                            // Try to read from reader using source column name or target column name
                            object? value = null;
                            if (!string.IsNullOrEmpty(sourceColumn))
                            {
                                try { value = reader[sourceColumn]; } catch { }
                            }
                            if (value == null && !string.IsNullOrEmpty(targetColumn))
                            {
                                try { value = reader[targetColumn]; } catch { }
                            }
                            
                            if (value == null || value == DBNull.Value)
                            {
                                errors.Add($"Column '{columnName}' cannot be NULL");
                            }
                        }
                        catch
                        {
                            // Column doesn't exist in reader, skip
                        }
                    }
                }
                
                // Special validation for known CHECK constraints
                if (tableName.Equals("tbl_stock_in_items", StringComparison.OrdinalIgnoreCase))
                {
                    var quantityReceivedCol = sourceColumns.FirstOrDefault(c => 
                        string.Equals(c, "quantity_received", StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(quantityReceivedCol))
                    {
                        try
                        {
                            var quantityReceived = reader[quantityReceivedCol];
                            if (quantityReceived != null && quantityReceived != DBNull.Value)
                            {
                                var qty = Convert.ToInt32(quantityReceived);
                                if (qty < 0)
                                {
                                    errors.Add("quantity_received must be >= 0");
                                }
                            }
                        }
                        catch
                        {
                            // Skip if column doesn't exist or can't convert
                        }
                    }
                }
            }
            catch
            {
                // If validation query fails, continue without validation
            }
            
            return errors;
        }
    }

    public class SyncResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<string> Messages { get; set; } = new List<string>();
        public int TotalTablesProcessed { get; set; }
        public int TotalRowsCopied { get; set; }
        public bool HasWarnings { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class TableSyncResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public int RowsCopied { get; set; }
    }
}
