using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SqlKata.Execution;
using ScholarshipApi.DTOs.Auth;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

public class AuthService(QueryFactory db, IConfiguration config) : IAuthService
{
    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await db.Query("Users").Where("Email", request.Email).FirstOrDefaultAsync<User>();
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new ArgumentException("Invalid email or password.");

        return BuildAuthResponse(user);
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var exists = await db.Query("Users").Where("Email", request.Email).ExistsAsync();
        if (exists) throw new ArgumentException("Email is already registered.");

        string role = "applicant";
        string? schoolId = null;
        string? inviteId = null;

        if (!string.IsNullOrEmpty(request.InviteToken))
        {
            var invite = await db.Query("SchoolAdminInvites")
                .Where("Token", request.InviteToken)
                .WhereNull("UsedAt")
                .Where("ExpiresAt", ">", DateTime.UtcNow)
                .FirstOrDefaultAsync<dynamic>()
                ?? throw new ArgumentException("Invite link is invalid or has expired.");

            role = "school_admin";
            schoolId = (string)invite.SchoolId;
            inviteId = (string)invite.Id;
        }

        var id = Guid.NewGuid().ToString();
        await db.Query("Users").InsertAsync(new
        {
            Id = id,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Role = role,
            SchoolId = schoolId,
            CreatedAt = DateTime.UtcNow
        });

        if (inviteId is not null)
            await db.Query("SchoolAdminInvites").Where("Id", inviteId).UpdateAsync(new { UsedAt = DateTime.UtcNow });

        var user = await db.Query("Users").Where("Id", id).FirstAsync<User>();
        return BuildAuthResponse(user);
    }

    public async Task<InviteInfoResponse> GetInviteAsync(string token)
    {
        var row = await db.Query("SchoolAdminInvites as i")
            .Join("Schools as s", "s.Id", "i.SchoolId")
            .Select("i.SchoolId", "s.Name as SchoolName", "i.ExpiresAt", "i.UsedAt")
            .Where("i.Token", token)
            .FirstOrDefaultAsync<dynamic>()
            ?? throw new KeyNotFoundException("Invite not found.");

        if (row.UsedAt is not null)
            throw new ArgumentException("This invite has already been used.");

        if ((DateTime)row.ExpiresAt < DateTime.UtcNow)
            throw new ArgumentException("This invite has expired.");

        return new InviteInfoResponse
        {
            SchoolId = (string)row.SchoolId,
            SchoolName = (string)row.SchoolName,
            ExpiresAt = (DateTime)row.ExpiresAt
        };
    }

    public async Task<User> GetCurrentUserAsync(string userId)
    {
        var user = await db.Query("Users").Where("Id", userId).FirstOrDefaultAsync<User>();
        return user ?? throw new KeyNotFoundException("User not found.");
    }

    private AuthResponse BuildAuthResponse(User user)
    {
        var token = GenerateToken(user);
        return new AuthResponse
        {
            Token = token,
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role
        };
    }

    private string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(double.Parse(config["Jwt:ExpiresInMinutes"]!));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("firstName", user.FirstName),
            new Claim("lastName", user.LastName)
        };

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
