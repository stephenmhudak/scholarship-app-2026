using System.Net;
using Amazon.S3;
using Amazon.S3.Model;

namespace ScholarshipApi.Services;

// Cloudflare R2 is S3-compatible, so this uses the AWS S3 SDK pointed at the R2 endpoint.
public class R2StorageService(IAmazonS3 s3, IConfiguration config) : IFileStorageService
{
    private string Bucket => config["Storage:R2:BucketName"]
        ?? throw new InvalidOperationException("Storage:R2:BucketName is not configured.");

    public static IAmazonS3 CreateClient(IConfiguration config)
    {
        var accountId = config["Storage:R2:AccountId"];
        var accessKeyId = config["Storage:R2:AccessKeyId"];
        var secretAccessKey = config["Storage:R2:SecretAccessKey"];
        if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(accessKeyId) || string.IsNullOrWhiteSpace(secretAccessKey))
            throw new InvalidOperationException("Storage:R2 AccountId, AccessKeyId and SecretAccessKey must be configured.");

        return new AmazonS3Client(accessKeyId, secretAccessKey, new AmazonS3Config
        {
            ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
            AuthenticationRegion = "auto",
            ForcePathStyle = true,
            // R2 doesn't support every checksum the SDK sends by default; only send them when required.
            RequestChecksumCalculation = Amazon.Runtime.RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = Amazon.Runtime.ResponseChecksumValidation.WHEN_REQUIRED
        });
    }

    public async Task<string> StoreAsync(IFormFile file, string folder)
    {
        var key = $"{folder}/{Guid.NewGuid():N}";

        await using var stream = file.OpenReadStream();
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = Bucket,
            Key = key,
            InputStream = stream,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            // R2 rejects streaming (chunked) SigV4 payload signing.
            DisablePayloadSigning = true
        });

        return key;
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> RetrieveAsync(string storagePath, string originalFileName)
    {
        try
        {
            var response = await s3.GetObjectAsync(Bucket, storagePath);
            var contentType = response.Headers.ContentType ?? "application/octet-stream";
            return (response.ResponseStream, contentType, originalFileName);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException("File not found.");
        }
    }

    public async Task DeleteAsync(string storagePath)
    {
        // S3/R2 DeleteObject is idempotent — no error if the key is already gone.
        await s3.DeleteObjectAsync(Bucket, storagePath);
    }
}
