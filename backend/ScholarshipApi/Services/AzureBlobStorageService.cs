using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace ScholarshipApi.Services;

public class AzureBlobStorageService(IConfiguration config) : IFileStorageService
{
    private BlobContainerClient GetContainer()
    {
        var client = new BlobServiceClient(config["Storage:AzureBlobConnectionString"]);
        return client.GetBlobContainerClient(config["Storage:ContainerName"]);
    }

    public async Task<string> StoreAsync(IFormFile file, string folder)
    {
        var container = GetContainer();
        await container.CreateIfNotExistsAsync(PublicAccessType.None);

        var key = $"{folder}/{Guid.NewGuid():N}";
        var blob = container.GetBlobClient(key);
        await blob.UploadAsync(file.OpenReadStream(), new BlobHttpHeaders { ContentType = file.ContentType });
        return key;
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> RetrieveAsync(string storagePath, string originalFileName)
    {
        var blob = GetContainer().GetBlobClient(storagePath);
        var response = await blob.DownloadAsync();
        var props = await blob.GetPropertiesAsync();
        return (response.Value.Content, props.Value.ContentType, originalFileName);
    }

    public async Task DeleteAsync(string storagePath)
    {
        var blob = GetContainer().GetBlobClient(storagePath);
        await blob.DeleteIfExistsAsync();
    }
}
