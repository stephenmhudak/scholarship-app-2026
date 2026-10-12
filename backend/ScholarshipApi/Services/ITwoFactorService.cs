using ScholarshipApi.DTOs.Auth;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

public interface ITwoFactorService
{
    Task<TwoFactorStatusResponse> GetStatusAsync(string userId);
    Task<TwoFactorSetupResponse> BeginSetupAsync(string userId);
    Task<TwoFactorEnableResponse> EnableAsync(string userId, string code);
    Task BeginEmailSetupAsync(string userId);
    Task EnableEmailAsync(string userId, string code);
    Task IssueEmailLoginCodeAsync(User user, string challengeId);
    Task DisableAsync(string userId, string password);
    Task<TwoFactorEnableResponse> RegenerateRecoveryCodesAsync(string userId, string password);
    Task ResetByAdminAsync(string userId);
    Task<bool> VerifyLoginCodeAsync(User user, string code, string challengeId);
}
