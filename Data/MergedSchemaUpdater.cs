using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data.Common;

namespace SHMS.Backend.Data
{
    public static class MergedSchemaUpdater
    {
        public static void EnsureSchema(SHMSDbContext context)
        {
            context.Database.OpenConnection();

            try
            {
                ExecuteNonQuery(context, @"
                    CREATE TABLE IF NOT EXISTS AuditLogs (
                        Id INTEGER NOT NULL CONSTRAINT PK_AuditLogs PRIMARY KEY AUTOINCREMENT,
                        Timestamp TEXT NOT NULL,
                        UserId TEXT NULL,
                        UserName TEXT NULL,
                        UserRole TEXT NULL,
                        PatientId INTEGER NULL,
                        Action TEXT NULL,
                        ResourceType TEXT NULL,
                        ResourceId TEXT NULL,
                        Details TEXT NULL,
                        IpAddress TEXT NULL,
                        WasSuccessful INTEGER NOT NULL
                    );");

                var auditColumns = GetColumns(context, "AuditLogs");
                AddColumnIfMissing(context, auditColumns, "AuditLogs", "Timestamp", "TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'");
                AddColumnIfMissing(context, auditColumns, "AuditLogs", "UserId", "TEXT NULL");
                AddColumnIfMissing(context, auditColumns, "AuditLogs", "UserName", "TEXT NULL");
                AddColumnIfMissing(context, auditColumns, "AuditLogs", "UserRole", "TEXT NULL");
                AddColumnIfMissing(context, auditColumns, "AuditLogs", "PatientId", "INTEGER NULL");
                AddColumnIfMissing(context, auditColumns, "AuditLogs", "Action", "TEXT NULL");
                AddColumnIfMissing(context, auditColumns, "AuditLogs", "ResourceType", "TEXT NULL");
                AddColumnIfMissing(context, auditColumns, "AuditLogs", "ResourceId", "TEXT NULL");
                AddColumnIfMissing(context, auditColumns, "AuditLogs", "Details", "TEXT NULL");
                AddColumnIfMissing(context, auditColumns, "AuditLogs", "IpAddress", "TEXT NULL");
                AddColumnIfMissing(context, auditColumns, "AuditLogs", "WasSuccessful", "INTEGER NOT NULL DEFAULT 1");

                var appointmentColumns = GetColumns(context, "Appointments");
                AddColumnIfMissing(context, appointmentColumns, "Appointments", "ReminderSent", "INTEGER NOT NULL DEFAULT 0");
            }
            finally
            {
                context.Database.CloseConnection();
            }
        }

        private static HashSet<string> GetColumns(SHMSDbContext context, string tableName)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var command = context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = $"PRAGMA table_info({tableName});";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        columns.Add(reader.GetString(1));
                    }
                }
            }

            return columns;
        }

        private static void AddColumnIfMissing(SHMSDbContext context, HashSet<string> existingColumns, string tableName, string columnName, string columnDefinition)
        {
            if (existingColumns.Contains(columnName)) return;

            ExecuteNonQuery(context, $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};");
            existingColumns.Add(columnName);
        }

        private static void ExecuteNonQuery(SHMSDbContext context, string sql)
        {
            using (DbCommand command = context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }
    }
}
