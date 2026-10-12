using SqlKata;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

/// <summary>
/// When a cycle accepts applications. Open/close dates are picked as whole days (stored at midnight),
/// so a cycle stays open through the end of its close date (UTC).
/// </summary>
public static class CycleWindow
{
    public static DateTime ClosesAt(DateTime closeDate) => closeDate.Date.AddDays(1);

    public static bool IsClosed(ScholarshipCycle cycle, DateTime nowUtc) => nowUtc >= ClosesAt(cycle.CloseDate);

    /// <summary>Ids of cycles whose close date has passed, for use in queries.</summary>
    // CloseDate < today ⇔ the end of the close date has passed (see ClosesAt).
    public static Query ClosedCycleIds(DateTime nowUtc) =>
        new Query("ScholarshipCycles").Where("CloseDate", "<", nowUtc.Date).Select("Id");

    public static bool IsOpen(ScholarshipCycle cycle, DateTime nowUtc) =>
        cycle.IsActive && nowUtc >= cycle.OpenDate.Date && !IsClosed(cycle, nowUtc);
}
