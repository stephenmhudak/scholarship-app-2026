using SqlKata.Execution;

namespace ScholarshipApi.Services;

/// <summary>
/// Archives draft applications whose cycle has closed. Runs at startup, then hourly.
/// Saving and submitting also check the cycle is open, so a draft can't be changed in the gap before it's archived.
/// </summary>
public class ApplicationArchiveService(IServiceScopeFactory scopeFactory, ILogger<ApplicationArchiveService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<QueryFactory>();
                var archived = await ArchiveClosedDraftsAsync(db, DateTime.UtcNow);
                if (archived > 0) logger.LogInformation("Archived {Count} draft application(s) in closed cycles.", archived);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to archive draft applications in closed cycles.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public static Task<int> ArchiveClosedDraftsAsync(QueryFactory db, DateTime nowUtc)
    {
        // CloseDate < today ⇔ the end of the close date has passed (see CycleWindow.ClosesAt).
        var closedCycleIds = new SqlKata.Query("ScholarshipCycles")
            .Where("CloseDate", "<", nowUtc.Date)
            .Select("Id");

        return db.Query("Applications")
            .Where("Status", "draft")
            .WhereIn("CycleId", closedCycleIds)
            .UpdateAsync(new { Status = "archived" });
    }
}
