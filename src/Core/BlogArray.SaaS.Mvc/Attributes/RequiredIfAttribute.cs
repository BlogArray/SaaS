using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace BlogArray.SaaS.Mvc.Attributes;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
public class RequiredIfAttribute : ValidationAttribute, IClientModelValidator
{
    private readonly string _dependentProperty;
    private readonly object _targetValue;

    public RequiredIfAttribute(string dependentProperty, object targetValue)
    {
        _dependentProperty = dependentProperty;
        _targetValue = targetValue;
    }

    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        object instance = validationContext.ObjectInstance;
        object? dependentPropertyValue = instance.GetType().GetProperty(_dependentProperty).GetValue(instance);

        if (dependentPropertyValue?.ToString() == _targetValue?.ToString())
        {
            if (value == null || value is string str && string.IsNullOrWhiteSpace(str))
            {
                return new ValidationResult(ErrorMessage ?? $"{validationContext.DisplayName} is required.");
            }
        }

        return ValidationResult.Success;
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, "data-val-requiredif", ErrorMessage ?? $"{context.ModelMetadata.DisplayName} is required.");
        MergeAttribute(context.Attributes, "data-val-requiredif-dependentproperty", _dependentProperty);
        MergeAttribute(context.Attributes, "data-val-requiredif-targetvalue", _targetValue?.ToString() ?? "");
    }

    private static bool MergeAttribute(IDictionary<string, string> attributes, string key, string value)
    {
        if (attributes.ContainsKey(key))
        {
            return false;
        }
        attributes.Add(key, value);
        return true;
    }
}
