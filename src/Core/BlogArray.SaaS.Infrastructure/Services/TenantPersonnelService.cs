using System.Data;
using BlogArray.SaaS.Infrastructure.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace BlogArray.SaaS.Infrastructure.Services;

public interface ITenantPersonnelService
{
    Task<bool> TestConnectionAsync(string connectionString);

    Task EnablePersonnelInTenantAsync(string email, string connectionString);

    Task DisablePersonnelsInTenantAsync(IReadOnlyCollection<string> emails, string connectionString);

    Task DisablePersonnelInTenantsAsync(IReadOnlyCollection<string> connectionStrings, string email);
}

public class TenantPersonnelService(IDbConnectionFactory connectionFactory, IConfiguration configuration) : ITenantPersonnelService
{
    public async Task<bool> TestConnectionAsync(string connectionString)
    {
        // When "Tenants:AllowedSqlHosts" is configured (semicolon-separated), connection
        // strings may only target those SQL hosts. This prevents the server from being used
        // to probe or relay credentials to arbitrary hosts on the internal network. When the
        // setting is absent (development default), any host is allowed.
        if (!IsHostAllowed(connectionString))
        {
            return false;
        }

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

    private bool IsHostAllowed(string connectionString)
    {
        string? allowedHosts = configuration.GetValue<string?>("Tenants:AllowedSqlHosts");

        if (string.IsNullOrWhiteSpace(allowedHosts))
        {
            return true;
        }

        try
        {
            string dataSource = new SqlConnectionStringBuilder(connectionString).DataSource;
            string host = NormalizeHost(dataSource);

            foreach (string allowed in allowedHosts.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (string.Equals(host, NormalizeHost(allowed), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch (ArgumentException)
        {
            // Malformed connection string.
            return false;
        }

        return false;
    }

    private static string NormalizeHost(string dataSource)
    {
        string host = dataSource.Trim();

        // Strip SqlClient prefixes/suffixes: "tcp:host,port", "np:\\host\pipe", "(local)".
        if (host.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
        {
            host = host["tcp:".Length..];
        }

        int cut = host.IndexOfAny([',', '\\', ':']);

        if (cut >= 0)
        {
            host = host[..cut];
        }

        if (string.Equals(host, "(local)", StringComparison.OrdinalIgnoreCase) || host == ".")
        {
            host = "localhost";
        }

        return host.Trim().ToLowerInvariant();
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
