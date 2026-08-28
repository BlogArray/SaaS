using System.Data;
using Microsoft.Data.SqlClient;

namespace BlogArray.SaaS.Infrastructure.Data;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection(string connectionString);
}

public class SqlDbConnectionFactory : IDbConnectionFactory
{
    public IDbConnection CreateConnection(string connectionString)
    {
        return new SqlConnection(connectionString);
    }
}
