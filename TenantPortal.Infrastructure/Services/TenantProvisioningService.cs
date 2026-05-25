using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using TenantPortal.Application.Interfaces.Services;

namespace TenantPortal.Infrastructure.Services
{
    public class TenantProvisioningService : ITenantProvisioningService
    {
        private readonly IConfiguration _cfg;

        public TenantProvisioningService(IConfiguration cfg)
        {
            _cfg = cfg;
        }

        public string GetHubUrl()
        {
            return _cfg["ClyiqHub:BaseUrl"] ?? "https://clyiqhub.local/login";
        }

        public async Task<(bool ok, string erpUrl)> ProvisionAsync(Guid tenantId, Guid envManagerUserId, CancellationToken ct)
        {
            // 1) Compute suffix from envManagerUserId (first 12 chars of N format)
            var suffix = envManagerUserId.ToString("N")[..12];

            var coreDb = $"Clyiq_Core_EM_{suffix}";
            var txnDb = $"Clyiq_Txn_EM_{suffix}";
            var rptDb = $"Clyiq_Rpt_EM_{suffix}";

            // 2) Read provisioning settings
            var masterConn = _cfg["ErpProvisioning:SqlAdminMaster"]
                ?? throw new InvalidOperationException("Missing config: ErpProvisioning:SqlAdminMaster");

            var coreBak = _cfg["ErpProvisioning:CoreTemplateBak"]
                ?? throw new InvalidOperationException("Missing config: ErpProvisioning:CoreTemplateBak");

            var txnBak = _cfg["ErpProvisioning:TxnTemplateBak"]
                ?? throw new InvalidOperationException("Missing config: ErpProvisioning:TxnTemplateBak");

            var rptBak = _cfg["ErpProvisioning:RptTemplateBak"]
                ?? throw new InvalidOperationException("Missing config: ErpProvisioning:RptTemplateBak");



            var dataDir = _cfg["ErpProvisioning:SqlDataDir"]
                ?? throw new InvalidOperationException("Missing config: ErpProvisioning:SqlDataDir");

            var coreDataLogical = _cfg["ErpProvisioning:CoreDataLogical"]
                ?? throw new InvalidOperationException("Missing config: ErpProvisioning:CoreDataLogical");

            var coreLogLogical = _cfg["ErpProvisioning:CoreLogLogical"]
                ?? throw new InvalidOperationException("Missing config: ErpProvisioning:CoreLogLogical");

            var txnDataLogical = _cfg["ErpProvisioning:TxnDataLogical"]
                ?? throw new InvalidOperationException("Missing config: ErpProvisioning:TxnDataLogical");

            var txnLogLogical = _cfg["ErpProvisioning:TxnLogLogical"]
                ?? throw new InvalidOperationException("Missing config: ErpProvisioning:TxnLogLogical");


            var rptDataLogical = _cfg["ErpProvisioning:RptDataLogical"]
                ?? throw new InvalidOperationException("Missing config: ErpProvisioning:RptDataLogical");

            var rptLogLogical = _cfg["ErpProvisioning:RptLogLogical"]
                ?? throw new InvalidOperationException("Missing config: ErpProvisioning:RptLogLogical");


            // normalize directory to end with backslash
            if (!dataDir.EndsWith("\\"))
                dataDir += "\\";

            // 3) Connect to master and restore DBs
            await using var conn = new SqlConnection(masterConn);
            await conn.OpenAsync(ct);

            // Restore Core DB
            await RestoreDbAsync(
                conn,
                dbName: coreDb,
                bakPath: coreBak,
                dataLogical: coreDataLogical,
                logLogical: coreLogLogical,
                dataFilePath: $"{dataDir}{coreDb}.mdf",
                logFilePath: $"{dataDir}{coreDb}_log.ldf",
                ct);

            // Restore Txn DB
            await RestoreDbAsync(
                conn,
                dbName: txnDb,
                bakPath: txnBak,
                dataLogical: txnDataLogical,
                logLogical: txnLogLogical,
                dataFilePath: $"{dataDir}{txnDb}.mdf",
                logFilePath: $"{dataDir}{txnDb}_log.ldf",
                ct);

            // Restore Rpt DB
            await RestoreDbAsync(
                conn,
                dbName: rptDb,
                bakPath: rptBak,
                dataLogical: rptDataLogical,
                logLogical: rptLogLogical,
                dataFilePath: $"{dataDir}{rptDb}.mdf",
                logFilePath: $"{dataDir}{rptDb}_log.ldf",
                ct);

            // 4) Save mapping into TenantPortal DB table ErpEnvironmentDbMap
            // We will insert mapping into the same DB where TenantPortal runs (ConnectionStrings:Default)
            var portalConn = _cfg.GetConnectionString("Default")
                ?? throw new InvalidOperationException("Missing ConnectionStrings:Default for registry insert.");

            await using (var conn2 = new SqlConnection(portalConn))
            {
                await conn2.OpenAsync(ct);

                // Upsert (insert if not exists)
                var cmdText = @"
IF EXISTS (SELECT 1 FROM dbo.ErpEnvironmentDbMap WHERE TenantId = @TenantId)
BEGIN
  UPDATE dbo.ErpEnvironmentDbMap
    SET EnvManagerUserId = @EnvManagerUserId,
        CoreDbName = @CoreDbName,
        TxnDbName = @TxnDbName,
        RptDbName = @RptDbName,
        Status = @Status
  WHERE TenantId = @TenantId;
END
ELSE
BEGIN
  INSERT INTO dbo.ErpEnvironmentDbMap (TenantId, EnvManagerUserId, CoreDbName, TxnDbName, RptDbName, Status)
  VALUES (@TenantId, @EnvManagerUserId, @CoreDbName, @TxnDbName, @RptDbName, @Status);
END
";
                await using var cmd = new SqlCommand(cmdText, conn2);
                cmd.Parameters.AddWithValue("@TenantId", tenantId);
                cmd.Parameters.AddWithValue("@EnvManagerUserId", envManagerUserId);
                cmd.Parameters.AddWithValue("@CoreDbName", coreDb);
                cmd.Parameters.AddWithValue("@TxnDbName", txnDb);

                cmd.Parameters.AddWithValue("@RptDbName", rptDb);
                cmd.Parameters.AddWithValue("@Status", "Active");

                await cmd.ExecuteNonQueryAsync(ct);
            }

            // 5) Return ERP URL (tenant routing will be by tenantId)
            var baseUrl = _cfg["ErpProvisioning:ErpBaseUrl"] ?? "http://localhost:5050";
            var erpUrl = $"{baseUrl}/tenant/{tenantId}";

            return (true, erpUrl);
        }

