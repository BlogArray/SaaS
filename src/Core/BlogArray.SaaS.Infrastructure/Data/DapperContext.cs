using Microsoft.Data.SqlClient;
using System.Data;

namespace BlogArray.SaaS.Infrastructure.Data;

public static class DapperContext
{
    public static IDbConnection CreateConnection(string connectionString)
    {
        return new SqlConnection(connectionString);
    }
}
