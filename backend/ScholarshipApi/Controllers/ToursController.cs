using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlKata.Execution;

namespace ScholarshipApi.Controllers;

/// <summary>
/// Which guided page tours the signed-in user has been through (frontend/src/tours). The frontend starts a tour when
/// there's no record of it or the recorded version differs from the current one.
/// </summary>
[ApiController]
[Route("api/tours")]
[Authorize]
public partial class ToursController(QueryFactory db) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!;

    [GeneratedRegex("^[A-Za-z0-9_.:-]{1,100}$")]
    private static partial Regex TourIdPattern();

    [GeneratedRegex("^[a-z0-9]{1,64}$")]
    private static partial Regex VersionPattern();

    [HttpGet]
    public async Task<IActionResult> Mine() =>
        Ok(await db.Query("UserTours").Where("UserId", UserId)
            .Select("TourId", "Version", "Outcome", "LastSeenAt", "CompletedAt", "TimesSeen")
            .GetAsync<TourRecordDto>());

    // Records a run of a tour. Not while impersonating: the tours are the real user's to see.
    [HttpPut("{tourId}")]
    [Authorize(Policy = "NotImpersonating")]
    public async Task<IActionResult> Record(string tourId, [FromBody] RecordTourRequest request)
    {
        if (!TourIdPattern().IsMatch(tourId)) throw new ArgumentException("Unknown tour.");
        if (string.IsNullOrEmpty(request.Version) || !VersionPattern().IsMatch(request.Version))
            throw new ArgumentException("Unknown tour version.");
        if (request.Outcome is not ("completed" or "dismissed")) throw new ArgumentException("Unknown tour outcome.");

        var now = DateTime.UtcNow;
        DateTime? completedAt = request.Outcome == "completed" ? now : null;
        await db.StatementAsync("""
            INSERT INTO UserTours (UserId, TourId, Version, Outcome, LastSeenAt, CompletedAt, TimesSeen)
            VALUES (@UserId, @TourId, @Version, @Outcome, @Now, @CompletedAt, 1)
            ON DUPLICATE KEY UPDATE
                Version = VALUES(Version), Outcome = VALUES(Outcome), LastSeenAt = VALUES(LastSeenAt),
                CompletedAt = COALESCE(VALUES(CompletedAt), CompletedAt), TimesSeen = TimesSeen + 1
            """, new { UserId, TourId = tourId, request.Version, request.Outcome, Now = now, CompletedAt = completedAt });
        return NoContent();
    }

    // An admin's view of one user's tours (Users & Schools → Tour history)
    [HttpGet("~/api/admin/users/{userId}/tours")]
    [Authorize(Policy = "AppAdmin")]
    public async Task<IActionResult> ForUser(string userId)
    {
        if (!await db.Query("Users").Where("Id", userId).ExistsAsync()) throw new KeyNotFoundException("User not found.");
        return Ok(await db.Query("UserTours").Where("UserId", userId).OrderByDesc("LastSeenAt")
            .Select("TourId", "Version", "Outcome", "LastSeenAt", "CompletedAt", "TimesSeen")
            .GetAsync<TourRecordDto>());
    }
}

public class RecordTourRequest
{
    public string? Version { get; set; }
    public string? Outcome { get; set; }
}

public class TourRecordDto
{
    public string TourId { get; set; } = null!;
    public string Version { get; set; } = null!;
    public string Outcome { get; set; } = null!;
    public DateTime LastSeenAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TimesSeen { get; set; }
}
