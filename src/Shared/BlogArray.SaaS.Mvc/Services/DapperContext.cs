using Microsoft.Data.SqlClient;
using System.Data;

namespace BlogArray.SaaS.Mvc.Services;

public class DapperContext
{
    public static IDbConnection CreateConnection(string connectionString)
    {
        return new SqlConnection(connectionString);
    }
}
