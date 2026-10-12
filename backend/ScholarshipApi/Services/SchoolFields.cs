namespace ScholarshipApi.Services;

public static class SchoolFields
{
    /// <summary>Validated, trimmed values for updating a Schools row (address columns are NOT NULL, so blanks become "").</summary>
    public static object Validate(string? name, string? addressLine1, string? addressLine2, string? city, string? state, string? zip) => new
    {
        Name = InputValidation.Required(name, "School name", 255),
        AddressLine1 = InputValidation.Optional(addressLine1, "Address line 1", 500, ""),
        AddressLine2 = InputValidation.Optional(addressLine2, "Address line 2", 500, ""),
        City = InputValidation.Optional(city, "City", 200, ""),
        State = InputValidation.Optional(state, "State", 100, ""),
        Zip = InputValidation.Optional(zip, "Zip", 20, ""),
    };
}
