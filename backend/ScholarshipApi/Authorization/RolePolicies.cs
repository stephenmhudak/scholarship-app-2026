namespace ScholarshipApi.Authorization;

public static class RolePolicies
{
    public static void Configure(Microsoft.AspNetCore.Authorization.AuthorizationOptions options)
    {
        options.AddPolicy("Applicant",   p => p.RequireRole("applicant"));
        options.AddPolicy("Scorer",        p => p.RequireRole("scorer"));
        options.AddPolicy("ScorerOrAdmin", p => p.RequireRole("scorer", "app_admin"));
        options.AddPolicy("AppAdmin",    p => p.RequireRole("app_admin"));
        options.AddPolicy("SchoolAdmin", p => p.RequireRole("school_admin"));
        options.AddPolicy("Counselor",   p => p.RequireRole("counselor"));
        options.AddPolicy("Staff",       p => p.RequireRole("scorer", "app_admin", "school_admin", "counselor"));
    }
}
