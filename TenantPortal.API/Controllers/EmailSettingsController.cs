using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TenantPortal.API.Auth;
using TenantPortal.Application.DTOs.Settings;
using TenantPortal.Application.Exceptions;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Domain.Entities;
using TenantPortal.Infrastructure.Services;

namespace TenantPortal.API.Controllers
{
    [ApiController]
    [Route("api/settings/email")]
    [Authorize]
    public class EmailSettingsController : ControllerBase
    {
        private readonly IEmailSettingsRepository _repo;
        private readonly ISecretProtector _protector;
        private readonly IEmailSender _sender;

        public EmailSettingsController(IEmailSettingsRepository repo, ISecretProtector protector, IEmailSender sender)
        {
            _repo = repo;
            _protector = protector;
            _sender = sender;
        }

        private string RoleSlug => User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value ?? "";

        private void RequireOwner()
        {
            if (!RoleSlug.Equals("owner", StringComparison.OrdinalIgnoreCase))
                throw new ForbiddenException("Only Owner can manage email settings.");
        }

        [HttpGet]
        [RequirePermission("tenant_management", "view")]
        public async Task<IActionResult> Get(CancellationToken ct)
        {
            RequireOwner();

            var s = await _repo.GetAsync(ct);
            if (s == null) return Ok(null);

            return Ok(new EmailSettingsDto
            {
                SenderName = s.SenderName,
                SenderEmail = s.SenderEmail,
                SmtpDriver = s.SmtpDriver,
                SmtpHost = s.SmtpHost,
                SmtpPort = s.SmtpPort,
                SmtpUsername = s.SmtpUsername,
                SmtpEncryption = s.SmtpEncryption,
                IsEnabled = s.IsEnabled,
                SmtpPasswordMasked = "********"
            });
        }

        [HttpPut]
        [RequirePermission("tenant_management", "view")]
        public async Task<IActionResult> Update([FromBody] UpdateEmailSettingsRequest req, CancellationToken ct)
        {
            RequireOwner();

            var entity = new EmailSetting
            {
                Id = 1,
                SenderName = req.SenderName.Trim(),
                SenderEmail = req.SenderEmail.Trim(),
                SmtpDriver = req.SmtpDriver.Trim(),
                SmtpHost = req.SmtpHost.Trim(),
                SmtpPort = req.SmtpPort,
                SmtpUsername = req.SmtpUsername.Trim(),
                SmtpPasswordEncrypted = _protector.Protect(req.SmtpPassword),
                SmtpEncryption = req.SmtpEncryption.Trim(),
                IsEnabled = req.IsEnabled,
                UpdatedAt = DateTime.UtcNow
            };

            await _repo.UpsertAsync(entity, ct);
            return Ok(new { ok = true });
        }

        [HttpPost("test")]
        [RequirePermission("tenant_management", "view")]
        public async Task<IActionResult> Test([FromBody] TestEmailRequest req, CancellationToken ct)
        {
            RequireOwner();

            await _sender.SendAsync(
                req.ToEmail.Trim(),
                "SMTP Test - ClyiqHub",
                "This is a test email from ClyiqHub SMTP settings.",
                null,
                null,
                ct);

            return Ok(new { ok = true });
        }
    }
}