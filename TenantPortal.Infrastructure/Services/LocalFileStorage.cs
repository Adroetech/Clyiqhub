using TenantPortal.Application.Interfaces.Services;

namespace TenantPortal.Infrastructure.Services
{
    // Saves to disk under <contentRoot>/wwwroot/uploads/... — served statically
    // by UseStaticFiles() in Program.cs. _rootPath is the absolute wwwroot path,
    // injected from the API layer since only it knows IWebHostEnvironment.
    public class LocalFileStorage : IFileStorage
    {
        private readonly string _rootPath;

        public LocalFileStorage(string wwwRootPath)
        {
            _rootPath = wwwRootPath;
        }

        public async Task<string> SaveAsync(Stream content, string fileName, string subFolder, CancellationToken ct)
        {
            var safeFileName = $"{Guid.NewGuid():N}_{Path.GetFileName(fileName)}";
            var folder = Path.Combine(_rootPath, "uploads", subFolder);
            Directory.CreateDirectory(folder);

            var fullPath = Path.Combine(folder, safeFileName);
            await using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
            {
                await content.CopyToAsync(fileStream, ct);
            }

            // Forward slashes regardless of OS so the stored path works as a URL path too.
            return $"uploads/{subFolder}/{safeFileName}".Replace('\\', '/');
        }

        public Task DeleteAsync(string relativePath, CancellationToken ct)
        {
            var fullPath = Path.Combine(_rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath))
                File.Delete(fullPath);
            return Task.CompletedTask;
        }
    }
}
