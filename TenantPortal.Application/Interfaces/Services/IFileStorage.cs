namespace TenantPortal.Application.Interfaces.Services
{
    // Abstracts where uploaded documents actually land — local disk today
    // (LocalFileStorage), swappable for blob storage later without touching
    // any caller.
    public interface IFileStorage
    {
        // Returns the relative path to store in the DB (e.g. "uploads/agreements/{tenantId}/{file}").
        Task<string> SaveAsync(Stream content, string fileName, string subFolder, CancellationToken ct);

        // relativePath is the same string SaveAsync returned. Missing files are ignored.
        Task DeleteAsync(string relativePath, CancellationToken ct);
    }
}
