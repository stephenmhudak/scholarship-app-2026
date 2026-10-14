namespace ScholarshipApi.Authorization;

public static class AppRoles
{
    public const string SuperAdmin = "super_admin";

    /// <summary>
    /// Claim on a session token issued while a super admin impersonates someone: the super admin's user id.
    /// The token is otherwise the impersonated user's, so everything behaves as it would for them.
    /// </summary>
    public const string ImpersonatorClaim = "imp";
    /// <summary>The impersonating super admin's session version, so ending their sessions ends the impersonation too.</summary>
    public const string ImpersonatorSessionClaim = "imp_sv";

    /// <summary>Anything an app admin can do, a super admin can too.</summary>
    public static bool IsAdmin(string? role) => role is "app_admin" or SuperAdmin;
}
