using BlogArray.SaaS.Domain.Helpers;
using Xunit;

namespace BlogArray.SaaS.UnitTests;

public class ApiKeyHasherTests
{
    [Fact]
    public void Hash_ReturnsKnownSha256Vector()
    {
        Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD",
            ApiKeyHasher.Hash("abc"));
    }

    [Fact]
    public void Hash_IsDeterministicAnd64Characters()
    {
        string first = ApiKeyHasher.Hash("0a1b2c3d4e5f60718293a4b5c6d7e8f9");
        string second = ApiKeyHasher.Hash("0a1b2c3d4e5f60718293a4b5c6d7e8f9");

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void Hash_DifferentKeysProduceDifferentHashes()
    {
        Assert.NotEqual(ApiKeyHasher.Hash("key-one"), ApiKeyHasher.Hash("key-two"));
    }

    [Fact]
    public void GetPrefix_ReturnsRequestedCharacters()
    {
        Assert.Equal("abcdefgh", ApiKeyHasher.GetPrefix("abcdefghijklmnopqrstuvwxyz", 8));
    }

    [Fact]
    public void GetPrefix_ReturnsWholeKeyWhenShorterThanRequested()
    {
        Assert.Equal("abc", ApiKeyHasher.GetPrefix("abc", 8));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void GetPrefix_ReturnsEmptyForNonPositiveLength(int length)
    {
        Assert.Equal(string.Empty, ApiKeyHasher.GetPrefix("abcdefghijklmnopqrstuvwxyz", length));
    }

    [Fact]
    public void GetPrefix_ClampsLengthToSixteen()
    {
        Assert.Equal(16, ApiKeyHasher.GetPrefix("abcdefghijklmnopqrstuvwxyz", 100).Length);
    }
}
