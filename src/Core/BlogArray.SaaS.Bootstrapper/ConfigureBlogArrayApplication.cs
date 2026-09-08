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

namespace BlogArray.SaaS.Bootstrapper;

public static class ConfigureBlogArrayApplication
{
    public static IApplicationBuilder AddBlogArrayApplication(this IApplicationBuilder app, bool isDevelopment)
    {
        app.UseCors("AllowedOrigins");

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

        app.UseRateLimiter();

        app.UseStaticFiles();

        app.UseUnobtrusiveAjax();

        app.UseAuthentication();

        // Strict MFA-enrollment gate: a user holding the restricted enrollment cookie but no
        // completed authentication is blocked from every page except the enrollment page,
        // logout and static assets.
        app.UseMiddleware<MfaEnrollmentMiddleware>();

        app.UseAuthorization();

        return app;
    }
}
