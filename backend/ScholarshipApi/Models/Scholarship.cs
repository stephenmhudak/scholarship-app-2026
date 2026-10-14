namespace ScholarshipApi.Models;

public class Scholarship
{
    public string Id { get; set; } = null!;
    public string CycleId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    /// <summary><c>open</c> (everyone is eligible) or <c>conditional</c>.</summary>
    public string Eligibility { get; set; } = "open";
    /// <summary><c>any</c> or <c>all</c> of the conditions must match.</summary>
    public string EligibilityMatch { get; set; } = "any";
    public int Order { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ScholarshipCondition
{
    public string Id { get; set; } = null!;
    public string ScholarshipId { get; set; } = null!;
    public string QuestionId { get; set; } = null!;
    public string? OptionId { get; set; }
    public string? SchoolId { get; set; }
    public string? TextContains { get; set; }
}

public class ScholarshipAward
{
    public string Id { get; set; } = null!;
    public string ScholarshipId { get; set; } = null!;
    public string ApplicationId { get; set; } = null!;
    public decimal? Amount { get; set; }
    public string? Note { get; set; }
    public string? AwardedById { get; set; }
    public DateTime AwardedAt { get; set; }
}
