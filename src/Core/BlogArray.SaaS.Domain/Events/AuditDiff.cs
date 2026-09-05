//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Reflection;
using System.Text.Json;

namespace BlogArray.SaaS.Domain.Events;

/// <summary>
/// Builds the OldValue/NewValue JSON payloads for audit events: both hold only the
/// properties that actually changed, keyed by property name. Secrets must never be passed
/// through these helpers.
/// </summary>
public static class AuditDiff
{
    /// <summary>
    /// Diff of a single named property.
    /// </summary>
    public static (string? OldJson, string? NewJson) Changed(string propertyName, object? oldValue, object? newValue)
    {
        if (Equals(oldValue, newValue))
        {
            return (null, null);
        }

        return (JsonSerializer.Serialize(new Dictionary<string, object?> { [propertyName] = oldValue }),
                JsonSerializer.Serialize(new Dictionary<string, object?> { [propertyName] = newValue }));
    }

    /// <summary>
    /// Diff of two same-shaped objects (anonymous types or DTOs): only public properties
    /// whose values differ are included.
    /// </summary>
    public static (string? OldJson, string? NewJson) Changed(object? before, object? after)
    {
        if (before is null && after is null)
        {
            return (null, null);
        }

        Dictionary<string, object?> beforeProps = ToDictionary(before);
        Dictionary<string, object?> afterProps = ToDictionary(after);

        Dictionary<string, object?> oldChanged = [];
        Dictionary<string, object?> newChanged = [];

        foreach (string key in beforeProps.Keys.Union(afterProps.Keys))
        {
            beforeProps.TryGetValue(key, out object? oldValue);
            afterProps.TryGetValue(key, out object? newValue);

            if (!Equals(oldValue, newValue))
            {
                oldChanged[key] = oldValue;
                newChanged[key] = newValue;
            }
        }

        return oldChanged.Count == 0
            ? (null, null)
            : (JsonSerializer.Serialize(oldChanged), JsonSerializer.Serialize(newChanged));
    }

    private static Dictionary<string, object?> ToDictionary(object? obj)
    {
        if (obj is null)
        {
            return [];
        }

        return obj.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead)
            .ToDictionary(property => property.Name, property => property.GetValue(obj));
    }

    /// <summary>
    /// Renders Old/New JSON payloads as a compact "property: old → new" summary for display.
    /// </summary>
    public static string? Summarize(string? oldValueJson, string? newValueJson)
    {
        if (string.IsNullOrEmpty(oldValueJson) && string.IsNullOrEmpty(newValueJson))
        {
            return null;
        }

        try
        {
            Dictionary<string, JsonElement>? oldProps = string.IsNullOrEmpty(oldValueJson)
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(oldValueJson);

            Dictionary<string, JsonElement>? newProps = string.IsNullOrEmpty(newValueJson)
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(newValueJson);

            if (oldProps is null && newProps is null)
            {
                return null;
            }

            HashSet<string> keys = [];

            if (oldProps is not null)
            {
                keys.UnionWith(oldProps.Keys);
            }

            if (newProps is not null)
            {
                keys.UnionWith(newProps.Keys);
            }

            IEnumerable<string> parts = keys.Select(key =>
            {
                string? oldText = TryGetString(oldProps, key);
                string? newText = TryGetString(newProps, key);
                return $"{key}: {oldText ?? "-"} → {newText ?? "-"}";
            });

            return string.Join("; ", parts);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? TryGetString(Dictionary<string, JsonElement>? source, string key)
    {
        if (source is null || !source.TryGetValue(key, out JsonElement element) || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
    }
}
