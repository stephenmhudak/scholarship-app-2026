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
    }
}
