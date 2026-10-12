using ScholarshipApi.DTOs.Auth;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(LoginRequest request);
    Task<AuthResponse> CompleteTwoFactorLoginAsync(TwoFactorLoginRequest request);
    Task<RegisterResponse> RegisterAsync(RegisterRequest request);
    Task<LoginResult> VerifyEmailAsync(string token);
    Task ResendVerificationAsync(string email);
    Task ForgotPasswordAsync(string email);
    Task<LoginResult> ResetPasswordAsync(ResetPasswordWithCodeRequest request);
    Task ChangePasswordAsync(string userId, ChangePasswordRequest request);
    Task<User> GetCurrentUserAsync(string userId);
    Task<InviteInfoResponse> GetInviteAsync(string token);
    Task<IEnumerable<string>> GetPermissionsForRoleAsync(string roleName);
}
