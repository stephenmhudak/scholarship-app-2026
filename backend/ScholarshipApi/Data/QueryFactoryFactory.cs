using SqlKata.Compilers;
using SqlKata.Execution;

namespace ScholarshipApi.Data;

public static class SqlKataExtensions
{
    public static IServiceCollection AddSqlKata(this IServiceCollection services)
    {
        services.AddScoped<IDbConnectionFactory, MySqlConnectionFactory>();
        services.AddScoped<QueryFactory>(sp =>
        {
            var factory = sp.GetRequiredService<IDbConnectionFactory>();
            var conn = factory.Create();
            return new QueryFactory(conn, new MySqlCompiler());
        });
        return services;
    }
}
