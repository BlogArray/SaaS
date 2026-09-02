namespace BlogArray.SaaS.Web.Helpers;

public static class PasskeyCredentialNameResolver
{
    //https://raw.githubusercontent.com/passkeydeveloper/passkey-authenticator-aaguids/refs/heads/main/aaguid.json
    private static readonly Dictionary<Guid, string> CredentialNames =
        new()
        {
            [Guid.Parse("fbfc3007-154e-4ecc-8c0b-6e020557d7bd")] = "Apple Passwords",
            [Guid.Parse("ea9b8d66-4d01-1d21-3ce4-b6b48cb575d4")] = "Google Password Manager",
            [Guid.Parse("d3452668-01fd-4c12-926c-83a4204853aa")] = "Microsoft Password Manager",
            [Guid.Parse("9ddd1817-af5a-4672-a2b9-3e3dd95000a9")] = "Windows Hello",
            [Guid.Parse("6028b017-b1d4-4c02-b4b3-afcdafc96bb2")] = "Windows Hello",
            [Guid.Parse("bada5566-a7aa-401f-bd96-45619a55120d")] = "1Password",
            [Guid.Parse("531126d6-e717-415c-9320-3d9aa6981239")] = "Dashlane",
            [Guid.Parse("0ea242b4-43c4-4a1b-8b17-dd6d0b6baec6")] = "Keeper",
            [Guid.Parse("f3809540-7f14-49c1-a8b3-8f813b225541")] = "Enpass",
            [Guid.Parse("a10c6dd9-465e-4226-8198-c7c44b91c555")] = "Kaspersky Password Manager",
            [Guid.Parse("fdb141b2-5d84-443e-8a35-4698c205a502")] = "KeePassXC",
            [Guid.Parse("b78a0a55-6ef8-d246-a042-ba0f6d55050c")] = "LastPass",
            [Guid.Parse("b84e4048-15dc-4dd0-8640-f4f60813c8af")] = "NordPass",
            [Guid.Parse("fa37f553-f9b6-4adb-ac53-8bbb57ebdf0d")] = "Norton Password Manager",
            [Guid.Parse("50726f74-6f6e-5061-7373-50726f746f6e")] = "Proton Pass",
            [Guid.Parse("d9be9d39-e6a6-4c28-a581-32b044d986e4")] = "Sticky Password",
            [Guid.Parse("b35a26b2-8f6e-4697-ab1d-d44db4da28c6")] = "Zoho Vault",
            [Guid.Parse("22248c4c-7a12-46e2-9a41-44291b373a4d")] = "LogMeOnce",
            [Guid.Parse("de503f9c-21a4-4f76-b4b7-558eb55c6f89")] = "Devolutions",
            [Guid.Parse("45e3057e-b2f9-48ed-912f-9b901e153b16")] = "Uniqkey",
        };

    private const string FallbackName = "Passkey";

    public static string Resolve(Guid aaguid)
        => aaguid != Guid.Empty && CredentialNames.TryGetValue(aaguid, out string? name)
            ? name
            : FallbackName;
}
