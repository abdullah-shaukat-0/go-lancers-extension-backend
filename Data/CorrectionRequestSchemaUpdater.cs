using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace SHMS.Backend.Data
{
    public static class CorrectionRequestSchemaUpdater
    {
        public static void EnsureSchema(SHMSDbContext context)
        {
            context.Database.OpenConnection();

            try
            {
                ExecuteNonQuery(context, @"
                    CREATE TABLE IF NOT EXISTS PatientCorrectionRequests (
                        Id INTEGER NOT NULL CONSTRAINT PK_PatientCorrectionRequests PRIMARY KEY AUTOINCREMENT,
                        PatientId INTEGER NOT NULL,
                        FieldName TEXT NULL,
                        CurrentValue TEXT NULL,
                        RequestedValue TEXT NULL,
                        Reason TEXT NULL,
                        Status TEXT NULL,
                        SubmittedAt TEXT NOT NULL,
                        ReviewedAt TEXT NULL,
                        ReviewedByUserId TEXT NULL,
                        ReviewNote TEXT NULL,
                        CONSTRAINT FK_PatientCorrectionRequests_Patients_PatientId FOREIGN KEY (PatientId) REFERENCES Patients (Id) ON DELETE RESTRICT,
                        CONSTRAINT FK_PatientCorrectionRequests_AspNetUsers_ReviewedByUserId FOREIGN KEY (ReviewedByUserId) REFERENCES AspNetUsers (Id) ON DELETE SET NULL
                    );");

                ExecuteNonQuery(context, @"
                    CREATE INDEX IF NOT EXISTS IX_PatientCorrectionRequests_PatientId
                    ON PatientCorrectionRequests (PatientId);");

                ExecuteNonQuery(context, @"
                    CREATE INDEX IF NOT EXISTS IX_PatientCorrectionRequests_ReviewedByUserId
                    ON PatientCorrectionRequests (ReviewedByUserId);");
            }
            finally
            {
                context.Database.CloseConnection();
            }
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
