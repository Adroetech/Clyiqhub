using Microsoft.EntityFrameworkCore;
using TenantPortal.Application.DTOs.Tenant;

using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Repositories
{
    public class TenantDetailRepository : ITenantDetailRepository
    {
        private readonly AppDbContext _db;
        public TenantDetailRepository(AppDbContext db) => _db = db;

        private const string TENANT_ACTIVE = "Active";
        private const string SUB_ACTIVE = "Active";
        private static readonly string[] TICKET_OPEN = new[] { "Open", "In-progress" };
        private static readonly string[] INVOICE_OUT = new[] { "unpaid", "overdue" };

        public async Task<TenantDetailPageDto?> GetTenantDetailPageAsync(Guid tenantId, CancellationToken ct)
        {
            var t = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
            if (t == null) return null;

            // Summary tiles
            var activeSubs = await _db.TenantSubscriptions.AsNoTracking()
                .CountAsync(s => s.TenantId == tenantId && s.Status == SUB_ACTIVE, ct);

            var openTickets = await _db.SupportTickets.AsNoTracking()
                .CountAsync(s => s.TenantId == tenantId && TICKET_OPEN.Contains(s.Status), ct);

            // Outstanding invoice BALANCE (sum of Total)
            var outstandingBalance = await _db.Invoices.AsNoTracking()
                .Where(i => i.TenantId == tenantId && INVOICE_OUT.Contains(i.Status))
                .SumAsync(i => (decimal?)i.Total, ct) ?? 0m;

            return new TenantDetailPageDto
            {
                TenantId = t.TenantId,
                TenantName = t.TenantName,
                Status = t.Status.ToString(),
                Domain = t.Domain,
                EmailDomain = t.EmailDomain,
                Email = t.Email,
                CountryId = t.CountryId,
                City = t.CityId,
                Address = t.Address,
                PrimaryContact = t.PrimaryContact,
                ContactNumber = t.ContactNumber,
                AutoRenewal = t.AutoRenewal,
                StartDate = t.StartDate,
                LanguageId = t.LanguageId,
                BackupPlan = t.BackupPlan.ToString(),
                Timezone = t.Timezone,
                Summary = new TenantDetailSummaryDto
                {
                    ActiveSubscriptionsCount = activeSubs,
                    OpenSupportTicketsCount = openTickets,
                    OutstandingInvoiceBalance = outstandingBalance
                }
            };
        }

        public async Task<IReadOnlyList<TenantSubscriptionRowDto>> GetSubscriptionsAsync(Guid tenantId, CancellationToken ct)
        {
            return await _db.TenantSubscriptions.AsNoTracking()
                .Where(s => s.TenantId == tenantId)
                .OrderByDescending(s => s.ExpiryDate)
                .Select(s => new TenantSubscriptionRowDto
                {
                    SubscriptionId = s.SubscriptionId,
                    SubscriptionName = s.SubscriptionName,
                    TotalQty = s.TotalQty,
                    UsedQty = s.UsedQty,
                    AvailableQty = s.AvailableQty,
                    Billing = s.Billing,
                    Term = s.Term,
                    Price = s.Price,
                    StartDate = s.StartDate,
                    ExpiryDate = s.ExpiryDate,
                    Status = s.Status,
                    SuspendedDate = s.SuspendedDate
                })
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<TenantSupportTicketRowDto>> GetSupportTicketsAsync(Guid tenantId, CancellationToken ct)
        {
            return await _db.SupportTickets.AsNoTracking()
                .Where(x => x.TenantId == tenantId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new TenantSupportTicketRowDto
                {
                    SupportTicketsId = x.SupportTicketsId,
                    Title = x.Title,
                    Status = x.Status,
                    Priority = x.Priority,
                    ReportedDate = x.ReportedDate,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<TenantInvoiceRowDto>> GetInvoicesAsync(Guid tenantId, CancellationToken ct)
        {
            return await _db.Invoices.AsNoTracking()
                .Where(x => x.TenantId == tenantId)
                .OrderByDescending(x => x.IssuedDate)
                .Select(x => new TenantInvoiceRowDto
                {
                    InvoiceId = x.InvoiceId,
                    InvoiceNumber = x.InvoiceNumber,
                    Total = x.Total,
                    Status = x.Status,
                    DueDate = x.DueDate,
                    IssuedDate = x.IssuedDate
                })
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<TenantUserRowDto>> GetUsersAsync(Guid tenantId, CancellationToken ct)
        {
            var rows = await _db.SysUserInfo.AsNoTracking()
                .Where(u => u.TenantId == tenantId && !u.IsDeleted)
                .OrderBy(u => u.Email)
                .Select(u => new { u.UserId, u.Email, RoleName = u.Role.Name, u.Status, u.LastLoginAt })
                .ToListAsync(ct);

            return rows.Select(u => new TenantUserRowDto
            {
                UserId = u.UserId,
                Email = u.Email,
                Role = u.RoleName,
                Status = u.Status.ToString(),
                LastLoginAt = u.LastLoginAt
            }).ToList();
        }

        public async Task<IReadOnlyList<TenantAgreementRowDto>> GetAgreementsAsync(Guid tenantId, CancellationToken ct)
        {
            return await _db.Agreements.AsNoTracking()
                .Where(x => x.TenantId == tenantId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new TenantAgreementRowDto
                {
                    AgreementId = x.AgreementId,
                    ContractType = x.ContractType,
                    StartDate = x.StartDate,
                    EndDate = x.EndDate,
                    PaymentTerm = x.PaymentTerm,
                    Email = x.Email,
                    DocumentPath = x.DocumentPath,
                    Status = (int)x.Status,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<TenantOrderRowDto>> GetOrdersAsync(Guid tenantId, CancellationToken ct)
        {
            return await _db.Orders.AsNoTracking()
                .Where(x => x.TenantId == tenantId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new TenantOrderRowDto
                {
                    OrderId = x.OrderId,
                    SalesOrderNo = x.SalesOrderNo,
                    AgreementId = x.ContractId,
                    Domain = x.Domain,
                    SalesDate = x.SalesDate,
                    Qty = x.Qty,
                    TotalPayable = x.TotalPayable,
                    NetPayable = x.NetPayable,
                    Status = (int)x.Status,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync(ct);
        }
    }
}