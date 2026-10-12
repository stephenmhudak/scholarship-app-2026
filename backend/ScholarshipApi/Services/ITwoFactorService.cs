using ScholarshipApi.DTOs.Auth;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

public interface ITwoFactorService
{
    Task<TwoFactorStatusResponse> GetStatusAsync(string userId);
    Task<TwoFactorSetupResponse> BeginSetupAsync(string userId);
    Task<TwoFactorEnableResponse> EnableAsync(string userId, string code);
    Task DisableAsync(string userId, string password);
    Task<bool> VerifyLoginCodeAsync(User user, string code);
}
