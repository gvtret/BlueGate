using System.Linq;
using System.Collections.Generic;
using BlueGate.Core.Models;
using Gurux.DLMS.Objects.Enums;
using Microsoft.Extensions.Options;

namespace BlueGate.Core.Configuration;

/// <summary>
/// Validates that DLMS client options include complete mapping profiles.
/// </summary>
public class DlmsClientOptionsValidator : IValidateOptions<DlmsClientOptions>
{
    public ValidateOptionsResult Validate(string? name, DlmsClientOptions options)
    {
        var failures = new List<string>();

        ValidateProfiles(options, failures);
        ValidateSecurity(options, failures);

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static bool IsMissingRequiredFields(MappingProfile profile) =>
        string.IsNullOrWhiteSpace(profile.ObisCode) || string.IsNullOrWhiteSpace(profile.OpcNodeId);

    private static void ValidateProfiles(DlmsClientOptions options, List<string> failures)
    {
        if (options.Profiles is null || options.Profiles.Count == 0)
        {
            return;
        }

        var invalidProfiles = options.Profiles
            .Select((profile, index) => (profile, index))
            .Where(tuple => IsMissingRequiredFields(tuple.profile))
            .Select(tuple => $"Profiles[{tuple.index}] must include both ObisCode and OpcNodeId.");

        failures.AddRange(invalidProfiles);
    }

    private static void ValidateSecurity(DlmsClientOptions options, List<string> failures)
    {
        if (options.SecuritySuite == SecuritySuite.Suite0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.BlockCipherKey))
        {
            failures.Add("BlockCipherKey is required when security suite 1 or 2 is configured.");
        }
        else if (!IsHex(options.BlockCipherKey))
        {
            failures.Add("BlockCipherKey must be a valid hex string.");
        }

        if (string.IsNullOrWhiteSpace(options.AuthenticationKey))
        {
            failures.Add("AuthenticationKey is required when security suite 1 or 2 is configured.");
        }
        else if (!IsHex(options.AuthenticationKey))
        {
            failures.Add("AuthenticationKey must be a valid hex string.");
        }

        if (string.IsNullOrWhiteSpace(options.SystemTitle))
        {
            failures.Add("SystemTitle is required when security suite 1 or 2 is configured.");
        }
        else if (!IsHex(options.SystemTitle))
        {
            failures.Add("SystemTitle must be a valid hex string.");
        }

        if (string.IsNullOrWhiteSpace(options.InvocationCounterPath))
        {
            failures.Add("InvocationCounterPath is required when security suite 1 or 2 is configured.");
        }
    }

    private static bool IsHex(string value) =>
        value.Length % 2 == 0 && value.All(c => IsHexDigit(c));

    private static bool IsHexDigit(char c) =>
        c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
}
