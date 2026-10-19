using ScholarshipApi.Authorization;
using ScholarshipApi.Models;
using SqlKata.Execution;

namespace ScholarshipApi.Services;

/// <summary>
/// Creates or promotes a super admin from the command line, for the deploy scripts (nobody can register as one):
/// <c>dotnet ScholarshipApi.dll --create-super-admin</c>, with email, first name, last name and password on stdin, one
/// per line, so the password never appears in a command line or an env file. Runs after migrations, then the
/// process exits instead of serving requests.
/// <para>A new account needs every field. An existing one becomes an active, verified super admin with no school;
/// blank names or password keep what it has. Either way its sessions end.</para>
/// </summary>
public static class SuperAdminSetup
{
    public const string Argument = "--create-super-admin";

    /// <returns>The process exit code: 0 on success, 1 with the reason on stderr.</returns>
    public static async Task<int> RunAsync(QueryFactory db, TextReader input, ILogger logger)
    {
        try
        {
            var email = InputValidation.Email(input.ReadLine());
            var firstName = InputValidation.Optional(input.ReadLine(), "First name", 100);
            var lastName = InputValidation.Optional(input.ReadLine(), "Last name", 100);
            var password = input.ReadLine();
            if (!string.IsNullOrEmpty(password)) InputValidation.Password(password);
            var now = DateTime.UtcNow;

            var user = await db.Query("Users").Where("Email", email).FirstOrDefaultAsync<User>();
            if (user is null)
            {
                if (firstName is null || lastName is null || string.IsNullOrEmpty(password))
                    throw new ArgumentException("A new account needs a first name, last name and password.");
                await db.Query("Users").InsertAsync(new
                {
                    Id = Guid.NewGuid().ToString(),
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    FirstName = firstName,
                    LastName = lastName,
                    Role = AppRoles.SuperAdmin,
                    EmailVerifiedAt = now,
                    CreatedAt = now
                });
                logger.LogWarning("Created super admin {Email} from the command line", email);
                Console.WriteLine($"Created super admin {email}.");
                return 0;
            }

            // An invited account that never set a password can't sign in without one
            if (string.IsNullOrEmpty(password) && string.IsNullOrEmpty(user.PasswordHash))
                throw new ArgumentException($"{email} has no password yet: enter one.");

            await db.Query("Users").Where("Id", user.Id).UpdateAsync(new
            {
                Role = AppRoles.SuperAdmin,
                SchoolId = (string?)null,
                DeactivatedAt = (DateTime?)null,
                EmailVerifiedAt = user.EmailVerifiedAt ?? now,
                FirstName = firstName ?? user.FirstName,
                LastName = lastName ?? user.LastName,
                PasswordHash = string.IsNullOrEmpty(password) ? user.PasswordHash : BCrypt.Net.BCrypt.HashPassword(password)
            });
            if (!string.IsNullOrEmpty(password))
            {
                // Same as any password change: outstanding reset codes and setup links stop working
                await db.Query("PasswordResetCodes").Where("UserId", user.Id).WhereNull("UsedAt").UpdateAsync(new { UsedAt = now });
                await db.Query("PasswordSetupTokens").Where("UserId", user.Id).WhereNull("UsedAt").UpdateAsync(new { UsedAt = now });
            }
            await SessionVersion.BumpAsync(db, user.Id);
            logger.LogWarning("Made {Email} a super admin from the command line (was {Role})", email, user.Role);
            Console.WriteLine(user.Role == AppRoles.SuperAdmin ? $"Updated super admin {email}." : $"Made {email} a super admin (was {user.Role}).");
            return 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Super admin not set up: {ex.Message}");
            return 1;
        }
    }
}
