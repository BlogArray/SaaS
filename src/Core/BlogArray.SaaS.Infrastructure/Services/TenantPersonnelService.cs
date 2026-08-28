using System.Data;
using BlogArray.SaaS.Infrastructure.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BlogArray.SaaS.Infrastructure.Services;

public interface ITenantPersonnelService
{
    Task<bool> TestConnectionAsync(string connectionString);

    Task EnablePersonnelInTenantAsync(string email, string connectionString);

    Task DisablePersonnelsInTenantAsync(IReadOnlyCollection<string> emails, string connectionString);

    Task DisablePersonnelInTenantsAsync(IReadOnlyCollection<string> connectionStrings, string email);
}

public class TenantPersonnelService(IDbConnectionFactory connectionFactory) : ITenantPersonnelService
{
    public async Task<bool> TestConnectionAsync(string connectionString)
    {
        try
        {
            using SqlConnection connection = new(connectionString);
            await connection.OpenAsync();
            return true;
        }
        catch (SqlException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task EnablePersonnelInTenantAsync(string email, string connectionString)
    {
        if (string.IsNullOrEmpty(email))
        {
            return;
        }

        const string checkQuery = @"SELECT COUNT(1)
                                FROM AppPersonnels
                                WHERE Email = @Email";

        const string updateQuery = @"UPDATE AppPersonnels
                                 SET IsActive = @IsActive
                                 WHERE Email = @Email";

        const string insertQuery = @"INSERT INTO AppPersonnels (Email, IsActive)
                                 VALUES (@Email, @IsActive)";

        using IDbConnection connection = connectionFactory.CreateConnection(connectionString);

        int userExists = await connection.ExecuteScalarAsync<int>(checkQuery, new { Email = email });

        if (userExists > 0)
        {
            await connection.ExecuteAsync(updateQuery, new { Email = email, IsActive = true });
        }
        else
        {
            await connection.ExecuteAsync(insertQuery, new { Email = email, IsActive = true });
        }
    }

    public async Task DisablePersonnelsInTenantAsync(IReadOnlyCollection<string> emails, string connectionString)
    {
        if (emails.Count == 0)
        {
            return;
        }

        const string query = @"UPDATE AppPersonnels
                           SET IsActive = @IsActive
                           WHERE Email IN @Emails";

        using IDbConnection connection = connectionFactory.CreateConnection(connectionString);

        await connection.ExecuteAsync(query, new { IsActive = false, Emails = emails });
    }

    public async Task DisablePersonnelInTenantsAsync(IReadOnlyCollection<string> connectionStrings, string email)
    {
        const string query = @"UPDATE AppPersonnels
                           SET IsActive = @IsActive
                           WHERE Email = @Email";

        IEnumerable<Task> tasks = connectionStrings.Select(async connectionString =>
        {
            using IDbConnection connection = connectionFactory.CreateConnection(connectionString);
            await connection.ExecuteAsync(query, new { IsActive = false, Email = email });
        });

        await Task.WhenAll(tasks);
    }
}
