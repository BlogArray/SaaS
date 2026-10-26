---
name: identity-db-migration
description: Change the BlogArray.SaaS identity database schema - entities in BlogArray.SaaS.Domain mapped by OpenIdDbContext (users, sessions, sign-in/audit events, passkeys, OpenIddict tables) - and add the EF Core migration. Use when adding or changing an entity property, index or table.
---

# Identity database changes

- Context: `src/Core/BlogArray.SaaS.OpenId/OpenIdDbContext.cs`, model configuration in
  `OpenIdDbContextExtensions.cs` (lengths, indexes, owned types).
- Entities: `src/Core/BlogArray.SaaS.Domain/Entities/`.
- Migrations: `src/Core/BlogArray.SaaS.OpenId/Migrations/` (the only migrations project).
- Design-time factory: `OpenIdDbContextDesignTimeFactory` reads `ConnectionStrings:IdentityContext`
  (falls back to `localhost`, `BlogArray.SaaS.Identity`, Windows auth).
- Pending migrations are applied at Identity startup (`ConfigureOpenIdApplication.AddOpenIdServer`
  → `MigrateAsync`), so the Identity app migrates the shared database for all apps.
- The tenant app database (`SaasAppDbContext` in Bootstrapper) is separate and per tenant.

## Steps

1. Change the entity. Add `[StringLength]` on strings and mirror lengths/indexes in
   `OpenIdDbContextExtensions` like existing properties. Computed helpers that aren't columns get
   `[NotMapped]` (see `UserSession.IsExpired`).
2. Add the migration from the OpenId project:
   ```bash
   dotnet ef migrations add <PascalCaseName> --project src/Core/BlogArray.SaaS.OpenId --startup-project src/Core/BlogArray.SaaS.OpenId
   ```
   (The design-time factory makes the OpenId project its own startup project.)
3. Review the generated migration and the snapshot: no unintended drops, sensible defaults for
   existing rows (nullable new columns or explicit defaults), index filters as intended.
4. Build all three apps (`verify-change`). Don't run `dotnet ef database update` against the local
   database unless the user asks; the Identity app applies it on its next start.
5. If the change affects what's shown (sessions, events), update the Identity and TenantSuite
   views and any queries that should filter on the new column.

Never edit an existing migration that may have been applied; add a new one.