        private static async Task RestoreDbAsync(
            SqlConnection conn,
            string dbName,
            string bakPath,
            string dataLogical,
            string logLogical,
            string dataFilePath,
            string logFilePath,
            CancellationToken ct)
        {
            // If DB already exists, drop it (idempotent provisioning for dev)
            var dropSql = $@"
IF DB_ID(N'{EscapeSqlLiteral(dbName)}') IS NOT NULL
BEGIN
  ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
  DROP DATABASE [{dbName}];
END
";
            await using (var dropCmd = new SqlCommand(dropSql, conn))
            {
                await dropCmd.ExecuteNonQueryAsync(ct);
            }

            // RESTORE DATABASE ... WITH MOVE ...
            // Logical names must match RESTORE FILELISTONLY output. [2](https://stackoverflow.com/questions/79834574/authentication-not-working-in-swagger-with-net-10)
            var restoreSql = $@"
RESTORE DATABASE [{dbName}]
FROM DISK = N'{EscapeSqlLiteral(bakPath)}'
WITH
  MOVE N'{EscapeSqlLiteral(dataLogical)}' TO N'{EscapeSqlLiteral(dataFilePath)}',
  MOVE N'{EscapeSqlLiteral(logLogical)}'  TO N'{EscapeSqlLiteral(logFilePath)}',
  REPLACE,
  RECOVERY;
";
            await using var cmd = new SqlCommand(restoreSql, conn);
            cmd.CommandTimeout = 600; // restore can take time
            await cmd.ExecuteNonQueryAsync(ct);
        }


        private static string EscapeSqlLiteral(string s) => s.Replace("'", "''");
    }
}
