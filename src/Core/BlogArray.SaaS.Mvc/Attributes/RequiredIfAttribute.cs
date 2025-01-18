using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace BlogArray.SaaS.Mvc.Attributes;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
public class RequiredIfAttribute(string dependentProperty, object targetValue) : ValidationAttribute, IClientModelValidator
{
    protected override ValidationResult IsValid(object? value, ValidationContext validationContext)
    {
        object instance = validationContext.ObjectInstance;
        System.Reflection.PropertyInfo? propertyInfo = instance.GetType().GetProperty(dependentProperty);

        if (propertyInfo == null)
        {
            return new ValidationResult($"Property '{dependentProperty}' not found.");
        }

        object? dependentPropertyValue = propertyInfo.GetValue(instance);

        if (dependentPropertyValue?.ToString() == targetValue?.ToString())
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
        MergeAttribute(context.Attributes, "data-val-requiredif-dependentproperty", dependentProperty);
        MergeAttribute(context.Attributes, "data-val-requiredif-targetvalue", targetValue?.ToString() ?? "");
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
