using SqlKata.Execution;

namespace ScholarshipApi.Services;

/// <summary>
/// Ends a user's sessions after an account change. Tokens carry the <c>Users.SessionVersion</c> they were
/// issued with and Program.cs rejects any that no longer match.
/// </summary>
public static class SessionVersion
{
    public const string Claim = "sv";

    public static async Task BumpAsync(QueryFactory db, string userId)
    {
        await db.StatementAsync("UPDATE Users SET SessionVersion = SessionVersion + 1 WHERE Id = @Id", new { Id = userId });
        // A sign-in waiting on its second factor was started under the old account state too
        await db.Query("TwoFactorChallenges").Where("UserId", userId).WhereNull("UsedAt").UpdateAsync(new { UsedAt = DateTime.UtcNow });
    }
}
