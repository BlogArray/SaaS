using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BlogArray.SaaS.OpenId.Entities;

namespace BlogArray.SaaS.OpenId;

public static class OpenIdDbContextExtensions
{

    public static void AddAspNetIdentityModifications(this ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(s => s.Id).HasMaxLength(400);
            entity.Property(s => s.CreatedOn).HasDefaultValue(new DateTime(2024, 11, 8, 7, 23, 2, 837, DateTimeKind.Utc).AddTicks(2866));
            entity.Property(s => s.CreatedById).HasMaxLength(400);
            entity.Property(s => s.UpdatedById).HasMaxLength(400);

            entity.HasIndex(b => b.Email).IsUnique();

            entity.HasOne(u => u.CreatedBy)
                .WithMany()
                .HasForeignKey(u => u.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(u => u.UpdatedBy)
                .WithMany()
                .HasForeignKey(u => u.UpdatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApplicationRole>(entity =>
        {
            entity.Property(s => s.Id).HasMaxLength(400);
            entity.Property(s => s.Description).HasMaxLength(512);
        });

        builder.Entity<IdentityRoleClaim<string>>(entity =>
        {
            entity.Property(s => s.Id).HasMaxLength(400);
            entity.Property(s => s.RoleId).HasMaxLength(400);
        });

        builder.Entity<IdentityUserClaim<string>>(entity =>
        {
            entity.Property(s => s.Id).HasMaxLength(400);
            entity.Property(s => s.UserId).HasMaxLength(400);
        });

        builder.Entity<IdentityUserLogin<string>>(entity =>
        {
            entity.Property(s => s.UserId).HasMaxLength(400);
        });

        builder.Entity<IdentityUserRole<string>>(entity =>
        {
            entity.Property(s => s.RoleId).HasMaxLength(400);
            entity.Property(s => s.UserId).HasMaxLength(400);
        });

        builder.Entity<IdentityUserToken<string>>(entity =>
        {
            entity.Property(s => s.UserId).HasMaxLength(400);
        });

    }

    public static void AddOpenIdModifications(this ModelBuilder builder)
    {
        builder.Entity<OpenIdAuthorization>(entity =>
        {
            entity.HasOne(u => u.SubjectUser)
                .WithMany()
                .HasForeignKey(u => u.Subject)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OpenIdApplication>(entity =>
        {
            entity.Property(s => s.CreatedOn).HasDefaultValue(new DateTime(2024, 11, 8, 7, 23, 2, 837, DateTimeKind.Utc).AddTicks(2866));
            entity.Property(s => s.CreatedById).HasMaxLength(400);
            entity.Property(s => s.UpdatedById).HasMaxLength(400);

            entity.HasOne(u => u.Admin)
                .WithMany()
                .HasForeignKey(u => u.AdminId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(u => u.CreatedBy)
                .WithMany()
                .HasForeignKey(u => u.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(u => u.UpdatedBy)
                .WithMany()
                .HasForeignKey(u => u.UpdatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.UseOpenIddict<OpenIdApplication, OpenIdAuthorization, OpenIdScope, OpenIdToken, string>();

    }

    public static void IdentityDbContextSeed(this ModelBuilder builder)
    {
        builder.Entity<ApplicationRole>().HasData(
            new ApplicationRole
            {
                Id = "7b7a2de3-52b0-40cd-b074-e9cfc26aff96",
                Name = "Superuser",
                NormalizedName = "SUPERUSER",
                ConcurrencyStamp = "828849a7-8073-4635-bbff-800e707074d4",
                Description = "Has access to all portals and all operations",
                SystemDefined = true
            });

        builder.Entity<ApplicationUser>().HasData(
                new ApplicationUser
                {
                    Id = "16d81679-26ad-4ea7-8f93-1a12268ba340",
                    Email = "admin@blogarray.net",
                    NormalizedEmail = "admin@blogarray.net".ToUpper(),
                    FirstName = "BlogArray",
                    LastName = "Admin",
                    DisplayName = "BlogArray Admin",
                    UserName = "admin@blogarray.net",
                    NormalizedUserName = "admin@blogarray.net".ToUpper(),
                    PasswordHash = "AQAAAAIAAYagAAAAEMphxjtx+fKVBJSZzLJT93uQaoXqSWVatXtuQbcetTm74FKfrS991vNxb1nbZJkudw==",
                    Gender = "Male",
                    TimeZone = "AUS Eastern Standard Time",
                    LocaleCode = "en-IN",
                    ProfileImage = "/_content/BlogArray.SaaS.Resources/resources/images/user-icon.webp",
                    AccessFailedCount = 0,
                    LockoutEnabled = false,
                    CreatedOn = new DateTime(2022, 7, 8, 16, 37, 32, 163, DateTimeKind.Utc).AddTicks(7893),
                    EmailConfirmed = true,
                    ConcurrencyStamp = "828849a7-8073-4635-bbff-800e707074d4",
                    SecurityStamp = "6OSIMZ5JEKWSK7SC7ZSANW3WTV2KPCA7"
                }
            );

        builder.Entity<IdentityUserRole<string>>().HasData(
               new IdentityUserRole<string> { RoleId = "7b7a2de3-52b0-40cd-b074-e9cfc26aff96", UserId = "16d81679-26ad-4ea7-8f93-1a12268ba340" }
           );
    }

}

