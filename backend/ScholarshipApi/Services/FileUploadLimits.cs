namespace ScholarshipApi.Services;

public static class FileUploadLimits
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    // Request body cap: the file plus headroom for multipart boundaries/headers,
    // so a file of exactly MaxFileSizeBytes isn't rejected by the request limit.
    public const long MaxRequestSizeBytes = MaxFileSizeBytes + 64 * 1024;

    public const string TooLargeMessage = "File must be 5 MB or less.";
}
