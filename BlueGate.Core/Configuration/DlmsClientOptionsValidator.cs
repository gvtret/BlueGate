using System.Linq;
using BlueGate.Core.Models;
using Microsoft.Extensions.Options;

namespace BlueGate.Core.Configuration;

/// <summary>
/// Validates that DLMS client options include complete mapping profiles.
/// </summary>
public class DlmsClientOptionsValidator : IValidateOptions<DlmsClientOptions>
{
    public ValidateOptionsResult Validate(string? name, DlmsClientOptions options)
    {
        if (options.Profiles is null || options.Profiles.Count == 0)
            return ValidateOptionsResult.Success;

        var invalidProfiles = options.Profiles
            .Select((profile, index) => (profile, index))
            .Where(tuple => IsMissingRequiredFields(tuple.profile))
            .Select(tuple => $"Profiles[{tuple.index}] must include both ObisCode and OpcNodeId.")
            .ToList();

        return invalidProfiles.Count > 0
            ? ValidateOptionsResult.Fail(invalidProfiles)
            : ValidateOptionsResult.Success;
    }

    private static bool IsMissingRequiredFields(MappingProfile profile) =>
        string.IsNullOrWhiteSpace(profile.ObisCode) || string.IsNullOrWhiteSpace(profile.OpcNodeId);
}
