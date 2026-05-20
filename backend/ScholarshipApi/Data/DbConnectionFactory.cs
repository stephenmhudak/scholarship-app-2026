using System.Data;
using MySqlConnector;

namespace ScholarshipApi.Data;

public interface IDbConnectionFactory
{
    IDbConnection Create();
}

public class MySqlConnectionFactory(IConfiguration config) : IDbConnectionFactory
{
    public IDbConnection Create()
    {
        var builder = new MySqlConnectionStringBuilder(config.GetConnectionString("Default")!)
        {
            // Return CHAR(36) UUID columns as plain strings, not Guid objects.
            // Without this, MySqlConnector maps them to Guid and Dapper fails to
            // bind them to string-typed model properties.
            GuidFormat = MySqlGuidFormat.None,
        };
        var conn = new MySqlConnection(builder.ConnectionString);
        conn.Open();
        return conn;
    }
}
