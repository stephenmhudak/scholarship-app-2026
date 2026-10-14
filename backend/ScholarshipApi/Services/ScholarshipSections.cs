using SqlKata.Execution;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

/// <summary>
/// The sections scholarships manage: the cycle's shared "Scholarship Eligibility" section (every applicant answers it)
/// and each scholarship's own section (only shown to eligible applicants). Created when first needed.
/// </summary>
public static class ScholarshipSections
{
    public const string EligibilityKind = "eligibility";

    public static async Task<string> EnsureEligibilitySectionAsync(QueryFactory db, string cycleId)
    {
        var existing = await db.Query("Sections").Where("CycleId", cycleId).Where("Kind", EligibilityKind)
            .Select("Id").FirstOrDefaultAsync<string>();
        if (existing is not null) return existing;

        // Before every scholarship's own section, so applicants answer eligibility first
        var firstScholarshipOrder = await db.Query("Sections").Where("CycleId", cycleId).WhereNotNull("ScholarshipId")
            .MinAsync<int?>("Order");
        int order;
        if (firstScholarshipOrder is { } first)
        {
            await db.StatementAsync("UPDATE Sections SET `Order` = `Order` + 1 WHERE CycleId = @CycleId AND `Order` >= @First",
                new { CycleId = cycleId, First = first });
            order = first;
        }
        else
        {
            order = (await db.Query("Sections").Where("CycleId", cycleId).MaxAsync<int?>("Order") ?? -1) + 1;
        }

        var id = Guid.NewGuid().ToString();
        await db.Query("Sections").InsertAsync(new
        {
            Id = id,
            CycleId = cycleId,
            Title = "Scholarship Eligibility",
            Description = "Your answers here decide which scholarships you can apply for.",
            Order = order,
            Kind = EligibilityKind,
        });
        return id;
    }

    public static async Task<string> EnsureScholarshipSectionAsync(QueryFactory db, Scholarship scholarship)
    {
        var existing = await db.Query("Sections").Where("ScholarshipId", scholarship.Id).Select("Id").FirstOrDefaultAsync<string>();
        if (existing is not null) return existing;

        var id = Guid.NewGuid().ToString();
        await db.Query("Sections").InsertAsync(new
        {
            Id = id,
            CycleId = scholarship.CycleId,
            Title = scholarship.Name,
            scholarship.Description,
            Order = (await db.Query("Sections").Where("CycleId", scholarship.CycleId).MaxAsync<int?>("Order") ?? -1) + 1,
            ScholarshipId = scholarship.Id,
        });
        return id;
    }

    public static bool IsManaged(Section section) => section.Kind is not null || section.ScholarshipId is not null;
}
