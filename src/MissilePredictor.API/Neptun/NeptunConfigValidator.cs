using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace MissilePredictor.API.Neptun;

/// <summary>Validates <see cref="NeptunConfig.Areas"/>, which data annotations do not cover.</summary>
public sealed class NeptunConfigValidator : IValidateOptions<NeptunConfig>
{
    public ValidateOptionsResult Validate(string? name, NeptunConfig options)
    {
        if (options.Areas.Count == 0)
            return ValidateOptionsResult.Fail("Neptun:Areas must contain at least one area.");

        var failures = new List<string>();
        for (var i = 0; i < options.Areas.Count; i++)
        {
            var area = options.Areas[i];
            var results = new List<ValidationResult>();
            if (Validator.TryValidateObject(area, new ValidationContext(area), results, validateAllProperties: true))
                continue;

            failures.AddRange(results.Select(r => $"Neptun:Areas[{i}] ({area.Title}): {r.ErrorMessage}"));
        }

        failures.AddRange(options.Areas
            .GroupBy(a => a.Title, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => $"Neptun:Areas has duplicate title '{g.Key}'."));

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
