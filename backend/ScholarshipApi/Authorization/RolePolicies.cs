namespace ScholarshipApi.Authorization;

public static class RolePolicies
{
    public static void Configure(Microsoft.AspNetCore.Authorization.AuthorizationOptions options)
    {
        options.AddPolicy("Applicant",     p => p.RequireClaim("permission", "manage_application"));
        options.AddPolicy("Scorer",        p => p.RequireClaim("permission", "score_application"));
        options.AddPolicy("ScorerOrAdmin", p => p.RequireClaim("permission", "score_application"));
        options.AddPolicy("AppAdmin",      p => p.RequireClaim("permission", "admin_applications"));
        options.AddPolicy("SchoolAdmin",   p => p.RequireClaim("permission", "manage_school"));
        options.AddPolicy("Staff",         p => p.RequireClaim("permission", "view_school_data"));
        options.AddPolicy("Counselor",     p => p.RequireClaim("permission", "view_counselor_dashboard"));
        options.AddPolicy("Referee",       p => p.RequireClaim("permission", "submit_references"));
        // By role rather than permission: super admins have every permission, but only they impersonate
        options.AddPolicy("SuperAdmin",    p => p.RequireRole(AppRoles.SuperAdmin));
        // Account security changes (password, two-factor) are the real user's to make, never an impersonator's
        options.AddPolicy("NotImpersonating", p => p.RequireAuthenticatedUser()
            .RequireAssertion(ctx => !ctx.User.HasClaim(c => c.Type == AppRoles.ImpersonatorClaim)));
    }
}
