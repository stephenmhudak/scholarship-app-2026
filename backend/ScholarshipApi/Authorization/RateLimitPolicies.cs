using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace ScholarshipApi.Authorization;

/// <summary>
/// Per-IP request limits for endpoints that work without signing in. Accounts are never locked;
/// over-limit requests get a 429 until the window resets.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>Password, code and email-sending endpoints: sign-in, 2FA, register, reset, verification.</summary>
    public const string Auth = "auth";
    /// <summary>Other public lookups and uploads: invite links and reference codes.</summary>
    public const string Public = "public";

    public static void Configure(RateLimiterOptions options)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (context, cancellationToken) =>
        {
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsync(
                """{"error":"Too many requests. Please wait a minute and try again."}""", cancellationToken);
        };

        options.AddPolicy(Auth, context => PerClientIp(context, permitLimit: 10));
        options.AddPolicy(Public, context => PerClientIp(context, permitLimit: 30));
    }

    // RemoteIpAddress is the real client once UseForwardedHeaders has run (see Program.cs)
    private static RateLimitPartition<string> PerClientIp(HttpContext context, int permitLimit) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = TimeSpan.FromMinutes(1) });
}
