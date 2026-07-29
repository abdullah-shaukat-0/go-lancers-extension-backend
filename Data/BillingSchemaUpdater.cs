using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data.Common;

namespace SHMS.Backend.Data
{
    public static class BillingSchemaUpdater
    {
        public static void EnsureSchema(SHMSDbContext context)
        {
            context.Database.OpenConnection();

            try
            {
                EnsureBillColumns(context);
                ExecuteNonQuery(context, @"
                    CREATE TABLE IF NOT EXISTS HospitalServices (
                        Id INTEGER NOT NULL CONSTRAINT PK_HospitalServices PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NULL,
                        Category TEXT NULL,
                        Price TEXT NOT NULL,
                        IsActive INTEGER NOT NULL
                    );");

                ExecuteNonQuery(context, @"
                    CREATE TABLE IF NOT EXISTS Expenses (
                        Id INTEGER NOT NULL CONSTRAINT PK_Expenses PRIMARY KEY AUTOINCREMENT,
                        Title TEXT NULL,
                        Category TEXT NULL,
                        Amount TEXT NOT NULL,
                        ExpenseDate TEXT NOT NULL,
                        Notes TEXT NULL,
                        CreatedAt TEXT NOT NULL
                    );");

                ExecuteNonQuery(context, @"
                    CREATE TABLE IF NOT EXISTS BillItems (
                        Id INTEGER NOT NULL CONSTRAINT PK_BillItems PRIMARY KEY AUTOINCREMENT,
                        BillId INTEGER NOT NULL,
                        HospitalServiceId INTEGER NULL,
                        Description TEXT NULL,
                        Quantity INTEGER NOT NULL,
                        UnitPrice TEXT NOT NULL,
                        LineTotal TEXT NOT NULL,
                        CONSTRAINT FK_BillItems_Bills_BillId FOREIGN KEY (BillId) REFERENCES Bills (Id) ON DELETE CASCADE,
                        CONSTRAINT FK_BillItems_HospitalServices_HospitalServiceId FOREIGN KEY (HospitalServiceId) REFERENCES HospitalServices (Id) ON DELETE SET NULL
                    );");
            }
            finally
            {
                context.Database.CloseConnection();
            }
        }

        private static void EnsureBillColumns(SHMSDbContext context)
        {
            var existingColumns = GetColumns(context, "Bills");
            AddColumnIfMissing(context, existingColumns, "Bills", "InvoiceNumber", "TEXT NULL");
            AddColumnIfMissing(context, existingColumns, "Bills", "Subtotal", "TEXT NOT NULL DEFAULT '0.0'");
            AddColumnIfMissing(context, existingColumns, "Bills", "DiscountAmount", "TEXT NOT NULL DEFAULT '0.0'");
            AddColumnIfMissing(context, existingColumns, "Bills", "TaxAmount", "TEXT NOT NULL DEFAULT '0.0'");
            AddColumnIfMissing(context, existingColumns, "Bills", "Notes", "TEXT NULL");
            AddColumnIfMissing(context, existingColumns, "Bills", "DatePaid", "TEXT NULL");
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
