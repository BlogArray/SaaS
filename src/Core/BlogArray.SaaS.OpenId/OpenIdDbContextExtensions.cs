//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using BlogArray.SaaS.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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

        builder.Entity<PasswordHistory>(entity =>
        {
            entity.Property(history => history.Id).HasMaxLength(400);
            entity.Property(history => history.UserId).HasMaxLength(400);
            entity.HasIndex(history => history.UserId);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(history => history.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SignInEvent>(entity =>
        {
            entity.Property(signInEvent => signInEvent.Id).HasMaxLength(400);
            entity.Property(signInEvent => signInEvent.UserId).HasMaxLength(400);
            entity.Property(signInEvent => signInEvent.ClientId).HasMaxLength(400);
            entity.Property(signInEvent => signInEvent.EventType).HasMaxLength(100);
            entity.Property(signInEvent => signInEvent.AuthMethod).HasMaxLength(100);
            entity.Property(signInEvent => signInEvent.Result).HasMaxLength(20);
            entity.Property(signInEvent => signInEvent.Details).HasMaxLength(512);
            entity.Property(signInEvent => signInEvent.IpAddress).HasMaxLength(64);
            entity.Property(signInEvent => signInEvent.DeviceInfo).HasMaxLength(256);
            entity.Property(signInEvent => signInEvent.UserAgent).HasMaxLength(512);
            entity.HasIndex(signInEvent => new { signInEvent.UserId, signInEvent.CreatedOn });
            entity.HasIndex(signInEvent => new { signInEvent.ClientId, signInEvent.CreatedOn });
            entity.HasIndex(signInEvent => signInEvent.CreatedOn);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(signInEvent => signInEvent.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Audit rows are append-only compliance records: deliberately no FK to ApplicationUser
        // so the trail of changes an actor made survives actor deletion (hard delete or
        // anonymization never rewrites history).
        builder.Entity<AuditEvent>(entity =>
        {
            entity.Property(auditEvent => auditEvent.Id).HasMaxLength(400);
            entity.Property(auditEvent => auditEvent.UserId).HasMaxLength(400);
            entity.Property(auditEvent => auditEvent.TriggeredBy).HasMaxLength(20);
            entity.Property(auditEvent => auditEvent.TargetUserId).HasMaxLength(400);
            entity.Property(auditEvent => auditEvent.ClientId).HasMaxLength(400);
            entity.Property(auditEvent => auditEvent.EventType).HasMaxLength(100);
            entity.Property(auditEvent => auditEvent.Reason).HasMaxLength(512);
            entity.Property(auditEvent => auditEvent.Result).HasMaxLength(20);
            entity.Property(auditEvent => auditEvent.IpAddress).HasMaxLength(64);
            entity.Property(auditEvent => auditEvent.DeviceInfo).HasMaxLength(256);
            entity.Property(auditEvent => auditEvent.UserAgent).HasMaxLength(512);
            entity.HasIndex(auditEvent => new { auditEvent.ClientId, auditEvent.CreatedOn });
            entity.HasIndex(auditEvent => new { auditEvent.UserId, auditEvent.CreatedOn });
            entity.HasIndex(auditEvent => auditEvent.TargetUserId);
            entity.HasIndex(auditEvent => new { auditEvent.EventType, auditEvent.CreatedOn });
        });

        builder.Entity<WebAuthnCredential>(entity =>
        {
            entity.Property(credential => credential.Id).HasMaxLength(400);
            entity.Property(credential => credential.UserId).HasMaxLength(400);
            entity.Property(credential => credential.Name).HasMaxLength(200);
            entity.Property(credential => credential.CredentialId).HasMaxLength(1024);
            entity.Property(credential => credential.PublicKey).HasMaxLength(8192);
            entity.Property(credential => credential.Aaguid).HasMaxLength(400);
            entity.HasIndex(credential => credential.UserId);
            entity.HasIndex(credential => credential.CredentialId).IsUnique();
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(credential => credential.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserSession>(entity =>
        {
            entity.Property(userSession => userSession.Id).HasMaxLength(400);
            entity.Property(userSession => userSession.UserId).HasMaxLength(400);
            entity.Property(userSession => userSession.SessionId).HasMaxLength(400);
            entity.Property(userSession => userSession.DeviceName).HasMaxLength(200);
            entity.Property(userSession => userSession.UserAgent).HasMaxLength(512);
            entity.Property(userSession => userSession.IpAddress).HasMaxLength(64);
            entity.HasIndex(userSession => userSession.UserId);
            entity.HasIndex(userSession => userSession.SessionId).IsUnique();
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(userSession => userSession.UserId)
                .OnDelete(DeleteBehavior.Cascade);
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
            entity.Property(s => s.AdminEmail).HasMaxLength(1024);
            entity.Property(s => s.APIKeyHash).HasMaxLength(64);
            entity.Property(s => s.APIKeyProtected).HasMaxLength(1024);
            entity.Property(s => s.APIKeyPrefix).HasMaxLength(16);
            entity.HasIndex(s => s.APIKeyHash).IsUnique().HasFilter("[APIKeyHash] IS NOT NULL");

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
            },
            new ApplicationRole
            {
                Id = "910e3de8-1c0c-40c9-b19f-20dcf072bdd6",
                Name = "TenantAdmin",
                NormalizedName = "TENANTADMIN",
                ConcurrencyStamp = "eed7af6e-1c4d-4ab1-8ed2-1f03e4cef8d8",
                Description = "Manage tenant personnel",
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
                    // No password is seeded: OIDCHostedService generates a unique random password
                    // at first startup, so no bootstrap credential is ever committed to source
                    // control or baked into a migration.
                    Gender = "Male",
                    TimeZone = "AUS Eastern Standard Time",
                    LocaleCode = "en-IN",
                    ProfileImage = "/_content/BlogArray.SaaS.Resources/resources/images/user-icon.webp",
                    AccessFailedCount = 0,
                    LockoutEnabled = true,
                    // The bootstrap password is temporary: the first login redirects to the
                    // reset-password flow until a new password is set.
                    MustChangePassword = true,
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

