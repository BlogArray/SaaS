//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using AspNetCore.Unobtrusive.Ajax;
using Microsoft.AspNetCore.Builder;

namespace BlogArray.SaaS.Middleware;

public static class ConfigureBlogArrayApplication
{
    public static IApplicationBuilder AddBlogArrayApplication(this IApplicationBuilder app, bool isDevelopment)
    {
        app.UseCors("AllowAllOrigins");

        app.UseCookiePolicy();

        if (isDevelopment)
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        app.UseRouting();

        app.UseStaticFiles();

        app.UseUnobtrusiveAjax();

        app.UseAuthentication();

        app.UseAuthorization();

        return app;
    }
}
