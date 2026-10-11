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

    public static bool IsOpen(ScholarshipCycle cycle, DateTime nowUtc) =>
        cycle.IsActive && nowUtc >= cycle.OpenDate.Date && !IsClosed(cycle, nowUtc);
}
