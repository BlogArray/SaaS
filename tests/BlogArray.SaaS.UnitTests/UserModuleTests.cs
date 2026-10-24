using BlogArray.SaaS.Domain.DTOs;
using Xunit;

namespace BlogArray.SaaS.UnitTests;

public class UserModuleTests
{
    [Fact]
    public void IsLockedOut_NoLockoutEnd_IsFalse()
    {
        // LockoutEnabled is true for every account; it must not read as "locked".
        UserToolbar toolbar = new() { LockoutEnabled = true, LockoutEnd = null };

        Assert.False(toolbar.IsLockedOut);
    }

    [Fact]
    public void IsLockedOut_PastLockoutEnd_IsFalse()
    {
        UserToolbar toolbar = new() { LockoutEnabled = true, LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-1) };

        Assert.False(toolbar.IsLockedOut);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsLockedOut_FutureLockoutEnd_IsTrue(bool administratorLock)
    {
        UserToolbar toolbar = new()
        {
            LockoutEnabled = true,
            LockoutEnd = administratorLock ? DateTimeOffset.MaxValue : DateTimeOffset.UtcNow.AddHours(1)
        };

        Assert.True(toolbar.IsLockedOut);
    }

    [Fact]
    public void DescribeLockoutEnd_IndefiniteLock_SaysUntilUnlocked()
    {
        Assert.True(UserToolbar.IsIndefiniteLockout(DateTimeOffset.MaxValue));
        Assert.Equal("until an administrator unlocks it", UserToolbar.DescribeLockoutEnd(DateTimeOffset.MaxValue));
    }

    [Fact]
    public void DescribeLockoutEnd_TimedLock_ShowsUtcTime()
    {
        // 18:00 at UTC+05:30 is 12:30 UTC.
        DateTimeOffset end = new(2026, 10, 24, 18, 0, 0, TimeSpan.FromMinutes(330));

        Assert.False(UserToolbar.IsIndefiniteLockout(end));
        Assert.Equal("until 24 Oct 2026, 12:30 UTC", UserToolbar.DescribeLockoutEnd(end));
    }

    [Theory]
    [InlineData(true, null, false, true)]          // confirmed email, no phone: nothing left to confirm
    [InlineData(true, "", false, true)]
    [InlineData(true, "+15550100", false, false)]  // phone set but unconfirmed
    [InlineData(true, "+15550100", true, true)]
    [InlineData(false, null, false, false)]        // email not confirmed
    public void IsContactConfirmed_RequiresPhoneOnlyWhenSet(bool emailConfirmed, string? phone, bool phoneConfirmed, bool expected)
    {
        Assert.Equal(expected, UserToolbar.IsContactConfirmed(emailConfirmed, phone, phoneConfirmed));
    }

    [Theory]
    [InlineData("Ada L.", "Ada", "Lovelace", "ada@example.com", "Ada L.")]
    [InlineData("  ", "Ada", "Lovelace", "ada@example.com", "Ada Lovelace")]
    [InlineData(null, "Ada", null, "ada@example.com", "Ada")]
    [InlineData(null, "", "", "ada@example.com", "ada@example.com")]
    public void ResolveDisplayName_FallsBackToFullNameThenEmail(string? displayName, string first, string? last, string email, string expected)
    {
        CreateUserViewModel model = new()
        {
            DisplayName = displayName!,
            FirstName = first,
            LastName = last,
            Email = email
        };

        Assert.Equal(expected, model.ResolveDisplayName());
    }
}
