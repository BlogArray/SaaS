using System.ComponentModel.DataAnnotations;
using BlogArray.SaaS.Domain.Attributes;
using Xunit;

namespace BlogArray.SaaS.UnitTests;

public class RequiredIfAttributeTests
{
    private sealed class Sample
    {
        public bool Enabled { get; set; }

        [RequiredIf("Enabled", true, ErrorMessage = "Value is required when enabled")]
        public string? Value { get; set; }
    }

    private static List<ValidationResult> Validate(object instance)
    {
        List<ValidationResult> results = [];
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void DependentPropertyTrue_AndValueMissing_Fails()
    {
        List<ValidationResult> results = Validate(new Sample { Enabled = true, Value = null });

        Assert.Contains(results, r => r.ErrorMessage == "Value is required when enabled");
    }

    [Fact]
    public void DependentPropertyTrue_AndValueWhitespace_Fails()
    {
        List<ValidationResult> results = Validate(new Sample { Enabled = true, Value = "   " });

        Assert.Contains(results, r => r.ErrorMessage == "Value is required when enabled");
    }

    [Fact]
    public void DependentPropertyTrue_AndValuePresent_Passes()
    {
        List<ValidationResult> results = Validate(new Sample { Enabled = true, Value = "ok" });

        Assert.DoesNotContain(results, r => r.ErrorMessage == "Value is required when enabled");
    }

    [Fact]
    public void DependentPropertyFalse_AndValueMissing_Passes()
    {
        List<ValidationResult> results = Validate(new Sample { Enabled = false, Value = null });

        Assert.Empty(results);
    }
}
