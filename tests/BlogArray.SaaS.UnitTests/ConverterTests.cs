using BlogArray.SaaS.Web.Helpers;
using Xunit;

namespace BlogArray.SaaS.UnitTests;

public class ConverterTests
{
    [Theory]
    [InlineData("1234", 1234)]
    [InlineData("1,234", 1234)]
    public void ToInt_ParsesValue(object value, int expected)
    {
        Assert.Equal(expected, Converter.ToInt(value));
    }

    [Fact]
    public void ToInt_Null_ReturnsDefault()
    {
        Assert.Equal(0, Converter.ToInt(null!));
        Assert.Equal(7, Converter.ToInt(null!, 7));
    }

    [Fact]
    public void ToInt_Unparsable_ReturnsDefault()
    {
        Assert.Equal(5, Converter.ToInt("abc", 5));
    }

    [Fact]
    public void ToBoolean_ParsesValue()
    {
        Assert.True(Converter.ToBoolean("true"));
        Assert.False(Converter.ToBoolean("false", true));
    }

    [Fact]
    public void ToBoolean_Unparsable_ReturnsDefault()
    {
        Assert.False(Converter.ToBoolean("junk", false));
    }

    [Fact]
    public void CheckNulls_Null_ReturnsDefault()
    {
        Assert.Equal(string.Empty, Converter.CheckNulls(null!));
        Assert.Equal("fallback", Converter.CheckNulls(null!, "fallback"));
    }

    [Fact]
    public void ToString_Null_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, Converter.ToString(null!));
    }
}
