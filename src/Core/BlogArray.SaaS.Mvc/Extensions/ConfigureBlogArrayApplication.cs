using AspNetCore.Unobtrusive.Ajax;
using Microsoft.AspNetCore.Builder;

namespace BlogArray.SaaS.Mvc.Extensions;

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
