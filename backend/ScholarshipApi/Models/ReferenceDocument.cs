namespace ScholarshipApi.Models;

public class ReferenceDocument
{
    public string Id { get; set; } = null!;
    public string ReferenceId { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string StoragePath { get; set; } = null!;
    public DateTime UploadedAt { get; set; }
}
