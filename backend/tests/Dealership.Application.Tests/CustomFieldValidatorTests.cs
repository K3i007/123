using Dealership.Domain;
using Xunit;
namespace Dealership.Application.Tests;
public sealed class CustomFieldValidatorTests
{
    private static readonly CustomFieldDefinition Select = new() { Key = "doors", Label = "Puertas", Type = CustomFieldType.Select, IsRequired = true, Options = "[\"3\",\"5\"]" };
    [Fact] public void Validates_select_value() => CustomFieldValidator.Validate("{\"doors\":\"5\"}", [Select]);
    [Fact] public void Rejects_invalid_select_value() => Assert.Throws<DomainRuleException>(() => CustomFieldValidator.Validate("{\"doors\":\"4\"}", [Select]));
    [Fact] public void Ignores_deactivated_definition() { Select.IsActive = false; CustomFieldValidator.Validate("{}", [Select]); Select.IsActive = true; }
    [Fact] public void Rejects_missing_required_field() => Assert.Throws<DomainRuleException>(() => CustomFieldValidator.Validate("{}", [Select]));
}
