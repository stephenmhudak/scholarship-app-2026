namespace ScholarshipApi.Services;

public class LocalFileStorageService(IConfiguration config, IWebHostEnvironment env) : IFileStorageService
{
    private string RootPath => Path.Combine(env.ContentRootPath, config["Storage:LocalPath"] ?? "wwwroot/uploads");

    public async Task<string> StoreAsync(IFormFile file, string folder)
    {
        var dir = Path.Combine(RootPath, folder);
        Directory.CreateDirectory(dir);

        var key = Guid.NewGuid().ToString("N");
        var storagePath = Path.Combine(folder, key);
        var fullPath = Path.Combine(RootPath, storagePath);

        using var stream = File.Create(fullPath);
        await file.CopyToAsync(stream);

        return storagePath;
    }

    public Task<(Stream Stream, string ContentType, string FileName)> RetrieveAsync(string storagePath, string originalFileName)
    {
        var fullPath = Path.Combine(RootPath, storagePath);
        if (!File.Exists(fullPath)) throw new KeyNotFoundException("File not found.");

        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult((stream, "application/octet-stream", originalFileName));
    }

    public Task DeleteAsync(string storagePath)
    {
        var fullPath = Path.Combine(RootPath, storagePath);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }
}
