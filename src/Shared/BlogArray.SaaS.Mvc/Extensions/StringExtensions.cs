using System.Web;

namespace BlogArray.SaaS.Mvc.Extensions;

public static class StringExtensions
{
    public static string LoadImageString(string path)
    {
        return MakeUrl("AppSettings.StorageEndPoint", path);
    }

    public static string MakeUrl(string host, string path)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out _))
        {
            return path;
        }

        UriBuilder uriBuilder = new(new Uri(host))
        {
            Path = path
        };

        return uriBuilder.ToString();
    }

    /// <summary>
    /// Extracts the value of a specified key from a query string.
    /// </summary>
    /// <param name="queryString">
    /// The query string containing the parameters. 
    /// Example: "/connect/authorize?client_id=tenantsuite&redirect_uri=https%3A%2F%2Fwww.console.blogarray.dev%2Fsignin-oidc"
    /// </param>
    /// <param name="key">The key whose value you want to extract (e.g., "client_id").</param>
    /// <returns>
    /// The value associated with the specified key, or <c>null</c> if the key is not found or the query string is invalid.
    /// </returns>
    /// <example>
    /// <code>
    /// string queryString = "/connect/authorize?client_id=tenantsuite&redirect_uri=https%3A%2F%2Fwww.console.blogarray.dev%2Fsignin-oidc";
    /// string value = QueryStringHelper.GetParam(queryString, "client_id");
    /// Console.WriteLine(value); // Output: tenantsuite
    /// </code>
    /// </example>
    public static string? GetParam(string queryString, string key)
    {
        if (string.IsNullOrEmpty(queryString) || string.IsNullOrEmpty(key))
            return null;

        // Ensure the query string contains a '?'
        int questionMarkIndex = queryString.IndexOf('?');
        if (questionMarkIndex == -1)
            return null; // No query string part to parse

        // Extract the actual query string
        string actualQueryString = queryString.Substring(questionMarkIndex + 1);

        // Parse the query string
        var queryParams = HttpUtility.ParseQueryString(actualQueryString);

        // Retrieve the value for the given key
        return queryParams[key];
    }
}
