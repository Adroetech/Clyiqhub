namespace TenantPortal.Application.Common.Settings
{
    public sealed class EmailSettings
    {
        public bool Enabled { get; set; }
        public string Host { get; set; } = default!;
        public int Port { get; set; } = 587;
        public bool UseSsl { get; set; } = true;
        public string User { get; set; } = default!;
        public string Password { get; set; } = default!;
        public string From { get; set; } = default!;
    }
}