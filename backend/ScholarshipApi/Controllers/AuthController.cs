using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ScholarshipApi.Authorization;
using ScholarshipApi.DTOs.Auth;
using ScholarshipApi.Services;

namespace ScholarshipApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService, ITwoFactorService twoFactorService) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!;

    // These changes end the user's other sessions; this one gets a replacement token (picked up by frontend/src/services/api.js)
    private async Task RefreshSessionAsync() =>
        Response.Headers["X-Session-Token"] = await authService.IssueTokenAsync(UserId);

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await authService.LoginAsync(request);
        return Ok(result.Body);
    }

    [HttpPost("login/2fa")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> LoginTwoFactor([FromBody] TwoFactorLoginRequest request)
    {
        var result = await authService.CompleteTwoFactorLoginAsync(request);
        return Ok(result);
    }

    [HttpPost("login/2fa/resend")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> ResendTwoFactorEmail([FromBody] TwoFactorResendRequest request)
    {
        await authService.ResendTwoFactorEmailAsync(request.ChallengeToken);
        return NoContent();
    }

    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await authService.RegisterAsync(request);
        return Ok(result);
    }

    [HttpPost("verify-email")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        var result = await authService.VerifyEmailAsync(request.Token);
        return Ok(result.Body);
    }

    [HttpPost("resend-verification")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request)
    {
        await authService.ResendVerificationAsync(request.Email);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        await authService.ForgotPasswordAsync(request.Email);
        return NoContent();
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordWithCodeRequest request)
    {
        var result = await authService.ResetPasswordAsync(request);
        return Ok(result.Body);
    }

    // Invited counselors create their password from the emailed link; until then the account can't sign in
    [HttpPost("setup-password/check")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> CheckPasswordSetup([FromBody] PasswordSetupCheckRequest request) =>
        Ok(await authService.GetPasswordSetupAsync(request.Token));

    [HttpPost("setup-password")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> SetupPassword([FromBody] PasswordSetupRequest request)
    {
        var result = await authService.SetupPasswordAsync(request);
        return Ok(result.Body);
    }

    [HttpPost("change-password")]
    [Authorize(Policy = "NotImpersonating")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        await authService.ChangePasswordAsync(UserId, request);
        await RefreshSessionAsync();
        return NoContent();
    }

    [HttpGet("2fa")]
    [Authorize]
    public async Task<IActionResult> GetTwoFactorStatus()
    {
        var result = await twoFactorService.GetStatusAsync(UserId);
        return Ok(result);
    }

    [HttpPost("2fa/setup")]
    [Authorize(Policy = "NotImpersonating")]
    public async Task<IActionResult> BeginTwoFactorSetup()
    {
        var result = await twoFactorService.BeginSetupAsync(UserId);
        return Ok(result);
    }

    [HttpPost("2fa/enable")]
    [Authorize(Policy = "NotImpersonating")]
    public async Task<IActionResult> EnableTwoFactor([FromBody] TwoFactorEnableRequest request)
    {
        var result = await twoFactorService.EnableAsync(UserId, request.Code);
        await RefreshSessionAsync();
        return Ok(result);
    }

    [HttpPost("2fa/email/setup")]
    [Authorize(Policy = "NotImpersonating")]
    public async Task<IActionResult> BeginEmailTwoFactorSetup()
    {
        await twoFactorService.BeginEmailSetupAsync(UserId);
        return NoContent();
    }

    [HttpPost("2fa/email/enable")]
    [Authorize(Policy = "NotImpersonating")]
    public async Task<IActionResult> EnableEmailTwoFactor([FromBody] TwoFactorEnableRequest request)
    {
        await twoFactorService.EnableEmailAsync(UserId, request.Code);
        await RefreshSessionAsync();
        return NoContent();
    }

    [HttpPost("2fa/disable")]
    [Authorize(Policy = "NotImpersonating")]
    public async Task<IActionResult> DisableTwoFactor([FromBody] TwoFactorPasswordRequest request)
    {
        await twoFactorService.DisableAsync(UserId, request.Password);
        await RefreshSessionAsync();
        return NoContent();
    }

    [HttpPost("2fa/recovery-codes")]
    [Authorize(Policy = "NotImpersonating")]
    public async Task<IActionResult> RegenerateRecoveryCodes([FromBody] TwoFactorPasswordRequest request)
    {
        var result = await twoFactorService.RegenerateRecoveryCodesAsync(UserId, request.Password);
        return Ok(result);
    }

    // Back to the impersonating super admin's own session
    [HttpPost("end-impersonation")]
    [Authorize]
    public async Task<IActionResult> EndImpersonation()
    {
        var impersonatorId = User.FindFirstValue(AppRoles.ImpersonatorClaim)
            ?? throw new ArgumentException("You aren't impersonating anyone.");
        return Ok(await authService.EndImpersonationAsync(impersonatorId, UserId));
    }

    [HttpGet("invite/{token}")]
    [EnableRateLimiting(RateLimitPolicies.Public)]
    public async Task<IActionResult> GetInvite(string token)
    {
        var result = await authService.GetInviteAsync(token);
        return Ok(result);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var user = await authService.GetCurrentUserAsync(UserId);
        var permissions = await authService.GetPermissionsForRoleAsync(user.Role);
        return Ok(new { user.Id, user.Email, user.FirstName, user.LastName, user.Role, user.SchoolId, Permissions = permissions });
    }
}
