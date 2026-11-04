using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Domain.Entities;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Services
{
    public class TenantProvisioningService : ITenantProvisioningService
    {
        private readonly IConfiguration _cfg;
        private readonly AppDbContext _db;

        public TenantProvisioningService(IConfiguration cfg, AppDbContext db)
        {
            _cfg = cfg;
            _db = db;
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

            // 4) Save mapping into TenantPortal DB table ErpEnvironmentDbMap —
            // an ordinary table upsert, done via EF Core (unlike the
            // RESTORE/DROP DATABASE calls above, which are server-admin DDL
            // with no EF Core equivalent and must stay on raw ADO.NET).
            var map = await _db.ErpEnvironmentDbMaps.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
            if (map is null)
            {
                map = new ErpEnvironmentDbMap { TenantId = tenantId };
                _db.ErpEnvironmentDbMaps.Add(map);
            }

            map.EnvManagerUserId = envManagerUserId;
            map.CoreDbName = coreDb;
            map.TxnDbName = txnDb;
            map.RptDbName = rptDb;
            map.Status = "Active";

            await _db.SaveChangesAsync(ct);

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
