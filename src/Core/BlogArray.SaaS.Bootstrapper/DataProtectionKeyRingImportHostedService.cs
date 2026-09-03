//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using BlogArray.SaaS.OpenId;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlogArray.SaaS.Bootstrapper;

/// <summary>
/// One-time migration from the legacy file-based DataProtection key ring: when Local mode is
/// active, the configured KeyRingPath folder still contains key files, and the database ring
/// is empty, every legacy key is imported into the DataProtectionKeys table so previously
/// protected payloads (tenant API keys, auth cookies) keep decrypting. Runs at host startup
/// before any request is served; safe to call repeatedly.
/// </summary>
public class DataProtectionKeyRingImportHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<DataProtectionKeyRingImportHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        string? keyRingPath = configuration["DataProtection:KeyRingPath"];

        if (string.IsNullOrEmpty(keyRingPath) || !Directory.Exists(keyRingPath))
        {
            return;
        }

        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();

            OpenIdDbContext context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();

            // Only import into an empty ring: a populated database ring means the import
            // already ran, or a newer ring has been created since - either way the folder is
            // stale.
            if (await context.DataProtectionKeys.AnyAsync(cancellationToken))
            {
                return;
            }

        List<DataProtectionKey> keys = [];

        foreach (string file in Directory.GetFiles(keyRingPath, "key-*.xml"))
        {
            if (!Guid.TryParse(Path.GetFileNameWithoutExtension(file)["key-".Length..], out Guid _))
            {
                logger.LogWarning("Skipping unrecognized DataProtection key file {File}.", file);
                continue;
            }

            keys.Add(new DataProtectionKey
            {
                FriendlyName = Path.GetFileNameWithoutExtension(file),
                Xml = File.ReadAllText(file)
            });
        }

            if (keys.Count == 0)
            {
                return;
            }

            context.DataProtectionKeys.AddRange(keys);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Imported {Count} DataProtection key(s) from {Path} into the database. The folder is no longer used and can be archived.", keys.Count, keyRingPath);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A fresh database does not have the DataProtectionKeys table until the seeding
            // hosted service runs EnsureCreated; legacy payloads do not exist there anyway.
            // Any other transient failure also just defers to the next startup.
            logger.LogWarning(ex, "DataProtection key ring import from {Path} was skipped and will retry on next startup.", keyRingPath);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
