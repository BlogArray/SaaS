//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Data;
using Microsoft.Data.SqlClient;

namespace BlogArray.SaaS.Infrastructure.Data;

public static class DapperContext
{
    public static IDbConnection CreateConnection(string connectionString)
    {
        return new SqlConnection(connectionString);
    }
}
