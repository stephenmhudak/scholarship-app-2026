namespace ScholarshipApi.Models;

public class ApplicationFile
{
    public string Id { get; set; } = null!;
    public string ApplicationId { get; set; } = null!;
    public string? QuestionId { get; set; }
    public string FileName { get; set; } = null!;
    public string StoragePath { get; set; } = null!;
    public DateTime UploadedAt { get; set; }
}
