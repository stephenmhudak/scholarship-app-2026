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
    /// <summary>What it's worth: pre-fills awards, and fills {{award_amount}} when an award has no amount of its own.</summary>
    public decimal? AwardAmount { get; set; }
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

public class LetterTemplate
{
    public string Id { get; set; } = null!;
    public string CycleId { get; set; } = null!;
    /// <summary>Set on a scholarship's own letter; null on the cycle's base letters.</summary>
    public string? ScholarshipId { get; set; }
    public string Name { get; set; } = null!;
    public string Body { get; set; } = null!;
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedById { get; set; }
}
