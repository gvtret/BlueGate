using System.Collections.Generic;
using System.Linq;
using BlueGate.Core.Configuration;
using Gurux.DLMS.Enums;
using Gurux.DLMS.Objects.Enums;
using Microsoft.Extensions.Options;
using Xunit;

public class DlmsClientOptionsValidatorTests
{
    private readonly DlmsClientOptionsValidator _validator = new();

    [Fact]
    public void Should_Pass_With_Suite0_And_No_Security()
    {
        var options = new DlmsClientOptions
        {
            SecuritySuite = SecuritySuite.Suite0,
            Security = Security.None
        };

        var result = _validator.Validate(string.Empty, options);

        Assert.Equal(ValidateOptionsResult.Success, result);
    }

    [Fact]
    public void Should_Fail_When_Suite0_Has_Security_Mode()
    {
        var options = new DlmsClientOptions
        {
            SecuritySuite = SecuritySuite.Suite0,
            Security = Security.Authentication
        };

        var result = _validator.Validate(string.Empty, options);

        Assert.False(result.Succeeded);
        var failures = Assert.IsAssignableFrom<IEnumerable<string>>(result.Failures);
        Assert.Contains(failures, failure => failure.Contains("not supported with security suite 0"));
    }

    [Fact]
    public void Should_Fail_When_Secure_Suite_Lacks_Mode()
    {
        var options = CreateSecureOptions(SecuritySuite.Suite1, Security.None);

        var result = _validator.Validate(string.Empty, options);

        Assert.False(result.Succeeded);
        var failures = Assert.IsAssignableFrom<IEnumerable<string>>(result.Failures);
        Assert.Contains(failures, failure => failure.Contains("requires Security"));
    }

    [Theory]
    [InlineData(Security.Authentication)]
    [InlineData(Security.Encryption)]
    [InlineData(Security.AuthenticationEncryption)]
    public void Should_Pass_For_Supported_Secure_Mode(Security security)
    {
        var options = CreateSecureOptions(SecuritySuite.Suite1, security);

        var result = _validator.Validate(string.Empty, options);

        Assert.Equal(ValidateOptionsResult.Success, result);
    }

    private static DlmsClientOptions CreateSecureOptions(SecuritySuite suite, Security security) => new()
    {
        SecuritySuite = suite,
        Security = security,
        BlockCipherKey = "00112233445566778899AABBCCDDEEFF",
        AuthenticationKey = "FFEEDDCCBBAA99887766554433221100",
        SystemTitle = "4C495645444C4D53",
        InvocationCounterPath = "/tmp/invocounter.txt",
        Authentication = Authentication.Low,
        Password = "password",
        Profiles = DlmsClientOptions.DefaultProfiles.ToList()
    };
}
