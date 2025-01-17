//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

namespace BlogArray.SaaS.Mvc.Extensions;

public static class UrlExtensions
{
    /// <summary>
    /// Builds a URL with the specified path and query parameters.
    /// </summary>
    /// <param name="baseUrl">The base URL.</param>
    /// <param name="path">The path to append to the base URL.</param>
    /// <param name="queryParams">Query parameters to add to the URL.</param>
    /// <returns>A fully constructed URL string.</returns>
    public static string BuildUrl(this string baseUrl, string path, object? queryParams = null)
    {
        UriBuilder urlBuilder = new(baseUrl.TrimEnd('/'));

        // Handle the path
        if (!string.IsNullOrEmpty(path))
        {
            urlBuilder.Path += path.TrimStart('/');
        }

        // Handle query parameters
        if (queryParams != null)
        {
            List<string> query = new();
            System.Reflection.PropertyInfo[] properties = queryParams.GetType().GetProperties();

            foreach (System.Reflection.PropertyInfo prop in properties)
            {
                object? value = prop.GetValue(queryParams);
                value = value ?? "";
                query.Add($"{Uri.EscapeDataString(prop.Name)}={Uri.EscapeDataString(value.ToString() ?? string.Empty)}");
            }

            if (query.Count > 0)
            {
                urlBuilder.Query = string.Join("&", query);
            }
        }

        return urlBuilder.Uri.ToString();
    }
}
