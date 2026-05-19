namespace ScholarshipApi.DTOs.Reference;

public class ReferenceDto
{
    public string Id { get; set; } = null!;
    public string ApplicationId { get; set; } = null!;
    public string? Label { get; set; }
    public string Status { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ReferenceWithCodeDto : ReferenceDto
{
    public string PlaintextCode { get; set; } = null!;
}

public class ReferencePublicDto
{
    public string ApplicantName { get; set; } = null!;
    public string? Label { get; set; }
}
