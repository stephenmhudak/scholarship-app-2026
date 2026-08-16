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

        var permissions = await GetPermissionsForRoleAsync(user.Role);
        return BuildAuthResponse(user, permissions);
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

            if (string.IsNullOrWhiteSpace(request.SchoolName))
                throw new ArgumentException("School name is required.");

            var newSchoolId = Guid.NewGuid().ToString();
            await db.Query("Schools").InsertAsync(new
            {
                Id = newSchoolId,
                Name = request.SchoolName.Trim(),
                AddressLine1 = request.SchoolAddressLine1?.Trim() ?? string.Empty,
                AddressLine2 = request.SchoolAddressLine2?.Trim() ?? string.Empty,
                City = request.SchoolCity?.Trim() ?? string.Empty,
                State = request.SchoolState?.Trim() ?? string.Empty,
                Zip = request.SchoolZip?.Trim() ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            });

            role = "school_admin";
            schoolId = newSchoolId;
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
            await db.Query("SchoolAdminInvites").Where("Id", inviteId).UpdateAsync(new { UsedAt = DateTime.UtcNow, SchoolId = schoolId });

        var user = await db.Query("Users").Where("Id", id).FirstAsync<User>();
        var permissions = await GetPermissionsForRoleAsync(user.Role);
        return BuildAuthResponse(user, permissions);
    }

    public async Task<InviteInfoResponse> GetInviteAsync(string token)
    {
        var row = await db.Query("SchoolAdminInvites")
            .Select("ExpiresAt", "UsedAt")
            .Where("Token", token)
            .FirstOrDefaultAsync<dynamic>()
            ?? throw new KeyNotFoundException("Invite not found.");

        if (row.UsedAt is not null)
            throw new ArgumentException("This invite has already been used.");

        if ((DateTime)row.ExpiresAt < DateTime.UtcNow)
            throw new ArgumentException("This invite has expired.");

        return new InviteInfoResponse { ExpiresAt = (DateTime)row.ExpiresAt };
    }

    public async Task<User> GetCurrentUserAsync(string userId)
    {
        var user = await db.Query("Users").Where("Id", userId).FirstOrDefaultAsync<User>();
        return user ?? throw new KeyNotFoundException("User not found.");
    }

    public async Task<IEnumerable<string>> GetPermissionsForRoleAsync(string roleName)
    {
        var rows = await db.Query("Permissions as p")
            .Join("RolePermissions as rp", "rp.PermissionId", "p.Id")
            .Join("Roles as r", "r.Id", "rp.RoleId")
            .Where("r.Name", roleName)
            .Select("p.Name")
            .GetAsync<dynamic>();
        return rows.Select(r => (string)r.Name).ToList();
    }

    private AuthResponse BuildAuthResponse(User user, IEnumerable<string> permissions)
    {
        var permList = permissions.ToList();
        var token = GenerateToken(user, permList);
        return new AuthResponse
        {
            Token = token,
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            SchoolId = user.SchoolId,
            Permissions = permList.ToArray()
        };
    }

    private string GenerateToken(User user, List<string> permissions)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(double.Parse(config["Jwt:ExpiresInMinutes"]!));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.Role, user.Role),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("firstName", user.FirstName),
            new("lastName", user.LastName)
        };
        if (!string.IsNullOrEmpty(user.SchoolId))
            claims.Add(new Claim("schoolId", user.SchoolId));
        foreach (var p in permissions)
            claims.Add(new Claim("permission", p));

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
