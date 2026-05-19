namespace ScholarshipApi.Services;

public interface IFileStorageService
{
    Task<string> StoreAsync(IFormFile file, string folder);
    Task<(Stream Stream, string ContentType, string FileName)> RetrieveAsync(string storagePath, string originalFileName);
    Task DeleteAsync(string storagePath);
}
