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
        var conn = new MySqlConnection(config.GetConnectionString("Default"));
        conn.Open();
        return conn;
    }
}
