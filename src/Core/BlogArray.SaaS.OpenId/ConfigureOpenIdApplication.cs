﻿using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BlogArray.SaaS.OpenId;

public static class ConfigureOpenIdApplication
{
    public static async Task<IApplicationBuilder> AddOpenIdServer(this IApplicationBuilder app)
    {
        using IServiceScope scope = app.ApplicationServices.CreateScope();

        OpenIdDbContext dbContext = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();

        if (dbContext.Database.GetPendingMigrations().Any())
        {
            await dbContext.Database.MigrateAsync();
        }

        return app;
    }
}