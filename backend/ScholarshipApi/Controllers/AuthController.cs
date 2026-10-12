using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarshipApi.DTOs.Auth;
using ScholarshipApi.Services;

namespace ScholarshipApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService, ITwoFactorService twoFactorService) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!;

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await authService.LoginAsync(request);
        return Ok(result.Body);
    }

    [HttpPost("login/2fa")]
    public async Task<IActionResult> LoginTwoFactor([FromBody] TwoFactorLoginRequest request)
    {
        var result = await authService.CompleteTwoFactorLoginAsync(request);
        return Ok(result);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await authService.RegisterAsync(request);
        return Ok(result);
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        var result = await authService.VerifyEmailAsync(request.Token);
        return Ok(result.Body);
    }

    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request)
    {
        await authService.ResendVerificationAsync(request.Email);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        await authService.ForgotPasswordAsync(request.Email);
        return NoContent();
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordWithCodeRequest request)
    {
        var result = await authService.ResetPasswordAsync(request);
        return Ok(result.Body);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        await authService.ChangePasswordAsync(UserId, request);
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
    [Authorize]
    public async Task<IActionResult> BeginTwoFactorSetup()
    {
        var result = await twoFactorService.BeginSetupAsync(UserId);
        return Ok(result);
    }

    [HttpPost("2fa/enable")]
    [Authorize]
    public async Task<IActionResult> EnableTwoFactor([FromBody] TwoFactorEnableRequest request)
    {
        var result = await twoFactorService.EnableAsync(UserId, request.Code);
        return Ok(result);
    }

    [HttpPost("2fa/disable")]
    [Authorize]
    public async Task<IActionResult> DisableTwoFactor([FromBody] TwoFactorPasswordRequest request)
    {
        await twoFactorService.DisableAsync(UserId, request.Password);
        return NoContent();
    }

    [HttpPost("2fa/recovery-codes")]
    [Authorize]
    public async Task<IActionResult> RegenerateRecoveryCodes([FromBody] TwoFactorPasswordRequest request)
    {
        var result = await twoFactorService.RegenerateRecoveryCodesAsync(UserId, request.Password);
        return Ok(result);
    }

    [HttpGet("invite/{token}")]
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
