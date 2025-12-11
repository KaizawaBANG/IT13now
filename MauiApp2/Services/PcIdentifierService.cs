using System;
using System.Configuration;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MauiApp2.Components.Database;

namespace MauiApp2.Services
{
    public class PcIdentifierService : IPcIdentifierService
    {
        private string? _cachedIdentifier = null;
        private const string IDENTIFIER_KEY = "PC_SYNC_IDENTIFIER";

        public string GetPcIdentifier()
        {
            if (_cachedIdentifier != null)
                return _cachedIdentifier;

            // Try to get from app settings first
            var identifier = ConfigurationManager.AppSettings[IDENTIFIER_KEY];
            
            if (string.IsNullOrEmpty(identifier))
            {
                // Generate a unique identifier based on machine name and user
                identifier = $"{Environment.MachineName}_{Environment.UserName}_{Environment.OSVersion}";
                // Make it shorter and more stable
                identifier = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(identifier))
                    .Replace("=", "").Replace("+", "").Replace("/", "").Substring(0, Math.Min(50, identifier.Length));
            }

            _cachedIdentifier = identifier;
            return identifier;
        }

        public async Task<string> GetOrCreatePcIdentifierAsync()
        {
            var identifier = GetPcIdentifier();
            
            // Store in database for persistence
            try
            {
                using var connection = db.GetConnection();
                await connection.OpenAsync();

                // Check if identifier exists in sync_tracking
                var checkCmd = new SqlCommand(@"
                    SELECT TOP 1 pc_identifier 
                    FROM tbl_sync_tracking 
                    WHERE pc_identifier = @identifier", connection);
                checkCmd.Parameters.AddWithValue("@identifier", identifier);
                
                var exists = await checkCmd.ExecuteScalarAsync();
                
                if (exists == null)
                {
                    // Create initial tracking entry for at least one table
                    var insertCmd = new SqlCommand(@"
                        IF NOT EXISTS (SELECT 1 FROM tbl_sync_tracking WHERE pc_identifier = @identifier AND table_name = 'tbl_product')
                        INSERT INTO tbl_sync_tracking (pc_identifier, table_name, last_sync_timestamp, last_sync_direction)
                        VALUES (@identifier, 'tbl_product', GETDATE(), 'Initial')", connection);
                    insertCmd.Parameters.AddWithValue("@identifier", identifier);
                    await insertCmd.ExecuteNonQueryAsync();
                }
            }
            catch
            {
                // If table doesn't exist yet, that's okay - it will be created
            }

            return identifier;
        }
    }
}


