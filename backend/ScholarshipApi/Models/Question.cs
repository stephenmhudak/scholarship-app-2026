namespace ScholarshipApi.Models;

public class Question
{
    public string Id { get; set; } = null!;
    public string CycleId { get; set; } = null!;
    public string Text { get; set; } = null!;
    public string Type { get; set; } = null!;
    public int Order { get; set; }
    public bool IsRequired { get; set; }
    public string? SectionId { get; set; }
    public string? Description { get; set; }
    public string? ValidationRules { get; set; }
    /// <summary>Set for questions on a reference form; null for application questions.</summary>
    public string? ReferenceFormId { get; set; }
    /// <summary><c>always</c>, or <c>conditional</c>: only shown when its <see cref="QuestionCondition"/>s match.</summary>
    public string Visibility { get; set; } = "always";
    /// <summary><c>any</c> or <c>all</c> of a conditional question's conditions must match.</summary>
    public string ConditionMatch { get; set; } = "any";
}

public class QuestionCondition
{
    public string Id { get; set; } = null!;
    public string QuestionId { get; set; } = null!;
    public string SourceQuestionId { get; set; } = null!;
    public string? OptionId { get; set; }
    /// <summary>For School Select questions: the school chosen.</summary>
    public string? SchoolId { get; set; }
    public string? TextContains { get; set; }
}
